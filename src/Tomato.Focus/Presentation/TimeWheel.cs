using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;

namespace Tomato
{
    public sealed class TimeWheel : FrameworkElement
    {
        public int Limit { get; private set; }
        public string Unit { get; private set; }

        public event EventHandler ValueChanged;
        public event EventHandler DetentCrossed;
        readonly DetentTracker detents = new DetentTracker();
        readonly FormattedText[] numerals;
        readonly Brush edgeFade;
        readonly Pen focusPen = new Pen(Art.Brush("#9AFFE8BC"), 1);
        double motionVelocity;
        double position, target, downY, downPosition, lastY, speed;
        long lastMove;
        bool dragging, animating;
        int lastValue;
        const double Row = 28;
        public TimeWheel(int limit, string unit)
        {
            Limit = limit;
            Unit = unit;
            Width = 48;
            Height = 92;
            numerals = new FormattedText[limit];
            var ink = Art.Brush("#FFF4DF");
            var typeface = new Typeface(new FontFamily("Segoe UI Variable Display"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            for (int i = 0; i < limit; i++)
                numerals[i] = new FormattedText(i.ToString("00"), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 24, ink, 1);
            var fade = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(180, 255, 255, 255), .18));
            fade.GradientStops.Add(new GradientStop(Colors.White, .36));
            fade.GradientStops.Add(new GradientStop(Colors.White, .64));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(180, 255, 255, 255), .82));
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            fade.Freeze();
            edgeFade = fade;
            focusPen.Freeze();
            Focusable = true;
            Cursor = Cursors.Hand;
            AutomationProperties.SetName(this, unit);
            ToolTip = "滚动或上下拖动调整" + unit + "；方向键微调，也可直接输入数字";
            Unloaded += delegate
            {
                Settle();
            };
            IsVisibleChanged += delegate
            {
                if (!IsVisible)
                    Settle();
            };
            IsEnabledChanged += delegate
            {
                if (!IsEnabled)
                    Settle();
            };
        }

        public int Value
        {
            get
            {
                return Wrap((int)Math.Round(target));
            }

            set
            {
                EndAnimation();
                dragging = false;
                ReleaseMouseCapture();
                position = target = Wrap(value);
                detents.Reset(Wrap((int)Math.Round(position)));
                motionVelocity = 0;
                Notify();
                InvalidateVisual();
            }
        }

        int Wrap(int n)
        {
            return ((n % Limit) + Limit) % Limit;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new WheelPeer(this);
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
            dc.PushOpacityMask(edgeFade);
            int nearest = (int)Math.Round(position);
            for (int i = nearest - 2; i <= nearest + 2; i++)
            {
                double angle = (i - position) * .68;
                if (Math.Abs(angle) >= Math.PI / 2)
                    continue;
                double depth = Math.Cos(angle);
                double centerY = 46 + Math.Sin(angle) * 44.5;
                var text = numerals[Wrap(i)];
                dc.PushOpacity(Math.Pow(depth, 2.5));
                dc.PushTransform(new ScaleTransform(.91 + .09 * depth, depth, ActualWidth / 2, centerY));
                dc.DrawText(text, new Point((ActualWidth - text.Width) / 2, centerY - text.Height / 2));
                dc.Pop();
                dc.Pop();
            }

            dc.Pop();
            if (IsKeyboardFocused)
                dc.DrawRoundedRectangle(null, focusPen, new Rect(2, 30, ActualWidth - 4, 30), 6, 6);
            dc.Pop();
            // Commit a single detent for the frame WPF draws, not for every input update.
            Feedback();
        }

        void Notify()
        {
            int v = Value;
            if (v == lastValue)
                return;
            lastValue = v;
            if (ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (!IsEnabled || e.Delta == 0)
                return;
            target += e.Delta > 0 ? -1 : 1;
            BeginAnimation();
            Notify();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            EndAnimation();
            motionVelocity = 0;
            dragging = true;
            downY = lastY = e.GetPosition(this).Y;
            downPosition = position;
            speed = 0;
            lastMove = Stopwatch.GetTimestamp();
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging)
                return;
            double y = e.GetPosition(this).Y;
            double dt = (Stopwatch.GetTimestamp() - lastMove) / (double)Stopwatch.Frequency;
            if (dt > .001)
                speed = .55 * speed + .45 * (lastY - y) / Row / dt;
            position = downPosition + (downY - y) / Row;
            target = position;
            Notify();
            lastY = y;
            lastMove = Stopwatch.GetTimestamp();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!dragging)
                return;
            dragging = false;
            ReleaseMouseCapture();
            double y = e.GetPosition(this).Y;
            if (Math.Abs(y - downY) < 4)
                target = Math.Round(position) + (y < 30 ? -1 : y > 61 ? 1 : 0);
            else
            {
                if ((Stopwatch.GetTimestamp() - lastMove) / (double)Stopwatch.Frequency > .12)
                    speed = 0;
                target = Math.Round(position + Math.Max(-8, Math.Min(8, speed * .15)));
                motionVelocity = Math.Max(-40, Math.Min(40, speed));
            }

            BeginAnimation();
            Notify();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            if (dragging)
            {
                dragging = false;
                target = Math.Round(position);
                BeginAnimation();
                Notify();
            }

            base.OnLostMouseCapture(e);
        }

        string digits = "";
        DateTime digitTime;
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                target += e.Key == Key.Up ? -1 : 1;
                BeginAnimation();
                Notify();
                e.Handled = true;
            }
            else if (e.Key == Key.Home)
            {
                SetFromInput(0);
                e.Handled = true;
            }
            else if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                int d = e.Key >= Key.NumPad0 ? (int)e.Key - (int)Key.NumPad0 : (int)e.Key - (int)Key.D0;
                if ((DateTime.UtcNow - digitTime).TotalSeconds > 1.2 || digits.Length == 2)
                    digits = "";
                digits += d.ToString();
                digitTime = DateTime.UtcNow;
                int number = int.Parse(digits);
                SetFromInput(number < Limit ? number : d);
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        public void Settle()
        {
            dragging = false;
            ReleaseMouseCapture();
            target = Math.Round(target);
            position = target;
            detents.Reset(Wrap((int)Math.Round(position)));
            motionVelocity = 0;
            EndAnimation();
            Notify();
            InvalidateVisual();
        }

        void SetFromInput(int value)
        {
            // Retain the last drawn index across input bursts before the next frame.
            EndAnimation();
            dragging = false;
            ReleaseMouseCapture();
            position = target = Wrap(value);
            motionVelocity = 0;
            Notify();
            InvalidateVisual();
        }

        void Feedback()
        {
            if (detents.Move(Wrap((int)Math.Round(position))))
                EmitDetent();
        }

        void EmitDetent()
        {
            if (!IsEnabled || !IsVisible)
                return;
            if (DetentCrossed != null)
                DetentCrossed(this, EventArgs.Empty);
        }

        void BeginAnimation()
        {
            if (!SystemParameters.ClientAreaAnimation)
            {
                position = target;
                motionVelocity = 0;
                EndAnimation();
                InvalidateVisual();
                return;
            }

            if (animating)
                return;
            animating = true;
            previous = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += Animate;
        }

        long previous;
        void Animate(object sender, EventArgs e)
        {
            if (dragging)
                return;
            long now = Stopwatch.GetTimestamp();
            double dt = Math.Min(.1, (now - previous) / (double)Stopwatch.Frequency);
            previous = now;
            WheelMotion.Advance(ref position, ref motionVelocity, target, dt);
            if (Math.Abs(target - position) < .0008 && Math.Abs(motionVelocity) < .02)
            {
                position = target;
                motionVelocity = 0;
                EndAnimation();
            }

            InvalidateVisual();
        }

        void EndAnimation()
        {
            CompositionTarget.Rendering -= Animate;
            animating = false;
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            InvalidateVisual();
            base.OnGotKeyboardFocus(e);
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            InvalidateVisual();
            base.OnLostKeyboardFocus(e);
        }

        sealed class WheelPeer : FrameworkElementAutomationPeer, IRangeValueProvider
        {
            readonly TimeWheel wheel;
            public WheelPeer(TimeWheel owner) : base(owner)
            {
                wheel = owner;
            }

            protected override string GetClassNameCore()
            {
                return "TimeWheel";
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Spinner;
            }

            public override object GetPattern(PatternInterface pattern)
            {
                return pattern == PatternInterface.RangeValue ? this : base.GetPattern(pattern);
            }

            public bool IsReadOnly
            {
                get
                {
                    return !wheel.IsEnabled;
                }
            }

            public double LargeChange
            {
                get
                {
                    return 5;
                }
            }

            public double SmallChange
            {
                get
                {
                    return 1;
                }
            }

            public double Maximum
            {
                get
                {
                    return wheel.Limit - 1;
                }
            }

            public double Minimum
            {
                get
                {
                    return 0;
                }
            }

            public double Value
            {
                get
                {
                    return wheel.Value;
                }
            }

            public void SetValue(double value)
            {
                if (!wheel.IsEnabled)
                    throw new InvalidOperationException("计时过程中不能编辑时间");
                if (value < 0 || value >= wheel.Limit)
                    throw new ArgumentOutOfRangeException("value");
                wheel.SetFromInput((int)value);
            }
        }
    }
}
