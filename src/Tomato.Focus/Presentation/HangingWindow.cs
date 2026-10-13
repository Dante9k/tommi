using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tomato
{
    // The native window is resized only at phase boundaries. Every animation frame is a drawing.
    public sealed class HangingWindow : Window
    {
        readonly HangingSurface surface;
        readonly Action detach, complete, settings;
        Point down;
        bool grabbed, subscribed, closed, ownerClosing;
        HwndSource source;
        public HangingMotion Motion { get; private set; }

        public static double Now
        {
            get
            {
                return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            }
        }

        public HangingWindow(HangingMotion motion, BitmapSource art, BitmapSource editor, Action detach, Action complete, Action settings)
        {
            Motion = motion;
            this.detach = detach;
            this.complete = complete;
            this.settings = settings;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            surface = new HangingSurface(motion, art, editor);
            Content = surface;
            Title = "Tommi";
            RefreshLanguage();
            SourceInitialized += delegate
            {
                source = (HwndSource)PresentationSource.FromVisual(this);
                SetInteractive(!Motion.Animating);
            };
            MouseLeftButtonDown += OnDown;
            MouseMove += OnMove;
            MouseLeftButtonUp += delegate
            {
                Release();
            };
            LostMouseCapture += delegate
            {
                Release();
            };
            MouseRightButtonUp += delegate
            {
                settings();
            };
            KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)
                    detach();
            };
            Closed += delegate
            {
                closed = true;
                grabbed = false;
                StopFrames();
                if (IsMouseCaptured)
                    ReleaseMouseCapture();
            };
            Closing += delegate (object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!ownerClosing)
                {
                    e.Cancel = true;
                    detach();
                }
            };
        }

        public void CloseForOwner()
        {
            ownerClosing = true;
            Close();
        }

        public void RefreshLanguage()
        {
            ToolTip = Texts.Get("idle.tooltip");
            System.Windows.Automation.AutomationProperties.SetName(this, Texts.Get("idle.tooltip"));
        }

        public void SetBounds(Rect bounds)
        {
            Left = Math.Floor(bounds.Left);
            Top = Math.Floor(bounds.Top);
            Width = Math.Ceiling(bounds.Right) - Left;
            Height = Math.Ceiling(bounds.Bottom) - Top;
            surface.Origin = new Point(Left, Top);
            surface.InvalidateVisual();
        }

        public void Animate()
        {
            if (closed || !Motion.Animating)
                return;
            SetInteractive(Motion.Phase == HangingPhase.Rebounding);
            if (!subscribed)
            {
                CompositionTarget.Rendering += OnFrame;
                subscribed = true;
            }
        }

        void SetInteractive(bool enabled)
        {
            if (source == null)
                return;
            int style = Native.GetWindowLong(source.Handle, -20);
            Native.SetWindowLong(source.Handle, -20, enabled ? style & ~0x20 : style | 0x20);
        }

        void OnFrame(object sender, EventArgs e)
        {
            Motion.Advance(Now);
            surface.InvalidateVisual();
            if (!Motion.Animating)
            {
                StopFrames();
                if (Motion.Phase == HangingPhase.Complete)
                    complete();
                else
                {
                    // Keep the pull corridor inside this compact window, never a desktop-size input layer.
                    Rect bounds = new Rect(Motion.Fruit.Left - 64, Motion.Root.Y - 20, 220, Math.Max(310, Motion.Fruit.Bottom - Motion.Root.Y + 170));
                    SetBounds(bounds);
                    SetInteractive(true);
                }
            }
        }

        void StopFrames()
        {
            if (subscribed)
                CompositionTarget.Rendering -= OnFrame;
            subscribed = false;
        }

        void OnDown(object sender, MouseButtonEventArgs e)
        {
            if (Motion.Phase != HangingPhase.Hanging && Motion.Phase != HangingPhase.Rebounding)
                return;
            Point point = e.GetPosition(surface) + (Vector)surface.Origin;
            Rect hit = Motion.Fruit;
            hit.Inflate(8, 8);
            if (!hit.Contains(point))
                return;
            Motion.Grab(Now);
            StopFrames();
            down = PointToScreen(e.GetPosition(this));
            grabbed = CaptureMouse();
            if (!grabbed)
            {
                Motion.Release(Now);
                Animate();
            }

            e.Handled = true;
        }

        void OnMove(object sender, MouseEventArgs e)
        {
            if (!grabbed)
                return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                Release();
                return;
            }

            Vector delta = PointToScreen(e.GetPosition(this)) - down;
            if (source != null)
                delta = source.CompositionTarget.TransformFromDevice.Transform(delta);
            bool ready = Motion.Pull(delta.X, delta.Y);
            surface.InvalidateVisual();
            if (ready)
            {
                grabbed = false;
                detach();
                ReleaseMouseCapture();
            }

            e.Handled = true;
        }

        void Release()
        {
            if (!grabbed)
                return;
            grabbed = false;
            Motion.Release(Now);
            if (IsMouseCaptured)
                ReleaseMouseCapture();
            if (!SystemParameters.ClientAreaAnimation)
            {
                Motion.Advance(Now + .72);
                surface.InvalidateVisual();
            }
            else
                Animate();
        }
    }

    public sealed class HangingSurface : FrameworkElement
    {
        readonly HangingMotion motion;
        readonly BitmapSource art, editor;
        readonly ImageBrush silhouette;
        static readonly Pen bark = Pen("#36552C", 4.2);
        static readonly Pen green = Pen("#719A48", 2.7);
        static readonly Pen light = Pen("#B8D080", .7);
        static readonly Brush leaf = LeafBrush();
        static readonly Brush leafFold = FrozenBrush("#4E7836");
        static readonly Pen vein = Pen("#BDD58C", .55);
        static readonly DrawingGroup leaflet = LeafDrawing();
        static readonly Brush distanceLight = FrozenBrush("#D6DDD2");
        public Point Origin { get; set; }

        public HangingSurface(HangingMotion motion, BitmapSource art, BitmapSource editor)
        {
            this.motion = motion;
            this.art = art;
            this.editor = editor;
            silhouette = new ImageBrush(art)
            {
                Stretch = Stretch.Fill
            };
            silhouette.Freeze();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.PushTransform(new TranslateTransform(-Origin.X, -Origin.Y));
            Point root = motion.Root, stem = motion.Stem;
            Vector span = stem - root;
            // A relaxed vine bows to one side; pulling takes up the slack gradually.
            // Both ends stay attached while the curve and its leaf tangents follow the load.
            double slack = 1 / (1 + Math.Max(0, span.Length - 54) / 90);
            Point c1 = root + new Vector(-32 * slack - motion.Load * 10, Math.Max(16, span.Y * .28) + motion.Load * 12);
            Point c2 = stem - new Vector(span.X * .18 + 23 * slack + motion.Load * 8, Math.Max(16, span.Y * .32) - motion.Load * 10);
            if (motion.Branch > 0)
            {
                // The far anchor stays thin; each nearer section shares the fruit's camera depth.
                double t = motion.Branch;
                double near = motion.Fruit.Width / 76;
                dc.DrawGeometry(bark.Brush, null, VineRibbon(root, c1, c2, stem, t, near, 4.2, 0));
                dc.DrawGeometry(green.Brush, null, VineRibbon(root, c1, c2, stem, t, near, 2.7, 0));
                dc.DrawGeometry(light.Brush, null, VineRibbon(root, c1, c2, stem, t, near, .65, -.7));
                if (t < 1)
                {
                    Point tip = Curve(root, c1, c2, stem, t);
                    double scale = PerspectiveAlong(t, near);
                    dc.DrawEllipse(bark.Brush, null, tip, 2.1 * scale, 2.1 * scale);
                    dc.DrawEllipse(green.Brush, null, tip, 1.35 * scale, 1.35 * scale);
                }

                DrawBranchLeaf(dc, root, c1, c2, stem, .42, t, -18, .82 * PerspectiveAlong(.42, near));
                DrawBranchLeaf(dc, root, c1, c2, stem, .70, t, 194, .56 * PerspectiveAlong(.70, near));
            }

            dc.PushTransform(new RotateTransform(motion.Angle, stem.X, stem.Y));
            dc.PushTransform(new ScaleTransform(Math.Cos(motion.Yaw * Math.PI / 180), 1, stem.X, stem.Y));
            dc.DrawImage(art, motion.Fruit);
            if (motion.Editor > 0 && editor != null)
            {
                dc.PushOpacity(motion.Editor);
                dc.DrawImage(editor, motion.Fruit);
                dc.Pop();
            }

            double distance = Math.Max(0, Math.Min(1, (220 / motion.Fruit.Width - 1) / (220.0 / 76 - 1)));
            dc.PushOpacityMask(silhouette);
            dc.PushOpacity(distance * .055);
            dc.DrawRectangle(distanceLight, null, motion.Fruit);
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        static double PerspectiveAlong(double t, double near)
        {
            return 1 / (1 - t + t / near);
        }

        static StreamGeometry VineRibbon(Point a, Point b, Point c, Point d, double growth, double near, double width, double offset)
        {
            const int segments = 32;
            var left = new Point[segments + 1];
            var right = new Point[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double t = growth * i / segments;
                Point point = Curve(a, b, c, d, t);
                Vector tangent = (b - a) * (3 * (1 - t) * (1 - t)) + (c - b) * (6 * (1 - t) * t) + (d - c) * (3 * t * t);
                Vector normal = new Vector(-tangent.Y, tangent.X);
                if (normal.LengthSquared > .0001)
                    normal.Normalize();
                double scale = PerspectiveAlong(t, near);
                left[i] = point + normal * ((offset - width / 2) * scale);
                right[i] = point + normal * ((offset + width / 2) * scale);
            }

            var ribbon = new StreamGeometry();
            using (var path = ribbon.Open())
            {
                path.BeginFigure(left[0], true, true);
                for (int i = 1; i <= segments; i++)
                    path.LineTo(left[i], true, false);
                for (int i = segments; i >= 0; i--)
                    path.LineTo(right[i], true, false);
            }

            ribbon.Freeze();
            return ribbon;
        }

        static void DrawBranchLeaf(DrawingContext dc, Point root, Point c1, Point c2, Point stem, double position, double growth, double angle, double scale)
        {
            double unfurl = Math.Max(0, Math.Min(1, (growth - position) / .18));
            if (unfurl == 0)
                return;
            unfurl = unfurl * unfurl * (3 - 2 * unfurl);
            Point node = Curve(root, c1, c2, stem, position);
            Vector tangent = (c1 - root) * (3 * (1 - position) * (1 - position)) + (c2 - c1) * (6 * (1 - position) * position) + (stem - c2) * (3 * position * position);
            double bend = Math.Atan2(tangent.Y, tangent.X) * 180 / Math.PI - 90;
            dc.PushOpacity(unfurl);
            DrawLeaf(dc, node, angle + bend * .65, scale * (.35 + .65 * unfurl));
            dc.Pop();
        }

        static void DrawLeaf(DrawingContext dc, Point node, double angle, double scale)
        {
            dc.PushTransform(new TranslateTransform(node.X, node.Y));
            dc.PushTransform(new RotateTransform(angle));
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawDrawing(leaflet);
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        static DrawingGroup LeafDrawing()
        {
            var drawing = new DrawingGroup();
            using (var dc = drawing.Open())
            {
                // A short curved petiole carries an asymmetric, softly lobed tomato leaflet.
                var petiole = new StreamGeometry();
                using (var path = petiole.Open())
                {
                    path.BeginFigure(new Point(0, 0), false, false);
                    path.QuadraticBezierTo(new Point(4, 1.5), new Point(9, -1), true, false);
                }

                petiole.Freeze();
                dc.DrawGeometry(null, green, petiole);
                dc.DrawGeometry(null, vein, petiole);
                var shape = new StreamGeometry();
                using (var path = shape.Open())
                {
                    path.BeginFigure(new Point(7, 0), true, true);
                    path.BezierTo(new Point(6, -5), new Point(9, -9), new Point(13, -9), true, false);
                    path.QuadraticBezierTo(new Point(14, -7), new Point(15, -8), true, false);
                    path.QuadraticBezierTo(new Point(18, -14), new Point(23, -13), true, false);
                    path.QuadraticBezierTo(new Point(23, -11), new Point(25, -11), true, false);
                    path.BezierTo(new Point(29, -13), new Point(33, -11), new Point(36, -10), true, false);
                    path.BezierTo(new Point(31, -8), new Point(32, -2), new Point(27, -2), true, false);
                    path.QuadraticBezierTo(new Point(25, -3), new Point(24, -2), true, false);
                    path.QuadraticBezierTo(new Point(21, 4), new Point(17, 2), true, false);
                    path.QuadraticBezierTo(new Point(16, 1), new Point(15, 2), true, false);
                    path.BezierTo(new Point(11, 4), new Point(8, 2), new Point(7, 0), true, false);
                }

                shape.Freeze();
                dc.DrawGeometry(leaf, null, shape);
                var fold = new StreamGeometry();
                using (var path = fold.Open())
                {
                    path.BeginFigure(new Point(7, 0), true, true);
                    path.BezierTo(new Point(18, -1), new Point(29, -8), new Point(36, -10), true, false);
                    path.BezierTo(new Point(28, -3), new Point(16, 5), new Point(7, 0), true, false);
                }

                fold.Freeze();
                dc.DrawGeometry(leafFold, null, fold);
                var veins = new StreamGeometry();
                using (var path = veins.Open())
                {
                    path.BeginFigure(new Point(7, 0), false, false);
                    path.BezierTo(new Point(17, -1), new Point(25, -6), new Point(33, -9), true, false);
                    path.BeginFigure(new Point(15, -2), false, false);
                    path.QuadraticBezierTo(new Point(12, -4), new Point(12, -7), true, false);
                    path.BeginFigure(new Point(22, -5), false, false);
                    path.QuadraticBezierTo(new Point(20, -7), new Point(21, -10), true, false);
                    path.BeginFigure(new Point(20, -4), false, false);
                    path.QuadraticBezierTo(new Point(21, -1), new Point(24, -1), true, false);
                }

                veins.Freeze();
                dc.DrawGeometry(null, vein, veins);
            }

            drawing.Freeze();
            return drawing;
        }

        static Point Lerp(Point a, Point b, double t)
        {
            return a + (b - a) * t;
        }

        static Brush LeafBrush()
        {
            var brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(166, 191, 102), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(99, 144, 59), .5));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(54, 92, 38), 1));
            brush.Freeze();
            return brush;
        }

        static Point Curve(Point a, Point b, Point c, Point d, double t)
        {
            return Lerp(Lerp(Lerp(a, b, t), Lerp(b, c, t), t), Lerp(Lerp(b, c, t), Lerp(c, d, t), t), t);
        }

        static Brush FrozenBrush(string color)
        {
            var value = (Brush)new BrushConverter().ConvertFromString(color);
            value.Freeze();
            return value;
        }

        static Pen Pen(string color, double width)
        {
            var value = new Pen(FrozenBrush(color), width)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            value.Freeze();
            return value;
        }
    }
}
