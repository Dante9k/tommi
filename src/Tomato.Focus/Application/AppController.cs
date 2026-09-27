using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Tomato
{
    public sealed class AppController
    {
        readonly Application app;
        readonly StateStore store;
        readonly Preferences preferences;
        readonly LoginStartup loginStartup;
        readonly Countdown clock = new Countdown();
        readonly WheelFeedback wheelFeedback = new WheelFeedback();
        readonly ThrowFeedback throwFeedback = new ThrowFeedback();
        readonly DispatcherTimer ticker = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        readonly BitmapSource art;
        readonly BitmapSource smallArt;
        Forms.NotifyIcon tray;
        ThrowWindow overlay;
        DateTime previewUntil;
        bool preview, initialized, hotkey;
        double editorLeft, editorTop;
        HwndSource source;
        SoundPlayer player;
        MemoryStream sound;
        public TomatoWindow Window { get; private set; }
        public SettingsWindow Settings { get; private set; }
        public bool Exiting { get; private set; }

        public bool Sound
        {
            get
            {
                return preferences.Sound;
            }
        }

        public bool WheelSound
        {
            get
            {
                return preferences.WheelSoundEnabled;
            }
        }

        public bool Haptics
        {
            get
            {
                return preferences.Haptics;
            }
        }

        public bool EffectsSound
        {
            get
            {
                return preferences.EffectsSound ?? preferences.Sound;
            }
        }

        public bool EffectsAudioReady
        {
            get
            {
                return throwFeedback.AudioReady;
            }
        }

        public bool EffectsAudioActive
        {
            get
            {
                return throwFeedback.IsActive;
            }
        }

        public void ToggleEffectsSound()
        {
            preferences.EffectsSound = !EffectsSound;
            if (IsThrowing && EffectsSound)
                throwFeedback.Start();
            else
                throwFeedback.Stop();
            Save();
        }

        public bool HapticsAvailable
        {
            get
            {
                return wheelFeedback.HapticsAvailable;
            }
        }

        public void ToggleWheelSound()
        {
            preferences.WheelSound = !WheelSound;
            PrepareWheelFeedback();
            Save();
        }

        public void ToggleHaptics()
        {
            preferences.Haptics = !Haptics;
            StopWheelFeedback();
            PrepareWheelFeedback();
            Save();
        }

        public bool WheelAudioReady
        {
            get
            {
                return wheelFeedback.AudioReady;
            }
        }

        public void PrepareWheelFeedback()
        {
            if (initialized)
                wheelFeedback.Prepare(WheelSound && Window.IsVisible && Phase == TimerPhase.Editing && !IsThrowing);
        }

        public void WheelTick()
        {
            if (initialized && Phase == TimerPhase.Editing && !IsThrowing && Window.IsVisible)
                wheelFeedback.Tick(WheelSound, Haptics);
        }

        public void StopWheelFeedback()
        {
            wheelFeedback.Stop();
        }

        public TimerPhase Phase
        {
            get
            {
                return clock.Phase;
            }
        }

        public bool IsThrowing
        {
            get
            {
                return overlay != null;
            }
        }

        public ThrowSurface Surface
        {
            get
            {
                return overlay == null ? null : overlay.Surface;
            }
        }

        public bool LaunchAtLogin
        {
            get
            {
                return loginStartup != null && loginStartup.Configured;
            }
        }

        public bool StartupAvailable
        {
            get
            {
                return loginStartup != null;
            }
        }

        public string StartupStatus
        {
            get
            {
                return loginStartup != null && loginStartup.Error != null ? loginStartup.Error : "Windows“启动应用”的设置也会生效。";
            }
        }

        public void ToggleLaunchAtLogin()
        {
            if (loginStartup != null)
            {
                loginStartup.Refresh();
                loginStartup.SetEnabled(!loginStartup.Configured);
                Save();
            }
        }

        public AppController(Application app, string dataPath, IStartupRegistration startupRegistration = null)
        {
            this.app = app;
            store = new StateStore(dataPath);
            preferences = store.Read();
            if (startupRegistration != null)
                loginStartup = new LoginStartup(preferences, startupRegistration, System.Reflection.Assembly.GetExecutingAssembly().Location);
            // Resolve the legacy sound preference once, before independently toggling either sound.
            preferences.WheelSound = preferences.WheelSoundEnabled;
            preferences.EffectsSound = EffectsSound;
            art = Art.TomatoImage(880);
            smallArt = Art.TomatoImage(192);
            Window = new TomatoWindow(this, art);
            Window.Duration = Math.Max(1, Math.Min(86399, preferences.Seconds));
            Window.Left = double.IsNaN(preferences.Left) ? SystemParameters.WorkArea.Right - Window.Width - 50 : preferences.Left + 250 - Window.Width;
            Window.Top = double.IsNaN(preferences.Top) ? SystemParameters.WorkArea.Top + 90 : preferences.Top;
            ticker.Tick += Tick;
            initialized = true;
        }

        public void Launch(bool restore)
        {
            Window.Show();
            ClampWindow();
            CreateTray();
            source = HwndSource.FromHwnd(new WindowInteropHelper(Window).Handle);
            source.AddHook(Hook);
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
            ticker.Start();
            if (restore && preferences.Active && preferences.DeadlineTicks > 0 && preferences.DeadlineTicks < DateTime.MaxValue.Ticks)
            {
                clock.Restore(preferences.Seconds, new DateTime(preferences.DeadlineTicks, DateTimeKind.Utc), DateTime.UtcNow);
                if (clock.Phase == TimerPhase.Ringing)
                    BeginAlarm(false);
                else
                    Window.ShowCountdown(clock.Remaining(DateTime.UtcNow));
            }

            if (loginStartup != null)
            {
                loginStartup.Initialize();
                Save();
            }

            PrepareWheelFeedback();
        }

        public void DurationChanged()
        {
            if (initialized && clock.Phase == TimerPhase.Editing && !preview)
            {
                preferences.Seconds = Window.Duration;
            }
        }

        public void Preset(int seconds)
        {
            Cancel();
            Window.Duration = seconds;
            Save();
        }

        public void ToggleSound()
        {
            preferences.Sound = !preferences.Sound;
            Save();
        }

        public void Start()
        {
            StopWheelFeedback();
            if (clock.Phase == TimerPhase.Running)
            {
                Show();
                return;
            }

            if (IsThrowing)
            {
                StopAlarm();
                return;
            }

            Window.Settle();
            if (Window.Duration == 0)
            {
                PrepareWheelFeedback();
                Window.Error("先设置一点专注时间");
                return;
            }

            clock.Start(Window.Duration, DateTime.UtcNow);
            preferences.Seconds = Window.Duration;
            Save();
            Window.ShowCountdown(clock.Remaining(DateTime.UtcNow));
            Window.Show();
            UpdateTray();
        }

        void Tick(object sender, EventArgs e)
        {
            if (preview && DateTime.UtcNow >= previewUntil)
            {
                StopAlarm();
                return;
            }

            if (clock.Tick(DateTime.UtcNow))
                BeginAlarm(false);
            if (clock.Phase == TimerPhase.Running)
            {
                UpdateTray();
                if (Window.IsVisible)
                    Window.ShowCountdown(clock.Remaining(DateTime.UtcNow));
            }
        }

        static string Format(int seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss");
        }

        public void Preview()
        {
            if (clock.Phase == TimerPhase.Running)
            {
                Window.Error("请先取消当前计时，再预览");
                return;
            }

            if (IsThrowing)
                return;
            previewUntil = DateTime.UtcNow.AddSeconds(8);
            BeginAlarm(true);
        }

        void BeginAlarm(bool isPreview)
        {
            if (overlay != null)
                return;
            Window.Settle();
            StopWheelFeedback();
            preview = isPreview;
            editorLeft = Window.ExpandedLeft;
            editorTop = Window.Top;
            var matrix = source.CompositionTarget.TransformFromDevice;
            Window.SetAlarm(true);
            var desktop = Forms.SystemInformation.VirtualScreen;
            var p = matrix.Transform(new Point(desktop.Left, desktop.Top));
            var size = matrix.Transform(new Vector(desktop.Width, desktop.Height));
            var bounds = new Rect(p.X, p.Y, size.X, size.Y);
            Point stem = Window.ThrowOrigin;
            Point origin = new Point(Window.Left + stem.X - bounds.Left, Window.Top + stem.Y - bounds.Top);
            overlay = new ThrowWindow(smallArt, bounds, origin);
            overlay.Surface.OriginProvider = delegate
            {
                // Convert through physical screen coordinates, including the current restore scale.
                return overlay.PointFromScreen(Window.PointToScreen(Window.ThrowOrigin));
            };
            overlay.Surface.SoundCue += OnFlightSound;
            if (EffectsSound)
                throwFeedback.Start();
            overlay.Show();
            Window.Show();
            Window.Topmost = false;
            Window.Topmost = true;
            Window.Activate();
            hotkey = Native.RegisterHotKey(source.Handle, 91, 0, 0x1B);
            if (Sound && !isPreview)
                PlayChime();
            Save();
            UpdateTray();
        }

        public void StopAlarm()
        {
            StopAlarmCore(true);
        }

        void OnFlightSound(object sender, FlightSoundEventArgs e)
        {
            if (EffectsSound && IsThrowing)
                throwFeedback.Play(e);
        }

        public void StopAlarmForDrag()
        {
            StopAlarmCore(false);
        }

        void StopAlarmCore(bool restorePosition)
        {
            if (!IsThrowing)
                return;
            overlay.Surface.SoundCue -= OnFlightSound;
            throwFeedback.Stop();
            overlay.Close();
            overlay = null;
            preview = false;
            if (hotkey)
            {
                Native.UnregisterHotKey(source.Handle, 91);
                hotkey = false;
            }

            if (player != null)
                player.Stop();
            clock.Stop();
            Window.ShowEditor();
            if (restorePosition)
            {
                Window.Left = editorLeft + 250 - Window.Width;
                Window.Top = editorTop;
                ClampWindow();
            }

            Save();
            UpdateTray();
        }

        public void Cancel()
        {
            Window.Settle();
            StopWheelFeedback();
            StopAlarm();
            clock.Stop();
            Window.ShowEditor();
            Window.Show();
            Window.Activate();
            Save();
            UpdateTray();
        }

        public void Show()
        {
            if (clock.Phase == TimerPhase.Running)
                Window.ShowCountdown(clock.Remaining(DateTime.UtcNow));
            Window.Show();
            ClampWindow();
            Window.Activate();
            PrepareWheelFeedback();
        }

        public void ClampWindow()
        {
            var hwnd = new WindowInteropHelper(Window).Handle;
            double scale = 1;
            var ps = PresentationSource.FromVisual(Window);
            if (ps != null)
                scale = ps.CompositionTarget.TransformFromDevice.M11;
            var screen = hwnd == IntPtr.Zero ? Forms.Screen.PrimaryScreen : Forms.Screen.FromHandle(hwnd);
            var area = screen.WorkingArea;
            Window.Left = Math.Max(area.Left * scale, Math.Min(Window.Left, area.Right * scale - Window.Width));
            Window.Top = Math.Max(area.Top * scale, Math.Min(Window.Top, area.Bottom * scale - Window.Height));
        }

        public void Save()
        {
            if (!initialized)
                return;
            if (clock.Phase == TimerPhase.Editing)
                preferences.Seconds = Window.Duration;
            preferences.Active = clock.Phase != TimerPhase.Editing;
            preferences.DeadlineTicks = clock.DeadlineUtc.Ticks;
            preferences.Left = IsThrowing ? editorLeft : Window.ExpandedLeft;
            preferences.Top = IsThrowing ? editorTop : Window.Top;
            if (!store.Write(preferences) && tray != null)
                tray.Text = "Tommi · 设置保存失败，本次计时继续";
        }

        void CreateTray()
        {
            tray = new Forms.NotifyIcon();
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(smallArt));
            using (var bytes = new MemoryStream())
            {
                png.Save(bytes);
                bytes.Position = 0;
                using (var bmp = new System.Drawing.Bitmap(bytes))
                {
                    var handle = bmp.GetHicon();
                    try
                    {
                        using (var icon = System.Drawing.Icon.FromHandle(handle))
                        {
                            tray.Icon = (System.Drawing.Icon)icon.Clone();
                        }
                    }
                    finally
                    {
                        Native.DestroyIcon(handle);
                    }
                }
            }

            tray.Visible = true;
            tray.Text = "Tommi · 番茄钟";
            tray.MouseUp += delegate (object sender, Forms.MouseEventArgs e)
            {
                if (e.Button == Forms.MouseButtons.Right)
                    app.Dispatcher.BeginInvoke(new Action(OpenSettings));
            };
            tray.DoubleClick += delegate
            {
                Show();
            };
        }

        public void OpenSettings()
        {
            if (loginStartup != null)
                loginStartup.Refresh();
            if (Settings != null)
            {
                Settings.Activate();
                return;
            }

            var pointer = Forms.Cursor.Position;
            var screen = Forms.Screen.FromPoint(pointer);
            var matrix = source.CompositionTarget.TransformFromDevice;
            var area = screen.WorkingArea;
            Point corner = matrix.Transform(new Point(area.Left, area.Top));
            Point limit = matrix.Transform(new Point(area.Right, area.Bottom));
            Point anchor = matrix.Transform(new Point(pointer.X, pointer.Y));
            var panel = new SettingsWindow(this, smallArt);
            Settings = panel;
            panel.Closed += delegate
            {
                if (Settings == panel)
                    Settings = null;
            };
            panel.MaxHeight = Math.Max(160, limit.Y - corner.Y - 16);
            panel.Left = Math.Max(corner.X + 8, Math.Min(anchor.X - panel.Width + 24, limit.X - panel.Width - 8));
            panel.Top = corner.Y + 8;
            panel.Opacity = 0;
            panel.Show();
            panel.UpdateLayout();
            panel.Top = Math.Max(corner.Y + 8, Math.Min(anchor.Y - panel.ActualHeight - 8, limit.Y - panel.ActualHeight - 8));
            panel.Opacity = 1;
            if (SystemParameters.ClientAreaAnimation)
                panel.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
            panel.Activate();
        }

        void UpdateTray()
        {
            if (tray != null)
                tray.Text = clock.Phase == TimerPhase.Running ? "Tommi · 专注剩余 " + Format(clock.Remaining(DateTime.UtcNow)) : IsThrowing ? "Tommi · 时间到了，拖动番茄结束提醒" : "Tommi · 番茄钟";
        }

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312 && wParam.ToInt32() == 91)
            {
                StopAlarm();
                handled = true;
            }

            return IntPtr.Zero;
        }

        void DisplayChanged(object sender, EventArgs e)
        {
            app.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (IsThrowing)
                    StopAlarm();
                ClampWindow();
            }));
        }

        void PlayChime()
        {
            try
            {
                if (player != null)
                {
                    player.Dispose();
                    sound.Dispose();
                }

                sound = new MemoryStream();
                var writer = new BinaryWriter(sound);
                int samples = 44100 * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(44100);
                writer.Write(88200);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    double t = i / 44100.0;
                    double v = Math.Sin(2 * Math.PI * 659.25 * t) * Math.Exp(-3 * t);
                    if (t > .23)
                        v += .7 * Math.Sin(2 * Math.PI * 830.61 * (t - .23)) * Math.Exp(-4 * (t - .23));
                    writer.Write((short)(v * 6500 * Math.Min(1, t * 100)));
                }

                writer.Flush();
                sound.Position = 0;
                player = new SoundPlayer(sound);
                player.Play();
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public void Quit()
        {
            if (Settings != null)
                Settings.Close();
            Window.Settle();
            wheelFeedback.Dispose();
            throwFeedback.Dispose();
            Save();
            Exiting = true;
            ticker.Stop();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            if (hotkey)
                Native.UnregisterHotKey(source.Handle, 91);
            if (overlay != null)
                overlay.Close();
            if (source != null)
                source.RemoveHook(Hook);
            if (player != null)
                player.Dispose();
            if (sound != null)
                sound.Dispose();
            if (tray != null)
            {
                tray.Visible = false;
                var icon = tray.Icon;
                tray.Dispose();
                if (icon != null)
                    icon.Dispose();
            }

            Window.Close();
            app.Shutdown();
        }

        internal static void Log(Exception ex)
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TomatoFocus");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "error.log");
                if (File.Exists(file) && new FileInfo(file).Length > 1024 * 1024)
                    File.WriteAllText(file, "");
                File.AppendAllText(file, DateTime.UtcNow.ToString("o") + " " + ex + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
