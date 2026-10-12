using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tomato;

namespace Tomato.Tests
{
    internal static class HangingVerification
    {
        static void Require(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }

        static HangingMotion Motion()
        {
            return new HangingMotion(new Rect(620, 54, 76, 76), new Point(736, 16));
        }

        public static void Run()
        {
            var idle = new IdleDeadline();
            idle.Touch(10);
            Require(!idle.Due(69.99, true) && idle.Due(70, true), "Idle timeout must be exactly one minute");
            idle.Touch(80);
            Require(!idle.Due(1000, false) && !idle.Due(1059, true) && idle.Due(1060, true), "Running, hidden, captured and settings states postpone idle");
            var m = Motion();
            Rect home = m.Fruit;
            m.Grab(0);
            Require(!m.Pull(400, 0), "Horizontal pull cannot detach");
            Require(!m.Pull(0, 95) && m.Pull(0, 96), "Downward pull has a deliberate threshold");
            Require(m.Fruit.Y > home.Y && m.Fruit.Y - home.Y < 96, "Branch resists pulling");
            m.Release(0);
            m.Advance(.3);
            Rect spring = m.Fruit;
            m.Grab(.3);
            m.Pull(0, 0);
            Require(m.Fruit == spring, "Regrabbing a returning tomato must not jump");
            m.Release(.3);
            m.Advance(2);
            Require(m.Phase == HangingPhase.Hanging && m.Fruit == home && m.Angle == 0, "A short pull settles exactly and stops drawing");
            var normal = new Rect(180, 230, 220, 220);
            m.Return(normal, 10);
            m.Advance(10.22);
            Require(m.Fruit == normal && m.Branch > 0 && m.Branch < 1 && m.Editor == 1, "Branch must reach the unchanged stem before lifting");
            m.Advance(10.46);
            Require(m.Branch == 1, "Branch attaches before lifting");
            m.Advance(13);
            Require(m.Fruit == home && m.Angle == 0 && m.Editor == 0 && !m.Animating, "Return completes and does not leave a permanent render callback");
            m.Grab(14);
            m.Pull(-20, 100);
            var picked = m.Fruit;
            m.Detach(normal, 14);
            m.Advance(14);
            Require(m.Fruit == picked, "Picking starts exactly under the pointer");
            m.Advance(14.23);
            Require(m.Branch == 0 && m.Fruit.Width > 76 && m.Fruit.Width < 220, "Branch retracts before growth finishes");
            m.Advance(15);
            Require(m.Fruit == normal && m.Editor == 1 && m.Phase == HangingPhase.Complete, "Editor handoff is exact");
            m.Return(normal, 20);
            m.Advance(20.2);
            double branch = m.Branch, readout = m.Editor;
            Rect interrupted = m.Fruit;
            m.Detach(normal, 20.2);
            m.Advance(20.2);
            Require(m.Branch == branch && m.Editor == readout && m.Fruit == interrupted, "Tray activation during branch growth must not jump or hide controls");
            foreach (int hz in new[]
            {
                30,
                60,
                144
            }

            )
            {
                var a = Motion();
                a.Return(normal, 0);
                for (int i = 0; i < hz; i++)
                    a.Advance(i / (double)hz);
                a.Advance(1);
                var b = Motion();
                b.Return(normal, 0);
                b.Advance(1);
                Require(a.Fruit == b.Fruit && a.Angle == b.Angle && a.Branch == b.Branch, "Motion must not depend on frame rate");
                a.Advance(1000);
                Require(a.Phase == HangingPhase.Hanging && a.Fruit == home, "Sleep/resume completes safely");
            }

            var negative = new HangingMotion(new Rect(-210, -800, 76, 76), new Point(-90, -840));
            negative.Return(new Rect(-1700, -450, 220, 220), 0);
            negative.Advance(10);
            Require(negative.Fruit.Left == -210 && negative.Fruit.Top == -800, "Secondary monitor negative coordinates");
        }

        static string Output(string file)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
        }

