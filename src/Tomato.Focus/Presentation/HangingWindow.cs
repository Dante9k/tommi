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
        static readonly Brush turnShade = TurnShade();
        static readonly DrawingGroup stemJoin = StemJoinDrawing();
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
            dc.PushOpacity(motion.Opacity);
            Point root = motion.Root, stem = motion.Stem;
            double joinVisibility = 0;
            var budTip = new VineProjection.Section();
            if (motion.Branch > 0)
            {
                var vine = new VineProjection(root, stem, motion.Fruit.Width, motion.Load, motion.Angle, motion.Yaw, motion.Pitch);
                double t = motion.Branch;
                var tip = vine.Sample(t);
                budTip = tip;
                joinVisibility = motion.Contact * (1 - motion.Bud.Presence) * Math.Max(0, 1 - (tip.Center - stem).Length / (6 * motion.Fruit.Width / 220));
                var sections = new VineProjection.Section[65];
                Vector neck = tip.Tangent;
                if (neck.Length > .0001)
                    neck.Normalize();
                neck *= 43 * tip.Scale / (220.0 / 76) * (1 - .72 * motion.Bud.Merge) * motion.Bud.Presence;
                for (int i = 0; i < sections.Length; i++)
                {
                    sections[i] = vine.Sample(t * i / (sections.Length - 1));
                    // The shoot enters the back of the bud, not the side of its cheek.
                    double u = Math.Max(0, (i / (double)(sections.Length - 1) - .7) / .3);
                    sections[i].Center -= neck * (u * u * (3 - 2 * u));
                    sections[i].Tangent -= neck * (6 * u * (1 - u) / Math.Max(.0001, t * .3));
                }

                dc.PushOpacity(.55 + .45 * t);
                dc.DrawGeometry(bark.Brush, null, VineRibbon(sections, 4.2, 0));
                dc.DrawGeometry(green.Brush, null, VineRibbon(sections, 2.7, 0));
                dc.DrawGeometry(light.Brush, null, VineRibbon(sections, .65, -.7));
                if (t < 1)
                {
                    dc.DrawEllipse(bark.Brush, null, tip.Center, 2.1 * tip.Scale, 2.1 * tip.Scale);
                    dc.DrawEllipse(green.Brush, null, tip.Center, 1.35 * tip.Scale, 1.35 * tip.Scale);
                }

                DrawBranchLeaf(dc, vine, .42, t, -18, .82);
                DrawBranchLeaf(dc, vine, .70, t, 194, .56);
                dc.Pop();
                VineBud.Draw(dc, tip, motion.Bud, false);
            }

            dc.PushTransform(new RotateTransform(motion.Angle, stem.X, stem.Y));
            dc.PushTransform(new SkewTransform(motion.Yaw * -.18, 0, stem.X, stem.Y));
            dc.PushTransform(new ScaleTransform(Math.Cos(motion.Yaw * Math.PI / 180), Math.Cos(motion.Pitch * Math.PI / 180), stem.X, stem.Y));
            dc.DrawImage(art, motion.Fruit);
            if (motion.Editor > 0 && editor != null)
            {
                dc.PushOpacity(motion.Editor);
                dc.DrawImage(editor, motion.Fruit);
                dc.Pop();
            }

            // The editor snapshot contains the whole fruit too. Join after both images,
            // so its opaque cut face cannot hide the incoming shoot behind the stem.
            if (joinVisibility > 0)
            {
                dc.PushOpacity(joinVisibility);
                dc.PushTransform(new TranslateTransform(stem.X, stem.Y));
                Vector axis = TomatoAttachment.Incoming();
                dc.PushTransform(new RotateTransform(Math.Atan2(-axis.X, axis.Y) * 180 / Math.PI));
                dc.PushTransform(new ScaleTransform(motion.Fruit.Width / 220, motion.Fruit.Height / 220));
                dc.DrawDrawing(stemJoin);
                dc.Pop();
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }

            double distance = Math.Max(0, Math.Min(1, (220 / motion.Fruit.Width - 1) / (220.0 / HangingMotion.RestSize - 1)));
            dc.PushOpacityMask(silhouette);
            dc.PushOpacity(Math.Min(1, (Math.Abs(motion.Yaw) + Math.Abs(motion.Pitch)) / 50));
            dc.DrawRectangle(turnShade, null, motion.Fruit);
            dc.Pop();
            dc.PushOpacity(distance * .12);
            dc.DrawRectangle(distanceLight, null, motion.Fruit);
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
            if (motion.Branch > 0)
                VineBud.Draw(dc, budTip, motion.Bud, true);
            dc.Pop();
            dc.Pop();
        }

        static Brush TurnShade()
        {
            var shade = new LinearGradientBrush();
            shade.StartPoint = new Point(0, 0);
            shade.EndPoint = new Point(1, .8);
            shade.GradientStops.Add(new GradientStop(Color.FromArgb(28, 255, 245, 223), 0));
            shade.GradientStops.Add(new GradientStop(Colors.Transparent, .35));
            shade.GradientStops.Add(new GradientStop(Color.FromArgb(65, 65, 12, 8), 1));
            shade.Freeze();
            return shade;
        }

        static DrawingGroup StemJoinDrawing()
        {
            // A short, shaded continuation of the stalk, without a ring or an outline.
            // Fade into the vine above and the textured stem below the cut face.
            var fade = new LinearGradientBrush();
            fade.StartPoint = new Point(0, 0);
            fade.EndPoint = new Point(0, 1);
            fade.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            fade.GradientStops.Add(new GradientStop(Colors.White, .3));
            fade.GradientStops.Add(new GradientStop(Colors.White, .72));
            fade.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            var shade = new LinearGradientBrush();
            shade.StartPoint = new Point(0, 0);
            shade.EndPoint = new Point(1, 0);
            shade.GradientStops.Add(new GradientStop(Color.FromRgb(86, 126, 27), 0));
            shade.GradientStops.Add(new GradientStop(Color.FromRgb(156, 188, 66), .25));
            shade.GradientStops.Add(new GradientStop(Color.FromRgb(108, 153, 30), .5));
            shade.GradientStops.Add(new GradientStop(Color.FromRgb(61, 102, 14), 1));
            var shape = new StreamGeometry();
            using (var path = shape.Open())
            {
                path.BeginFigure(new Point(-4.62, -6), true, true);
                path.LineTo(new Point(4.62, -6), true, false);
                path.BezierTo(new Point(5.3, -2), new Point(5.8, 0), new Point(5.4, 6), true, false);
                path.LineTo(new Point(-5.4, 6), true, false);
                path.BezierTo(new Point(-5.8, 0), new Point(-5.3, -2), new Point(-4.62, -6), true, false);
            }

            var drawing = new DrawingGroup
            {
                OpacityMask = fade
            };
            using (var dc = drawing.Open())
                dc.DrawGeometry(shade, null, shape);
            drawing.Freeze();
            return drawing;
        }

        static StreamGeometry VineRibbon(VineProjection.Section[] sections, double width, double offset)
        {
            int segments = sections.Length - 1;
            var left = new Point[segments + 1];
            var right = new Point[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                var section = sections[i];
                Point point = section.Center;
                Vector tangent = section.Tangent;
                Vector normal = new Vector(-tangent.Y, tangent.X);
                if (normal.LengthSquared > .0001)
                    normal.Normalize();
                double scale = section.Scale * section.Width;
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

        static void DrawBranchLeaf(DrawingContext dc, VineProjection vine, double position, double growth, double angle, double scale)
        {
            double unfurl = Math.Max(0, Math.Min(1, (growth - position * .35) / .65));
            if (unfurl == 0)
                return;
            unfurl = unfurl * unfurl * (3 - 2 * unfurl);
            // Leaf nodes travel with the growing shoot instead of appearing at fixed screen coordinates.
            var section = vine.Sample(position * growth);
            Point node = section.Center;
            Vector tangent = section.Tangent;
            double bend = Math.Atan2(tangent.Y, tangent.X) * 180 / Math.PI - 90;
            dc.PushOpacity(unfurl);
            DrawLeaf(dc, node, angle + bend * .65, scale * section.Scale * (.35 + .65 * unfurl));
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
