using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Automation;

namespace Tomato
{
    public sealed class TomatoWindow : Window
    {
        readonly AppController controller;
        readonly Canvas canvas;
        readonly Image fruit;
        readonly Viewbox viewbox;
        readonly ScaleTransform appearanceScale = new ScaleTransform(1, 1);
        int appearanceRevision;
        bool appearanceAnimating;
        readonly TimeWheel hours, minutes, seconds;
        readonly StackPanel picker;
        readonly StackPanel countdown;
        readonly RecessedGlyph[] countdownDigits = new RecessedGlyph[3];
        readonly RecessedGlyph[] countdownSeparators = new RecessedGlyph[2];
        readonly TextBlock countdownCaption;
        readonly TextBlock status;
        readonly Border glass;
        readonly ShakeDetector shake = new ShakeDetector();
        readonly System.Windows.Threading.DispatcherTimer statusTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        readonly Button menu;
        Point screenDown;
        bool pressed, moved;
        bool focusAppearance;
        double appearanceSize = 220;
        public bool AlarmMode { get; private set; }

        public TomatoWindow(AppController controller, BitmapSource art)
        {
            this.controller = controller;
            Title = Texts.Get("app.title");
            Width = 220;
            Height = 220;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            canvas = new Canvas
            {
                Width = 250,
                Height = 250,
                Background = Brushes.Transparent
            };
            viewbox = new Viewbox
            {
                Child = canvas,
                Stretch = Stretch.Uniform,
                RenderTransform = appearanceScale,
                RenderTransformOrigin = new Point(1, 0)
            };
            Content = viewbox;
            fruit = new Image
            {
                Name = "FruitArtwork",
                Source = art,
                Width = 250,
                Height = 250,
                IsHitTestVisible = false
            };
            canvas.Children.Add(fruit);
            ToolTip = Texts.Get("widget.tooltip");
            glass = new Border
            {
                Name = "TimeEditorFrame",
                Width = 174,
                Height = 92,
                CornerRadius = new CornerRadius(19),
                Background = new LinearGradientBrush(Color.FromArgb(145, 44, 8, 12), Color.FromArgb(90, 67, 10, 16), 90),
                BorderBrush = Art.Brush("#32FFD4AC"),
                BorderThickness = new Thickness(1)
            };
            Place(glass, 38, 100);
            var selection = new Border
            {
                Width = 158,
                Height = 30,
                CornerRadius = new CornerRadius(10),
                Background = new LinearGradientBrush(Color.FromArgb(28, 255, 242, 218), Color.FromArgb(8, 255, 242, 218), 90),
                BorderBrush = Art.Brush("#17FFE6C3"),
                BorderThickness = new Thickness(0, 1, 0, 1)
            };
            var inside = new Canvas();
            glass.Child = inside;
            inside.Children.Add(selection);
            Canvas.SetLeft(selection, 7);
            Canvas.SetTop(selection, 30);
            for (int i = 0; i < 2; i++)
            {
                var separator = Label(":", 19, "#BCFFE6CB", false);
                separator.Width = 12;
                inside.Children.Add(separator);
                Canvas.SetLeft(separator, 57 + i * 48);
                Canvas.SetTop(separator, 32);
            }

            picker = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            hours = new TimeWheel(24, Texts.Get("unit.hours"));
            minutes = new TimeWheel(60, Texts.Get("unit.minutes"));
            seconds = new TimeWheel(60, Texts.Get("unit.seconds"));
            picker.Children.Add(hours);
            picker.Children.Add(minutes);
            picker.Children.Add(seconds);
            Place(picker, 53, 100);
            countdownCaption = Label(Texts.Get("countdown.caption"), 10, "#E6F6D9BF", false);
            countdownCaption.Visibility = Visibility.Hidden;
            Place(countdownCaption, 38, 110, 174);
            countdown = new StackPanel
            {
                Name = "CountdownReadout",
                Orientation = Orientation.Horizontal,
                Visibility = Visibility.Hidden,
                IsHitTestVisible = false
            };
            for (int i = 0; i < countdownDigits.Length; i++)
            {
                if (i > 0)
                {
                    var separator = new RecessedGlyph
                    {
                        Text = ":",
                        FontSize = 32
                    };
                    separator.Width = 8;
                    separator.Margin = new Thickness(0, 4, 0, 0);
                    countdownSeparators[i - 1] = separator;
                    countdown.Children.Add(separator);
                }

                countdownDigits[i] = new RecessedGlyph
                {
                    Text = "00",
                    FontSize = 32,
                    Width = 42
                };
                countdown.Children.Add(countdownDigits[i]);
            }

            Place(countdown, 73, 124);
            foreach (var wheel in new[]
            {
                hours,
                minutes,
                seconds
            }

            )
            {
                wheel.ValueChanged += delegate
                {
                    if (controller != null)
                        controller.DurationChanged();
                };
                wheel.DetentCrossed += delegate
                {
                    if (controller != null)
                        controller.WheelTick();
                };
            }

            hours.IsVisibleChanged += delegate
            {
                if (controller != null)
                    controller.PrepareWheelFeedback();
            };
            hours.IsEnabledChanged += delegate
            {
                if (controller != null)
                    controller.PrepareWheelFeedback();
            };
            status = Label("", 10.5, "#FFF0D2", false);
            Place(status, 0, 201, 250);
            statusTimer.Tick += delegate
            {
                statusTimer.Stop();
                if (controller.Phase == TimerPhase.Editing)
                    status.Text = "";
            };
            menu = new Button
            {
                Name = "TomatoMenu",
                Content = "···",
                Width = 32,
                Height = 24,
                FontSize = 18,
                Foreground = Art.Brush("#FFF2D4"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = Texts.Get("menu.tooltip")
            };
            AutomationProperties.SetName(menu, Texts.Get("menu.name"));
            menu.Click += delegate
            {
                OpenMenu();
            };
            Place(menu, 185, 83);
            MouseLeftButtonDown += OnDown;
            MouseMove += OnMove;
            MouseLeftButtonUp += OnUp;
            LostMouseCapture += delegate
            {
                ResetGesture();
            };
            IsVisibleChanged += delegate
            {
                if (!IsVisible)
                {
                    if (appearanceAnimating)
                        FinishAppearance();
                    ResetGesture();
                    statusTimer.Stop();
                    if (controller != null)
                        controller.StopWheelFeedback();
                }
            };
            KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    controller.StopAlarm();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && !AlarmMode)
                {
                    controller.Start();
                    e.Handled = true;
                }
            };
            Closing += delegate (object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!controller.Exiting)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
            MouseRightButtonUp += delegate (object sender, MouseButtonEventArgs e)
            {
                controller.OpenSettings();
                e.Handled = true;
            };
        }

