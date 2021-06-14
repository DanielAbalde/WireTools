using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;

namespace WireTools
{

    public class AutoWireInteraction : GH_DragInteraction
    {

        protected bool controlPressed;
        protected RGH_Wire wire;
        protected IGH_DocumentObject obj;

        public AutoWireInteraction(GH_Canvas canvas, GH_CanvasMouseEvent e) : base(canvas, e)
        {
            canvas.CanvasPostPaintWires += Canvas_PostPaintWires;
        }

        #region Methods
        public static void StartAutoWireInteraction(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                var canvas = Grasshopper.Instances.ActiveCanvas;
                if (canvas.IsDocument)
                {
                    if (!(canvas.ActiveInteraction is AutoWireInteraction) && 
                        canvas.ActiveInteraction is GH_DragInteraction)
                    {
                        canvas.ActiveInteraction = new AutoWireInteraction(canvas, new GH_CanvasMouseEvent(canvas.Viewport,
                          new MouseEventArgs(MouseButtons.None, 0, canvas.CursorControlPosition.X, canvas.CursorControlPosition.Y, 0)));
                        e.Handled = true;
                    }
                }
            }
        }

        protected bool IsAutoWireable()
        {
            if (base.AttributeCount != 1)
                return false;

            var atts = typeof(GH_DragInteraction).GetField("m_att",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(this) as List<IGH_Attributes>;
            if (atts == null)
                return false;

            var att = atts[0];
            obj = att.GetTopLevel.DocObject;

            wire = null;
            var wires = RGH_Wire.FindAllVisibleWires(this.Canvas);
            if (wires.Count() == 0) 
                return false;

            var bounds = att.Bounds;
            foreach (var w in wires)
            {
                var path = w.GetPath(2f);
                var reg = new Region(path);

                if (reg.IsVisible(bounds))
                { 
                    if (obj is IGH_Param param)
                    { 
                        if (param.Sources.Contains(w.Source) ||
                            param.Recipients.Contains(w.Target)) 
                            continue;
                        //if (w.Source.Recipients.Contains(param) || w.Target.Sources.Contains(param)) continue;
                        /*
                        if (param.Sources.Contains(w.Source) || param.Sources.Contains(w.Target) ||
                            param.Recipients.Contains(w.Source) || param.Recipients.Contains(w.Target))
                        {
                            continue;
                        }*/
                    }
                    else if (obj is IGH_Component)
                    {

                    }
                    wire = w;
                    break;
                }
            }
            return true;
        }

        protected bool AutoWire()
        {
            if (wire == null || !this.Canvas.IsDocument)
                return false;
            var doc = this.Canvas.Document;
         
            if (!GetObjectParameters(out IGH_Param input, out IGH_Param output)) 
                return false;

            doc.AutoSave(GH_AutoSaveTrigger.wire_event);
            var actions = new List<Grasshopper.Kernel.Undo.IGH_UndoAction>();

            actions.AddRange(doc.UndoUtil.CreateWireEvent("Remove wire", wire.Target).Actions);
            wire.Target.RemoveSource(wire.Source);
             
            if (input != null)
            {
                if (input.SourceCount > 0)
                {
                    actions.AddRange(doc.UndoUtil.CreateWireEvent("Remove sources", input).Actions);
                    input.RemoveAllSources();
                }
                actions.AddRange(doc.UndoUtil.CreateWireEvent("Add wire", input).Actions);
                input.AddSource(wire.Source);

            }

            if (output != null)
            {
                if (output.Recipients.Count > 0)
                {
                    for (int i = output.Recipients.Count - 1; i > -1; i--)
                    {
                        var recipient = output.Recipients[i];
                        actions.AddRange(doc.UndoUtil.CreateWireEvent("Remove source", recipient).Actions);
                        recipient.RemoveSource(output);
                    }
                }
                actions.AddRange(doc.UndoUtil.CreateWireEvent("Add wire", wire.Target).Actions);
                wire.Target.AddSource(output);
            }

            doc.UndoUtil.RecordEvent("AutoWire", actions);

            obj.ExpireSolution(true);

            return true;
        }
        protected bool CanCast(IGH_Param from, IGH_Param to)
        {
            if (from == null || to == null || object.ReferenceEquals(from, to) || !from.Attributes.HasOutputGrip || !to.Attributes.HasInputGrip) return false;
            if (from.Attributes.IsTopLevel && to.Attributes.IsTopLevel ||
                from.Type == to.Type ||
                from.Type.IsAssignableFrom(to.Type) ||
                from is Param_GenericObject ||
                from is Param_Geometry ||
                from is GH_Panel ||
                to is Param_GenericObject ||
                to is Param_Geometry ||
                to is GH_Panel 
                ) return true;

            return false;
        }
        protected bool GetObjectParameters(out IGH_Param Input, out IGH_Param Output)
        {
            Input = null;
            Output = null;

            if (obj == null || wire == null) 
                return false;

            if (obj is IGH_Param param)
            {
                if (CanCast(wire.Source, param))
                {
                    Input = param;
                    Output = param;
                }
                else if (CanCast(param, wire.Target)) 
                {
                    Output = param; 
                }
            }
            else if (obj is IGH_Component comp)
            {
                foreach (var inParam in comp.Params.Input)
                {
                    if (CanCast(wire.Source, inParam))
                    {
                        Input = inParam;
                        break;
                    }
                }
                foreach (var outParam in comp.Params.Output)
                {
                    if (CanCast(outParam, wire.Target))
                    {
                        Output = outParam;
                        break;
                    }
                }
            }

            return Input != null || Output != null;
        }
        #endregion

        #region Render
        private void Canvas_PostPaintWires(GH_Canvas sender)
        {
            try
            {
                var graphics = sender.Graphics;

                if (this.IsActive && GetObjectParameters(out IGH_Param input, out IGH_Param output))
                {
                    if (input != null)
                    {
                        var w0 = new RGH_Wire(wire.Source, input);
                        using (var path = w0.GetPath())
                        using (var pen = new Pen(Color.White, 2f))
                            graphics.DrawPath(pen, path);
                    }
                    if (output != null)
                    {
                        var w0 = new RGH_Wire(output, wire.Target);
                        using (var path = w0.GetPath())
                        using (var pen = new Pen(Color.White, 2f))
                            graphics.DrawPath(pen, path);
                    }
                }
            }
            catch (Exception e)
            {
                Rhino.RhinoApp.WriteLine(e.ToString());
            }
        }

        #endregion

        #region Interaction
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var res = base.RespondToMouseMove(sender, e);
            if (IsAutoWireable())
            {
                sender.Invalidate();
            }
            return res;
        }
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        { 
            if (IsAutoWireable())
            {
                if (AutoWire()) 
                    return GH_ObjectResponse.Release;
            }

            return base.RespondToMouseUp(sender, e);
        }
        public override GH_ObjectResponse RespondToKeyDown(GH_Canvas sender, KeyEventArgs e)
        {
            controlPressed = e.KeyCode == Keys.Control;
            //Rhino.RhinoApp.WriteLine(controlPressed.ToString());
            return base.RespondToKeyDown(sender, e);
        }
        public override GH_ObjectResponse RespondToKeyUp(GH_Canvas sender, KeyEventArgs e)
        {
            controlPressed = false;
            return base.RespondToKeyUp(sender, e);
        }
        #endregion

        #region Desactivate 
        public override void Destroy()
        {
            Canvas.CanvasPostPaintWires -= Canvas_PostPaintWires;
            base.Destroy();
        }
        #endregion
    }

}
