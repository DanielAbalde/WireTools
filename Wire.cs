using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WireTools
{
    public class RGH_Wire
    {
        #region Fields
        protected IGH_Param source;
        protected IGH_Param target;
        protected Curve bezier;
        protected Color colour;
        protected PointF p0;
        protected PointF p1;
        protected PointF p2;
        protected PointF p3;
        #endregion

        #region Properties
        public IGH_Param Source { get { return source; } }
        public IGH_Param Target { get { return target; } }
        public Curve Bezier { get { return bezier; } }
        public PointF Input { get { return target.Attributes.InputGrip; } }
        public PointF Output { get { return source.Attributes.OutputGrip; } }
        public Color Colour { get { return colour; } }
        #endregion

        #region Constructors
        public RGH_Wire(IGH_Param src, IGH_Param tgt)
        {
            source = src;
            target = tgt;

            Point3d pt0 = ToPoint3d(Output);
            Point3d pt3 = ToPoint3d(Input);
            double TorqueTension = 0.75 * Math.Abs(pt3.Y - pt0.Y);
            double SpanTension = Math.Abs(0.5 * (pt3.X - pt0.X));
            if (SpanTension < TorqueTension) SpanTension = TorqueTension;
            Point3d pt1 = pt0; pt1.X += SpanTension;
            Point3d pt2 = pt3; pt2.X -= SpanTension;
            p0 = Output;
            p1 = ToPointF(pt1);
            p2 = ToPointF(pt2);
            p3 = Input;

            bezier = new BezierCurve(new Point3d[] { pt0, pt1, pt2, pt3 }).ToNurbsCurve().ToNurbsCurve();
            bezier.Domain = new Interval(0, 1);

            colour = Color.FromArgb(255, 255, 255, 255);
        }
        #endregion

        #region Find
        public static IEnumerable<RGH_Wire> FindAllVisibleWires(Grasshopper.GUI.Canvas.GH_Canvas Canvas)
        {
            foreach (IGH_DocumentObject obj in Canvas.Document.Objects)
            {
                if (obj is IGH_Component)
                {
                    IGH_Component comp = obj as IGH_Component;
                    foreach (IGH_Param target in comp.Params.Input)
                    {
                        foreach (IGH_Param source in target.Sources)
                        {
                            RGH_Wire wire = new RGH_Wire(source, target);
                            if (wire.IsVisible()) yield return wire;
                        }
                    }
                }
                else if (obj is IGH_Param)
                {
                    IGH_Param target = obj as IGH_Param;
                    foreach (IGH_Param source in target.Sources)
                    {
                        RGH_Wire wire = new RGH_Wire(source, target);
                        if (wire.IsVisible()) yield return wire;
                    }
                }
            }
        }
        public static bool FindWire(Grasshopper.GUI.Canvas.GH_Canvas Canvas, PointF pt, out RGH_Wire Wire)
        {
            Wire = null;
            return FindWire(Canvas, pt, 5, out Wire);
        }
        public static bool FindWire(Grasshopper.GUI.Canvas.GH_Canvas Canvas, PointF pt, float Radius, out RGH_Wire Wire)
        {
            Wire = null;
            foreach (IGH_DocumentObject obj in Canvas.Document.Objects)
            {
                if (obj is IGH_Component)
                {
                    IGH_Component comp = obj as IGH_Component;
                    foreach (IGH_Param target in comp.Params.Input)
                    {
                        foreach (IGH_Param source in target.Sources)
                        {
                            RGH_Wire wire = new RGH_Wire(source, target);
                            if (wire.IsVisible() && wire.IsTouching(pt, Radius))
                            {
                                Wire = wire;
                                return true;
                            }
                        }
                    }
                }
                else if (obj is IGH_Param)
                {
                    IGH_Param target = obj as IGH_Param;
                    foreach (IGH_Param source in target.Sources)
                    {
                        RGH_Wire wire = new RGH_Wire(source, target);
                        if (wire.IsVisible() && wire.IsTouching(pt, Radius))
                        {
                            Wire = wire;
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        public static bool FindWire(Grasshopper.GUI.Canvas.GH_Canvas Canvas, RectangleF Rec, out RGH_Wire Wire)
        {
            Wire = null;
            foreach (IGH_DocumentObject obj in Canvas.Document.Objects)
            {
                if (obj is IGH_Component)
                {
                    IGH_Component comp = obj as IGH_Component;
                    foreach (IGH_Param target in comp.Params.Input)
                    {
                        foreach (IGH_Param source in target.Sources)
                        {
                            RGH_Wire wire = new RGH_Wire(source, target);
                            if (wire.IsVisible() && wire.IsTouching(Rec))
                            {
                                Wire = wire;
                                return true;
                            }
                        }
                    }
                }
                else if (obj is IGH_Param)
                {
                    IGH_Param target = obj as IGH_Param;
                    foreach (IGH_Param source in target.Sources)
                    {
                        RGH_Wire wire = new RGH_Wire(source, target);
                        if (wire.IsVisible() && wire.IsTouching(Rec))
                        {
                            Wire = wire;
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        #endregion

        #region Methods
        public PointF PointAt(float t)
        {
            return Grasshopper.GUI.GH_BezierSolver.PointAt(ref p0, ref p1, ref p2, ref p3, t);
        }
        public SizeF TangentAt(float t)
        {
            return Grasshopper.GUI.GH_BezierSolver.TangentAt(ref p0, ref p1, ref p2, ref p3, t);
        }
        public SizeF NormalAt(float t)
        {
            SizeF tan = TangentAt(t);
            float cx = tan.Height;
            float cy = -tan.Width;
            float len = (float)Math.Sqrt(cx * cx + cy * cy);
            cx /= len;
            cy /= len;
            return new SizeF(cx, cy);
        }

        public PointF[] Divide(int Count)
        {
            return (from t in bezier.DivideByCount(Count, true) select PointAt((float)t)).ToArray();
        }

        public double DistanceTo(Point3d pt)
        {
            bezier.ClosestPoint(pt, out double t);
            return pt.DistanceTo(bezier.PointAt(t));
        }

        public double DistanceTo(PointF pt)
        {
            return DistanceTo(ToPoint3d(pt));
        }

        public bool IsTouching(PointF pt, double sep)
        {
            return IsTouching(ToPoint3d(pt), sep);
        }

        public bool IsTouching(Point3d pt, double sep)
        {
            bezier.ClosestPoint(pt, out double t);
            return pt.DistanceTo(bezier.PointAt(t)) < sep;
        }

        public bool IsTouching(Curve crv)
        {
            if (!bezier.IsValid || !crv.IsValid) return false;
            return Curve.GetDistancesBetweenCurves(bezier, crv, 1,
                out double maxDist, out double maxA, out double maxB,
                out double minDist, out double minA, out double minB) && minDist < 5;
        }

        public bool IsTouching(RectangleF rec)
        {
            RectangleF bb = GetBoundingBox();
            if (!bb.Contains(rec) && !bb.IntersectsWith(rec)) return false;
            System.Drawing.Drawing2D.GraphicsPath path = GetPath();

            Region reg = new Region(path);
            return reg.IsVisible(rec);
        }

        public RectangleF GetBoundingBox()
        {
            return new RectangleF(Math.Min(p0.X, p3.X), Math.Min(p0.Y, p3.Y), Math.Abs(p3.X - p0.X), Math.Abs(p3.Y - p0.Y));

            /*
            float x0 = float.MaxValue;
            float y0 = float.MaxValue;
            float x1 = float.MinValue;
            float y1 = float.MinValue;
            PointF[] pts = new PointF[] { p0,p1,p2,p3};
            for (int i = 0; i < 4; i++) {
                PointF p = pts[i];
                if(p.X > x0)
            }
            RectangleF bb = Rectangle.Empty;
            bb.
            return bb;*/
        }
        public bool IsVisible()
        {
            PointF a = Output;
            PointF b = Input;
            if (System.Math.Abs(a.X - b.X) < 2f && System.Math.Abs(a.Y - b.Y) < 2f)
            {
                return false;
            }
            float dx = System.Math.Abs(b.X - a.X);
            float dy = System.Math.Abs(b.Y - a.Y);
            float dd = System.Math.Max(dx, dy) * Grasshopper.Instances.ActiveCanvas.Viewport.Zoom;
            if (dd < 8f)
            {
                return false;
            }
            System.Drawing.RectangleF rec = Grasshopper.Instances.ActiveCanvas.Viewport.VisibleRegion;
            rec.Inflate(100f, 10f);
            return (a.Y >= rec.Top - 10f || b.Y >= rec.Top - 10f) && (a.Y <= rec.Bottom + 10f || b.Y <= rec.Bottom + 10f) && (a.X >= rec.Left - 100f || b.X >= rec.Left - 100f) && (a.X <= rec.Right + 100f || b.X <= rec.Right + 100f);

        }

        public Grasshopper.GUI.Canvas.GH_WireType GetWireType()
        {

            if (source == null)
            {
                return Grasshopper.GUI.Canvas.GH_WireType.@null;
            }
            switch (source.VolatileData.PathCount)
            {
                case 0:
                    return Grasshopper.GUI.Canvas.GH_WireType.@null;
                case 1:
                    if (source.VolatileData.get_Branch(0).Count == 0)
                    {
                        return Grasshopper.GUI.Canvas.GH_WireType.@null;
                    }
                    if (source.VolatileData.get_Branch(0).Count > 1)
                    {
                        return Grasshopper.GUI.Canvas.GH_WireType.list;
                    }
                    if (source.VolatileData.get_Branch(0)[0] == null)
                    {
                        return Grasshopper.GUI.Canvas.GH_WireType.@null;
                    }
                    else
                    {
                        return Grasshopper.GUI.Canvas.GH_WireType.item;
                    }
                default:
                    return Grasshopper.GUI.Canvas.GH_WireType.tree;
            }
        }

        public static Point3d ToPoint3d(PointF pt)
        {
            return new Point3d(pt.X, pt.Y, 0);
        }

        public static PointF ToPointF(Point3d pt)
        {
            return new PointF((float)pt.X, (float)pt.Y);
        }
        #endregion

        #region Draw

        public virtual void Draw(Graphics G, float focus)
        {
            int cnt = (int)Math.Max(30, bezier.GetLength() / 1.5);

            float width = 1f;

            switch (GetWireType())
            {
                case Grasshopper.GUI.Canvas.GH_WireType.@null:
                    return;
                case Grasshopper.GUI.Canvas.GH_WireType.item:
                    width = 3f;
                    break;
                case Grasshopper.GUI.Canvas.GH_WireType.list:
                    width = 1f;
                    break;
                case Grasshopper.GUI.Canvas.GH_WireType.tree:
                    width = 1.5f;
                    break;
            }

            float wid2 = width / 2f;
            for (int i = 0; i < cnt; i++)
            {
                float t0 = (float)i / cnt;
                float r0 = Factor(t0 + focus);
                float w20 = wid2 * (1 - r0);
                PointF pt0 = PointAt(t0);
                SizeF n0 = NormalAt(t0);
                PointF p0a = new PointF(pt0.X + n0.Width * w20, pt0.Y + n0.Height * w20);
                PointF p0b = new PointF(pt0.X - n0.Width * w20, pt0.Y - n0.Height * w20);
                float t1 = ((float)(i + 1) / cnt);
                float r1 = Factor(t1 + focus);
                float w21 = wid2 * (1 - r1);
                PointF pt1 = PointAt(t1);
                SizeF n1 = NormalAt(t1);
                PointF p1a = new PointF(pt1.X + n1.Width * w21, pt1.Y + n1.Height * w21);
                PointF p1b = new PointF(pt1.X - n1.Width * w21, pt1.Y - n1.Height * w21);
                Color col = Grasshopper.GUI.GH_GraphicsUtil.BlendColour(colour, Color.Transparent, (r0 + r1) / 2);
                Brush brh = new SolidBrush(col);
                G.FillPolygon(brh, new PointF[] { p0a, p0b, p1b, p1a });
                brh.Dispose();

            }
        }

        public virtual void DrawInfo(Graphics G)
        {

        }

        public virtual float Factor(float focus)
        {
            return (float)Math.Exp(-Math.Pow(Math.Cos(focus), 100) / 0.5);
        }

        public System.Drawing.Drawing2D.GraphicsPath GetPath()
        {
            return Grasshopper.GUI.Canvas.GH_Painter.ConnectionPath(
              Output, Input,
              Grasshopper.GUI.Canvas.GH_WireDirection.right, Grasshopper.GUI.Canvas.GH_WireDirection.left);
        }

        public System.Drawing.Drawing2D.GraphicsPath GetPath(float width)
        {
            int cnt = 50;
            double cnt1 = cnt - 1;
            float w2 = width / 2f;
            int cnt2 = cnt * 2;
            PointF[] pts = new PointF[cnt2];
            for (int i = 0; i < cnt; i++)
            {
                float t = (float)(i / cnt1);
                PointF p = PointAt(t);
                SizeF n = NormalAt(t);
                pts[i] = new PointF(p.X + n.Width * w2, p.Y + n.Height * w2);
                pts[cnt2 - 1 - i] = new PointF(p.X - n.Width * w2, p.Y - n.Height * w2);
            }
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddClosedCurve(pts);

            return path;
        }
        public System.Drawing.Drawing2D.GraphicsPath GetPath(float width, RectangleF RI, RectangleF RO)
        {
            try
            {
                double wid2 = width / 2;
                Curve b0 = bezier.Offset(Plane.WorldXY, wid2, 0.01, CurveOffsetCornerStyle.Sharp)[0];
                Curve b1 = bezier.Offset(Plane.WorldXY, -wid2, 0.01, CurveOffsetCornerStyle.Sharp)[0];
                Curve b = Curve.JoinCurves(new Curve[]{b0,b1,
          new Line(b0.PointAtStart, b1.PointAtStart).ToNurbsCurve(),
          new Line(b0.PointAtEnd, b1.PointAtEnd).ToNurbsCurve()})[0];
                Curve r0 = new Rectangle3d(Plane.WorldXY, new Point3d(RI.Left, RI.Top, 0), new Point3d(RI.Right, RI.Bottom, 0)).ToNurbsCurve();
                Curve r1 = new Rectangle3d(Plane.WorldXY, new Point3d(RO.Left, RO.Top, 0), new Point3d(RO.Right, RO.Bottom, 0)).ToNurbsCurve();
                Curve reg = Curve.CreateBooleanUnion(new Curve[] { b, r0, r1 })[0];
                PolylineCurve plc = reg.ToPolyline(0, 1, 0.1, 0, 0, 0, 0, 0, true);
                System.Drawing.Drawing2D.GraphicsPath gp = new System.Drawing.Drawing2D.GraphicsPath();
                PointF[] ptsf = new PointF[plc.PointCount];
                for (int i = 0; i < plc.PointCount; i++)
                {
                    Point3d pt = plc.Point(i);
                    ptsf[i] = new PointF((float)pt.X, (float)pt.Y);
                }
                gp.AddPolygon(ptsf);
                gp.CloseAllFigures();
                return gp;
            }
            catch (Exception e)
            {
                Rhino.RhinoApp.WriteLine(e.ToString());
                return null;
            }
        }
        #endregion

    }
}