        static TextBlock Label(string text, double size, string color, bool bold)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                FontFamily = new FontFamily(Texts.FontFamily),
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = Art.Brush(color),
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false
            };
        }

        void Place(UIElement element, double x, double y, double width = 0)
        {
            canvas.Children.Add(element);
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            if (width > 0)
                ((FrameworkElement)element).Width = width;
        }

        public int Duration
        {
            get
            {
                return hours.Value * 3600 + minutes.Value * 60 + seconds.Value;
            }

            set
            {
                hours.Value = value / 3600;
                minutes.Value = (value / 60) % 60;
                seconds.Value = value % 60;
            }
        }

        public void Settle()
        {
            hours.Settle();
            minutes.Settle();
            seconds.Settle();
        }

        public void SetAlarm(bool alarm)
        {
            // Restore about the current top-right anchor; the emitter follows the animated stem.
            AlarmMode = alarm;
            SetFocusAppearance(false);
            shake.Reset(0, 0);
            statusTimer.Stop();
            countdown.Visibility = Visibility.Hidden;
            countdownCaption.Visibility = Visibility.Hidden;
            picker.Visibility = alarm ? Visibility.Hidden : Visibility.Visible;
            glass.Visibility = alarm ? Visibility.Hidden : Visibility.Visible;
            menu.Visibility = Visibility.Visible;
            status.Text = alarm ? Texts.Get("alarm.dismiss") : "";
            status.Visibility = Visibility.Visible;
            Canvas.SetTop(status, alarm ? 191 : 201);
            if (alarm)
            {
                var title = Label(Texts.Get("alarm.title"), 25, "#FFF4DE", true);
                title.Name = "AlarmTitle";
                Place(title, 0, 137, 250);
                var subtitle = Label(Texts.Get("alarm.subtitle"), 11, "#FFDBBC", false);
                subtitle.Name = "AlarmSubtitle";
                Place(subtitle, 0, 176, 250);
            }
            else
            {
                for (int i = canvas.Children.Count - 1; i >= 0; i--)
                {
                    var fe = canvas.Children[i] as FrameworkElement;
                    if (fe != null && (fe.Name == "AlarmTitle" || fe.Name == "AlarmSubtitle"))
                        canvas.Children.RemoveAt(i);
                }
            }
        }

        public void Error(string text)
        {
            status.Text = text;
            statusTimer.Stop();
            statusTimer.Start();
        }

        public Point ThrowOrigin
        {
            get
            {
                return canvas.TranslatePoint(new Point(125, 55), this);
            }
        }

        void OnDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;
            var p = e.GetPosition(canvas);
            // Only the visible fruit is draggable; transparent desktop corners pass through.
            if (p.Y < 12 || p.Y > 239 || p.X < 13 || p.X > 237)
                return;
            if (e.ClickCount == 2)
            {
                pressed = false;
                ReleaseMouseCapture();
                if (AlarmMode)
                    controller.StopAlarm();
                else
                    controller.Start();
                e.Handled = true;
                return;
            }

            pressed = true;
            moved = false;
            screenDown = canvas.PointToScreen(p);
            var transform = PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice;
            var dip = transform.Transform(screenDown);
            shake.Reset(dip.X, dip.Y);
            CaptureMouse();
            e.Handled = true;
        }

        void OnMove(object sender, MouseEventArgs e)
        {
            if (!pressed || e.LeftButton != MouseButtonState.Pressed)
                return;
            var screen = PointToScreen(e.GetPosition(this));
            if (!moved && (screen - screenDown).Length < 6)
                return;
            if (!moved && AlarmMode)
            {
                controller.StopAlarmForDrag();
            }

            moved = true;
            var source = PresentationSource.FromVisual(this);
            var transform = source.CompositionTarget.TransformFromDevice;
            var delta = transform.Transform(screen - screenDown);
            Left += delta.X;
            Top += delta.Y;
            screenDown = screen;
            var dip = transform.Transform(screen);
            if (controller.Phase == TimerPhase.Running && shake.Move(dip.X, dip.Y, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency))
            {
                ResetGesture();
                controller.ClampWindow();
                controller.Cancel();
                Error(Texts.Get("timer.cancelled"));
            }
        }

        void OnUp(object sender, MouseButtonEventArgs e)
        {
            bool wasMoved = moved;
            pressed = false;
            ReleaseMouseCapture();
            shake.Reset(0, 0);
            if (wasMoved)
            {
                controller.ClampWindow();
                controller.Save();
            }

            e.Handled = true;
        }

        public void ShowCountdown(int remaining)
        {
            SetFocusAppearance(true);
            remaining = Math.Max(0, Math.Min(86399, remaining));
            if (countdown.Visibility != Visibility.Visible)
            {
                ResetGesture();
                statusTimer.Stop();
                status.Text = Texts.Get("timer.running");
            }

            picker.IsEnabled = false;
            picker.Visibility = Visibility.Hidden;
            glass.Visibility = Visibility.Hidden;
            menu.Visibility = Visibility.Hidden;
            countdown.Visibility = Visibility.Visible;
            countdownCaption.Visibility = Visibility.Hidden;
            status.Visibility = Visibility.Hidden;
            countdownDigits[0].Text = (remaining / 3600).ToString("00");
            countdownDigits[1].Text = (remaining / 60 % 60).ToString("00");
            countdownDigits[2].Text = (remaining % 60).ToString("00");
            bool showHours = remaining >= 3600;
            countdownDigits[0].Visibility = showHours ? Visibility.Visible : Visibility.Collapsed;
            countdownSeparators[0].Visibility = showHours ? Visibility.Visible : Visibility.Collapsed;
            foreach (var digit in countdownDigits)
            {
                digit.FontSize = showHours ? 30 : 40;
                digit.Width = showHours ? 36 : 48;
            }

            foreach (var separator in countdownSeparators)
                separator.FontSize = showHours ? 24 : 32;
            Canvas.SetLeft(countdown, showHours ? 63 : 73);
            AutomationProperties.SetName(countdown, Texts.Format("countdown.accessible", countdownDigits[0].Text, countdownDigits[1].Text, countdownDigits[2].Text));
        }

        // Persist the expanded position, so restoring a miniature never shifts it again.
        public double ExpandedLeft
        {
            get
            {
                return Left + Width - 250;
            }
        }

        void SetFocusAppearance(bool focused, bool animate = true)
        {
            Topmost = true;
            double size = focused ? 125 : AlarmMode ? 250 : 220;
            if (focusAppearance == focused && appearanceSize == size && animate)
                return;
            focusAppearance = focused;
            appearanceSize = size;
            bool motion = animate && IsVisible && SystemParameters.ClientAreaAnimation;
            double visualWidth = Width * appearanceScale.ScaleX;
            double visualHeight = Height * appearanceScale.ScaleY;
            double opacity = fruit.Opacity;
            int revision = ++appearanceRevision;
            appearanceScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            appearanceScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            fruit.BeginAnimation(UIElement.OpacityProperty, null);
            if (!motion)
            {
                FinishAppearance();
                return;
            }

            // Keep native bounds still while the compositor scales the artwork about its top-right.
            // This avoids per-frame Win32 resize/layout rounding and also permits dragging mid-flight.
            SetAppearanceBounds(250);
            UpdateLayout();
            viewbox.Measure(new Size(Width, Height));
            viewbox.Arrange(new Rect(0, 0, Width, Height));
            appearanceScale.ScaleX = size / Width;
            appearanceScale.ScaleY = size / Height;
            fruit.Opacity = focused ? .32 : 1;
            appearanceAnimating = true;
            appearanceScale.BeginAnimation(ScaleTransform.ScaleXProperty, AppearanceAnimation(visualWidth / Width, size / Width));
            var vertical = AppearanceAnimation(visualHeight / Height, size / Height);
            vertical.Completed += delegate
            {
                if (revision == appearanceRevision)
                    FinishAppearance();
            };
            appearanceScale.BeginAnimation(ScaleTransform.ScaleYProperty, vertical);
            fruit.BeginAnimation(UIElement.OpacityProperty, AppearanceAnimation(opacity, fruit.Opacity));
        }

        void FinishAppearance()
        {
            ++appearanceRevision;
            appearanceAnimating = false;
            appearanceScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            appearanceScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            fruit.BeginAnimation(UIElement.OpacityProperty, null);
            appearanceScale.ScaleX = appearanceScale.ScaleY = 1;
            fruit.Opacity = focusAppearance ? .32 : 1;
            SetAppearanceBounds(appearanceSize);
            UpdateLayout();
            viewbox.Measure(new Size(Width, Height));
            viewbox.Arrange(new Rect(0, 0, Width, Height));
            if (controller != null && IsVisible && !AlarmMode)
            {
                controller.ClampWindow();
                controller.Save();
            }
        }

        void SetAppearanceBounds(double size)
        {
            double right = Left + Width;
            var source = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
            if (source != null && !double.IsNaN(right))
            {
                var matrix = source.CompositionTarget.TransformToDevice;
                var position = matrix.Transform(new Point(right - size, Top));
                var edge = matrix.Transform(new Point(right, Top + size));
                int x = (int)Math.Round(position.X), y = (int)Math.Round(position.Y);
                // Move and resize atomically; separate WPF setters can enqueue an old position.
                // Round screen edges, not width separately, so repeated transitions cannot lose the anchor.
                if (Native.SetWindowPos(source.Handle, IntPtr.Zero, x, y, (int)Math.Round(edge.X) - x, (int)Math.Round(edge.Y) - y, 0x0004 | 0x0010))
                    return;
            }

            Width = Height = size;
            if (!double.IsNaN(right))
                Left = right - size;
        }

        static DoubleAnimation AppearanceAnimation(double current, double target)
        {
            return new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(460))
            {
                EasingFunction = new QuarticEase
                {
                    EasingMode = EasingMode.EaseOut
                },
                FillBehavior = FillBehavior.HoldEnd
            };
        }

        void ResetGesture()
        {
            pressed = moved = false;
            shake.Reset(0, 0);
            if (IsMouseCaptured)
                ReleaseMouseCapture();
        }

        public void RefreshLanguage()
        {
            Title = Texts.Get("app.title");
            ToolTip = Texts.Get("widget.tooltip");
            hours.SetUnit(Texts.Get("unit.hours"));
            minutes.SetUnit(Texts.Get("unit.minutes"));
            seconds.SetUnit(Texts.Get("unit.seconds"));
            countdownCaption.Text = Texts.Get("countdown.caption");
            menu.ToolTip = Texts.Get("menu.tooltip");
            AutomationProperties.SetName(menu, Texts.Get("menu.name"));
            AutomationProperties.SetName(countdown, Texts.Format("countdown.accessible", countdownDigits[0].Text, countdownDigits[1].Text, countdownDigits[2].Text));
            status.Text = AlarmMode ? Texts.Get("alarm.dismiss") : controller.Phase == TimerPhase.Running ? Texts.Get("timer.running") : "";
            foreach (var child in canvas.Children)
            {
                var text = child as TextBlock;
                if (text == null)
                    continue;
                text.FontFamily = new FontFamily(Texts.FontFamily);
                if (text.Name == "AlarmTitle")
                    text.Text = Texts.Get("alarm.title");
                if (text.Name == "AlarmSubtitle")
                    text.Text = Texts.Get("alarm.subtitle");
            }
        }

        public void ShowEditor()
        {
            picker.IsEnabled = true;
            SetAlarm(false);
            if (controller != null)
                controller.PrepareWheelFeedback();
        }

        void OpenMenu()
        {
            controller.OpenSettings();
        }
    }
}
