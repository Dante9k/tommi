using System;
using System.Windows;
using System.Windows.Media;

namespace Tomato
{
    // Vector artwork allows the jaws and cheeks to change shape instead of swapping sprites.
    public static class VineBud
    {
        static readonly Brush skin = Skin();
        static readonly Brush dark = Art.Brush("#284A23");
        static readonly Brush mouth = Art.Brush("#20351E");
        static readonly Brush cream = Art.Brush("#F7F2D6");
        static readonly Brush glint = Art.Brush("#E1F3AA");
        static readonly Brush lip = Art.Brush("#638E30");
        static readonly Pen brow = Stroke("#355620", 3.5);
        static readonly Pen lipEdge = Stroke("#446B25", 1.15);
        static readonly Geometry head = Shape("M -5,-48 C -17,-47 -22,-38 -21,-27 C -23,-14 -18,5 -11,9 C -5,12 6,12 12,8 C 21,1 23,-15 20,-29 C 20,-41 13,-47 5,-48 Z");
        static readonly Geometry leftEye = Shape("M -16,-31 Q -9,-30 -3,-26 L -4,-21 Q -11,-17 -15,-23 Z");
        static readonly Geometry rightEye = Shape("M 16,-31 Q 9,-30 3,-26 L 4,-21 Q 11,-17 15,-23 Z");
        public static void Draw(DrawingContext dc, VineProjection.Section tip, BudBite pose, bool front)
        {
            if (pose.Presence <= .0001)
                return;
            double scale = tip.Scale / (220.0 / 76);
            double angle = Math.Atan2(-tip.Tangent.X, tip.Tangent.Y) * 180 / Math.PI;
            dc.PushOpacity(pose.Presence);
            dc.PushTransform(new TranslateTransform(tip.Center.X, tip.Center.Y));
            dc.PushTransform(new RotateTransform(angle));
            dc.PushTransform(new ScaleTransform(scale * (1 - .73 * pose.Merge), scale * (1 - .72 * pose.Merge)));
            double opening = pose.Mouth;
            if (!front)
            {
                dc.DrawGeometry(skin, null, head);
                dc.PushOpacity(.3 * (1 - pose.Merge));
                dc.DrawEllipse(glint, null, new Point(-9, -37), 6, 3);
                dc.Pop();
                dc.PushOpacity(1 - pose.Merge);
                dc.DrawGeometry(cream, null, leftEye);
                dc.DrawGeometry(cream, null, rightEye);
                dc.DrawEllipse(dark, null, new Point(-6.4, -23.5), 2.6, 3.3);
                dc.DrawEllipse(dark, null, new Point(6.4, -23.5), 2.6, 3.3);
                dc.DrawEllipse(glint, null, new Point(-7.1, -24.8), .7, .8);
                dc.DrawEllipse(glint, null, new Point(5.7, -24.8), .7, .8);
                dc.DrawLine(brow, new Point(-17, -32), new Point(-2.8, -27));
                dc.DrawLine(brow, new Point(17, -32), new Point(2.8, -27));
                dc.DrawEllipse(lip, null, new Point(0, -17), 3.3, 2.2);
                dc.DrawEllipse(mouth, null, new Point(0, -2), 11.5, 2 + opening * 10);
                // Two blunt little teeth, kept behind the fruit so the stem enters the mouth.
                dc.DrawRoundedRectangle(cream, null, new Rect(-7.2, -3 - opening * 10, 4.1, 3.4), 1.1, 1.1);
                dc.DrawRoundedRectangle(cream, null, new Rect(3.1, -3 - opening * 10, 4.1, 3.4), 1.1, 1.1);
                dc.Pop();
            }
            else
            {
                // Only the near jaw covers the stem: the fruit is visibly inside the bite.
                double edge = -.8 + opening * 8.8;
                var jaw = new StreamGeometry();
                using (var path = jaw.Open())
                {
                    path.BeginFigure(new Point(-15.5, -4), true, true);
                    path.BezierTo(new Point(-11, edge + 1), new Point(-7, edge + 2), new Point(0, edge + 2), true, false);
                    path.BezierTo(new Point(7, edge + 2), new Point(11, edge + 1), new Point(15.5, -4), true, false);
                    path.BezierTo(new Point(17, 8), new Point(9, 13), new Point(0, 13), true, false);
                    path.BezierTo(new Point(-9, 13), new Point(-17, 8), new Point(-15.5, -4), true, false);
                }

                jaw.Freeze();
                dc.DrawGeometry(skin, null, jaw);
                var seam = new StreamGeometry();
                using (var path = seam.Open())
                {
                    path.BeginFigure(new Point(-13, edge - .5), false, false);
                    path.QuadraticBezierTo(new Point(0, edge + 4), new Point(13, edge - .5), true, false);
                }

                seam.Freeze();
                dc.PushOpacity(1 - pose.Merge);
                dc.DrawGeometry(null, lipEdge, seam);
                dc.Pop();
            }

            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        static Geometry Shape(string path)
        {
            var shape = Geometry.Parse(path);
            shape.Freeze();
            return shape;
        }

        static Brush Skin()
        {
            var brush = new RadialGradientBrush
            {
                Center = new Point(.32, .25),
                GradientOrigin = new Point(.25, .2),
                RadiusX = .85,
                RadiusY = .85
            };
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(185, 212, 99), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(115, 162, 51), .45));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(57, 103, 26), 1));
            brush.Freeze();
            return brush;
        }

        static Pen Stroke(string color, double width)
        {
            var pen = new Pen(Art.Brush(color), width)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            pen.Freeze();
            return pen;
        }
    }
}
