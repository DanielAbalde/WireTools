
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.GUI.Canvas;

namespace WireTools.Plugin
{
    public class Plugin : GH_AssemblyInfo
    {
        public override string Name => "WireTools";
        public override Bitmap Icon => null;
        public override string Description => "";
        public override Guid Id => new Guid("5f5511dd-4cdc-4887-abac-feefa3c1e790");
        public override string AuthorName => "Daniel Abalde";
        public override string AuthorContact => "https://discord.gg/XFGCpXewN4";
    }

    public class Priority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            Instances.CanvasCreated += AppendKnifeInteraction;
            //Instances.CanvasCreated += AppendAutoWireInteraction; // Needs fix.
            return GH_LoadingInstruction.Proceed;
        }

        private void AppendKnifeInteraction(GH_Canvas canvas)
        {
            Instances.CanvasCreated -= AppendKnifeInteraction;

            var editor = Grasshopper.Instances.DocumentEditor;

            var events = (System.ComponentModel.EventHandlerList)typeof(Control)
            .GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(editor, null);
            var key = typeof(Control).GetField("EventKeyDown", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Delegate handlers = events[key]; 
            if (handlers != null)
            {
                foreach (Delegate handler in handlers.GetInvocationList())
                {
                    if (handler == null)
                        continue;
                    var dele = (KeyEventHandler)Delegate.CreateDelegate(typeof(KeyEventHandler), editor, handler.Method, true);
                    editor.KeyDown -= dele;
                }
            }

            Instances.DocumentEditor.KeyDown += new KeyEventHandler(KnifeInteraction.UnsheatheKnife);

            if (handlers != null)
            {
                foreach (Delegate handler in handlers.GetInvocationList())
                {
                    if (handler == null)
                        continue;
                    var dele = (KeyEventHandler)Delegate.CreateDelegate(typeof(KeyEventHandler), editor, handler.Method, true);
                    editor.KeyDown += dele;
                }
            }
        }

        private void AppendAutoWireInteraction(GH_Canvas canvas)
        {
            Instances.CanvasCreated -= AppendAutoWireInteraction;
             
            Instances.DocumentEditor.KeyDown += new KeyEventHandler(AutoWireInteraction.StartAutoWireInteraction);
        }
    }
}