        public static int LaunchSmoke(string mode)
        {
            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            string path = Output("hanging-launch-state.xml");
            var pref = new Preferences
            {
                Seconds = 1500,
                Active = mode != "idle",
                DeadlineTicks = DateTime.UtcNow.AddMinutes(mode == "expired" ? -1 : 25).Ticks,
                Sound = false,
                EffectsSound = false
            };
            new StateStore(path).Write(pref);
            var c = new AppController(app, path);
            try
            {
                c.Launch(true);
                if (mode == "idle")
                    Require(c.Idle.IsHanging && !c.Window.IsVisible && c.Window.Duration == 1500, "Normal startup hangs and retains duration");
                if (mode == "running")
                    Require(!c.Idle.IsHanging && c.Phase == TimerPhase.Running && c.Window.IsVisible, "Active countdown bypasses hanging startup");
                if (mode == "expired")
                    Require(!c.Idle.IsHanging && c.IsThrowing && c.Window.IsVisible, "Expired countdown shows its reminder immediately");
                File.WriteAllText(Output("hanging-launch-" + mode + "-results.txt"), "PASS production Launch(true): " + mode);
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Output("hanging-launch-" + mode + "-results.txt"), "FAIL " + ex);
                return 1;
            }
            finally
            {
                c.Quit();
            }
        }

        public static void TimeoutSmoke(AppController c)
        {
            c.Idle.Touch();
            var watch = Stopwatch.StartNew();
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            bool sawReturn = false;
            timer.Tick += delegate
            {
                try
                {
                    double time = watch.Elapsed.TotalSeconds;
                    if (time < 59.8)
                        Require(!c.Idle.IsHanging && c.Window.IsVisible, "Idle must not begin before one minute");
                    if (c.Idle.IsHanging)
                        sawReturn = true;
                    if (time < 64)
                        return;
                    Require(sawReturn && c.Idle.IsHanging && c.Idle.Window.Motion.Phase == HangingPhase.Hanging, "One actual minute of inactivity must park and settle");
                    File.WriteAllText(Output("hanging-timeout-results.txt"), "PASS real 60-second inactivity deadline and settled hanging state; no early return");
                    timer.Stop();
                    c.Quit();
                }
                catch (Exception ex)
                {
                    Verification.SmokeExitCode = 1;
                    File.WriteAllText(Output("hanging-timeout-results.txt"), "FAIL " + ex);
                    timer.Stop();
                    c.Quit();
                }
            };
            timer.Start();
        }

        static void Save(Visual visual, string name, int width, int height, double scale)
        {
            var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Output(name)))
                png.Save(file);
        }

        public static void Storyboard(AppController c)
        {
            var art = Art.TomatoImage(880);
            var snapshot = c.Window.IdleSnapshot();
            var drawing = new DrawingVisual();
            using (var dc = drawing.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(238, 243, 230)), null, new Rect(0, 0, 1200, 600));
                for (int i = 0; i < 8; i++)
                {
                    int col = i % 4, row = i / 4;
                    var m = new HangingMotion(new Rect(165, 64, 76, 76), new Point(286, 18));
                    string label;
                    if (i < 4)
                    {
                        label = new[]
                        {
                            "01 / REST",
                            "02 / PULL",
                            "03 / PICK",
                            "04 / READY"
                        }[i];
                        if (i > 0)
                        {
                            m.Grab(0);
                            m.Pull(-12, 90);
                        }

                        if (i > 1)
                        {
                            m.Detach(new Rect(56, 78, 220, 220), 0);
                            m.Advance(i == 2 ? .3 : .68);
                        }
                    }
                    else
                    {
                        label = new[]
                        {
                            "05 / REACH",
                            "06 / LIFT",
                            "07 / SWAY",
                            "08 / REST"
                        }[i - 4];
                        m.Return(new Rect(56, 78, 220, 220), 0);
                        m.Advance(new[] { .3, 1.05, 1.7, 2.65 }[i - 4]);
                    }

                    var surface = new HangingSurface(m, art, snapshot)
                    {
                        Width = 300,
                        Height = 300
                    };
                    surface.Measure(new Size(300, 300));
                    surface.Arrange(new Rect(0, 0, 300, 300));
                    surface.UpdateLayout();
                    var brush = new VisualBrush(surface)
                    {
                        Stretch = Stretch.None,
                        ViewboxUnits = BrushMappingMode.Absolute,
                        Viewbox = new Rect(0, 0, 300, 300)
                    };
                    dc.DrawRectangle(brush, null, new Rect(col * 300, row * 300, 300, 300));
                    var text = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, new SolidColorBrush(Color.FromRgb(55, 70, 47)), 1);
                    dc.DrawText(text, new Point(col * 300 + 16, row * 300 + 278));
                }
            }

            Save(drawing, "hanging-storyboard.png", 1200, 600, 1.5);
        }

        public static void Smoke(AppController c)
        {
            var elapsed = Stopwatch.StartNew();
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(40)
            };
            int stage = 0;
            double since = 0;
            int duration = c.Window.Duration;
            var lines = new System.Collections.Generic.List<string>();
            var frames = new System.Collections.Generic.List<double>();
            EventHandler rendering = delegate (object sender, EventArgs args)
            {
                if (stage == 1 && c.Idle.Window != null && c.Idle.Window.Motion.Phase == HangingPhase.Returning)
                {
                    double stamp = ((RenderingEventArgs)args).RenderingTime.TotalMilliseconds;
                    if (frames.Count == 0 || frames[frames.Count - 1] != stamp)
                        frames.Add(stamp);
                }
            };
            CompositionTarget.Rendering += rendering;
            timer.Tick += delegate
            {
                try
                {
                    double now = elapsed.Elapsed.TotalSeconds;
                    if (now - since < (stage == 1 || stage == 3 ? 3.1 : stage == 2 || stage == 4 ? 1 : .25))
                        return;
                    if (stage == 0)
                    {
                        c.Idle.Park(true);
                        Require(c.Idle.IsHanging && !c.Window.IsVisible, "Returning replaces the editor with one animation surface");
                    }
                    else if (stage == 1)
                    {
                        Require(c.Idle.Window.Motion.Phase == HangingPhase.Hanging, "Return reaches its settled state");
                        c.Idle.Window.UpdateLayout();
                        Save(c.Idle.Window, "hanging-idle.png", (int)c.Idle.Window.Width, (int)c.Idle.Window.Height, 2);
                        c.SetLanguage("en");
                        c.OpenSettings();
                        c.Idle.Check();
                        Require(c.Idle.IsHanging && c.Settings != null, "Settings work while hanging");
                        c.Settings.Close();
                        c.Show();
                    }
                    else if (stage == 2)
                    {
                        Require(!c.Idle.IsHanging && c.Window.IsVisible && c.Window.Width == 220 && c.Window.Duration == duration, "Picking restores the same duration and normal editor size");
                        c.Idle.Park(true);
                        c.Start();
                        Require(!c.Idle.IsHanging && c.Phase == TimerPhase.Running, "Start safely interrupts a return");
                        c.Idle.Park(true);
                        Require(!c.Idle.IsHanging, "Focus never becomes idle");
                        c.Cancel();
                        c.Idle.Park(true);
                    }
                    else if (stage == 3)
                    {
                        Require(c.Idle.IsHanging, "Cancel can later return to idle");
                        c.Preview();
                        Require(c.IsThrowing && !c.Idle.IsHanging && c.Window.IsVisible, "Preview safely wakes the hanging tomato");
                        c.StopAlarm();
                        c.Idle.Park(false);
                        c.Idle.DisplayChanged();
                        Require(c.Idle.IsHanging && c.Idle.Window.Motion.Phase == HangingPhase.Hanging, "Display changes re-anchor without duplicate windows");
                        c.Idle.Detach();
                    }
                    else
                    {
                        Require(!c.Idle.IsHanging && c.Window.Duration == duration, "Duration survives all transitions: hanging=" + c.Idle.IsHanging + ", duration=" + c.Window.Duration + ", expected=" + duration + ", phase=" + (c.Idle.Window == null ? "none" : c.Idle.Window.Motion.Phase.ToString()));
                        lines.Add("PASS return / detach / settings / start interruption / running exclusion / alarm / display changes / duration preservation");
                        if (frames.Count > 1)
                        {
                            double maximum = 0;
                            for (int i = 1; i < frames.Count; i++)
                                maximum = Math.Max(maximum, frames[i] - frames[i - 1]);
                            lines.Add("Return frames=" + frames.Count + "; average interval=" + ((frames[frames.Count - 1] - frames[0]) / (frames.Count - 1)).ToString("F2") + " ms; maximum=" + maximum.ToString("F2") + " ms");
                        }

                        File.WriteAllLines(Output("hanging-smoke-results.txt"), lines);
                        CompositionTarget.Rendering -= rendering;
                        timer.Stop();
                        c.Quit();
                        return;
                    }

                    stage++;
                    since = elapsed.Elapsed.TotalSeconds;
                }
                catch (Exception ex)
                {
                    Verification.SmokeExitCode = 1;
                    File.WriteAllText(Output("hanging-smoke-results.txt"), "FAIL " + ex);
                    CompositionTarget.Rendering -= rendering;
                    timer.Stop();
                    c.Quit();
                }
            };
            timer.Start();
        }
    }
}
