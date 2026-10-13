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
        static readonly Pen bark = Pen("#36552C", 4.2);
        static readonly Pen green = Pen("#719A48", 2.7);
        static readonly Pen light = Pen("#B8D080", .7);
        static readonly Brush leaf = LeafBrush();
        public Point Origin { get; set; }

        public HangingSurface(HangingMotion motion, BitmapSource art, BitmapSource editor)
        {
            this.motion = motion;
            this.art = art;
            this.editor = editor;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.PushTransform(new TranslateTransform(-Origin.X, -Origin.Y));
            Point root = motion.Root, stem = motion.Stem;
            Vector span = stem - root;
            // The vine enters vertically through the top bezel, then bends toward the fruit.
            Point c1 = root + new Vector(0, Math.Max(16, span.Y * .28));
            Point c2 = stem - new Vector(span.X * .18, Math.Max(16, span.Y * .28));
            if (motion.Branch > 0)
            {
                // De Casteljau subdivision grows/retracts the branch along its curve, not by fading it.
                double t = motion.Branch;
                Point a = Lerp(root, c1, t), b = Lerp(c1, c2, t), c = Lerp(c2, stem, t);
                Point d = Lerp(a, b, t), e = Lerp(b, c, t), tip = Lerp(d, e, t);
                var curve = new StreamGeometry();
                using (var path = curve.Open())
                {
                    path.BeginFigure(root, false, false);
                    path.BezierTo(a, d, tip, true, false);
                }

                curve.Freeze();
                dc.DrawGeometry(null, bark, curve);
                dc.DrawGeometry(null, green, curve);
                dc.PushTransform(new TranslateTransform(0, -1));
                dc.DrawGeometry(null, light, curve);
                dc.Pop();
                if (t > .35)
                {
                    Point node = Curve(root, c1, c2, stem, .3);
                    dc.PushOpacity(Math.Min(1, (t - .35) / .4));
                    DrawLeaf(dc, node, 10, .9);
                    if (t > .62)
                        DrawLeaf(dc, Curve(root, c1, c2, stem, .6), 120, .7);
                    dc.Pop();
                }
            }

            dc.PushTransform(new RotateTransform(motion.Angle, stem.X, stem.Y));
            dc.DrawImage(art, motion.Fruit);
            if (motion.Editor > 0 && editor != null)
            {
                dc.PushOpacity(motion.Editor);
                dc.DrawImage(editor, motion.Fruit);
                dc.Pop();
            }

            dc.Pop();
            dc.Pop();
        }

        static void DrawLeaf(DrawingContext dc, Point node, double angle, double scale)
        {
            dc.PushTransform(new RotateTransform(angle, node.X, node.Y));
            dc.PushTransform(new ScaleTransform(scale, scale, node.X, node.Y));
            var shape = new StreamGeometry();
            using (var path = shape.Open())
            {
                path.BeginFigure(node, true, true);
                path.BezierTo(node + new Vector(3, -16), node + new Vector(22, -20), node + new Vector(31, -17), true, false);
                path.BezierTo(node + new Vector(26, -3), node + new Vector(9, 4), node, true, false);
            }

            shape.Freeze();
            dc.DrawGeometry(leaf, null, shape);
            dc.DrawLine(light, node, node + new Vector(24, -13));
            dc.Pop();
            dc.Pop();
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
