using Rhino.Geometry;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;

namespace WireTools
{

    public class KnifeInteraction : Grasshopper.GUI.Canvas.Interaction.GH_AbstractInteraction
    { 
        protected bool dragging;
        protected bool keyPressed;
        protected List<PointF> pts;
        protected Timer timer;
        protected int counter;

        public KnifeInteraction(GH_Canvas canvas, GH_CanvasMouseEvent mouseEvent) : base(canvas, mouseEvent)
        {
            canvas.CanvasPostPaintWidgets += Canvas_PostPaintWidgets;
            Cursor.Hide();
            canvas.Invalidate();

            pts = new List<PointF>();
            timer = new Timer
            {
                Interval = 10
            };
            timer.Tick += TickTimer;
        }

        #region Methods
        public void CheckIfCut(Grasshopper.GUI.Canvas.GH_Canvas Canvas, Grasshopper.GUI.GH_CanvasMouseEvent e)
        {
            if (pts == null || pts.Count < 2) 
                return;
            try
            {
                RGH_Wire wire = null;
                foreach (RGH_Wire w in RGH_Wire.FindAllVisibleWires(Canvas))
                {
                    if (w.IsTouching(e.CanvasLocation, 5))
                    {
                        wire = w;
                        break;
                    }
                }

                if (wire != null)
                {
                    this.Canvas.Document.AutoSave(GH_AutoSaveTrigger.wire_event);
                    this.Canvas.Document.UndoUtil.RecordWireEvent("Remove wire", wire.Target);
                    wire.Target.RemoveSource(wire.Source);
                    wire = null;
                    Canvas.Document.NewSolution(false);
                }
            }
            catch (Exception ex)
            {
                Rhino.RhinoApp.WriteLine(ex.ToString());
            }
        }

        public static void UnsheatheKnife(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.K)
            {
                var canvas = Grasshopper.Instances.ActiveCanvas;
                if (canvas.IsDocument)
                {
                    if (!(canvas.ActiveInteraction is KnifeInteraction))
                    {
                        canvas.ActiveInteraction = new KnifeInteraction(canvas, new Grasshopper.GUI.GH_CanvasMouseEvent(canvas.Viewport,
                          new MouseEventArgs(MouseButtons.None, 0, canvas.CursorControlPosition.X, canvas.CursorControlPosition.Y, 0)));
                    }

                    typeof(KeyEventArgs)
                       .GetField("keyData", BindingFlags.NonPublic | BindingFlags.Instance)
                       .SetValue(e, Keys.Select);
                }
                e.Handled = true;
            }
        }

        public void StartTimer()
        {
            counter = 0;
            if (pts.Count > 0) 
                pts = new List<PointF>();
            timer.Start();
        }
        public void StopTimer()
        {
            timer.Stop();
        }
        public void TickTimer(object sender, EventArgs e)
        {
            if (pts.Count > 0)
            {
                if (counter > 5)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        if (pts.Count == 0)
                            break;
                        pts.RemoveAt(0);
                    }

                    this.Canvas.Invalidate();
                }
            }
            else
            {

            }
            counter++;
        }
        #endregion

        #region Render
        private void Canvas_PostPaintWidgets(GH_Canvas sender)
        {
            try
            {
                var graphics = sender.Graphics;

                if (this.IsActive)
                { 
                    if (pts != null && pts.Count > 1)
                    {
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            path.AddCurve(pts.ToArray());
                            using (Pen pen = new Pen(Color.Black, 1.5f)
                            {
                                EndCap = LineCap.Triangle,
                                StartCap = LineCap.Triangle
                            })
                            {
                                graphics.DrawPath(pen, path);
                            }

                        }
                    }

                }
                var pt = sender.CursorCanvasPosition;
                pt.Y -= 40;
                graphics.DrawImage(Properties.Resources.Knife2_40x40, pt);
            }
            catch (Exception e)
            {
                Rhino.RhinoApp.WriteLine(e.ToString());
            }
        }

        #endregion

        #region Interaction
        public override GH_ObjectResponse RespondToKeyDown(GH_Canvas sender, KeyEventArgs e)
        {
            if (!keyPressed)
            {
                sender.Invalidate();
                keyPressed = true;
            }

            e.Handled = true;
            return GH_ObjectResponse.Capture;
        }
        public override GH_ObjectResponse RespondToKeyUp(GH_Canvas sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.K)
            {
                dragging = keyPressed = false;
                StopTimer();
                sender.Invalidate();
            }
            e.Handled = true;
            return GH_ObjectResponse.Release;
        }
        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                StartTimer();
                dragging = true;
                this.Canvas.Invalidate();
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = false;
                StopTimer();
                if (pts.Count > 0) pts = new List<PointF>();
                this.Canvas.Invalidate();
            }
            return base.RespondToMouseUp(sender, e);

        }
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            this.Canvas.Invalidate();
            if (dragging)
            {
                if (pts.Count == 0)
                {
                    pts.Add(e.CanvasLocation);
                    CheckIfCut(sender, e);
                }
                else
                {
                    if (GH_GraphicsUtil.Distance(pts[pts.Count-1], e.CanvasLocation) > 2)
                    {
                        pts.Add(e.CanvasLocation);
                        CheckIfCut(sender, e);
                    }
                }


                return GH_ObjectResponse.Handled;
            }
            else
            {
                if (pts.Count > 0)
                {
                    pts = new List<PointF>();
                }
            }
            return base.RespondToMouseMove(sender, e);
        }
        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            return base.RespondToMouseDoubleClick(sender, e);
        }
        #endregion

        #region Desactivate
        public override bool DeactivateOnFocusLoss { get { return true; } }
        public override void Destroy()
        {
            Cursor.Show();
            Canvas.CanvasPostPaintWidgets -= Canvas_PostPaintWidgets;
            this.Canvas.Invalidate();
            base.Destroy();
        }
        #endregion
    }

}
