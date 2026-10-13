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
            return new HangingMotion(new Rect(620, 54, 76, 76), new Point(658, -4));
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
            Rect released = m.Fruit;
            m.Advance(.001);
            Require((m.Fruit.Location - released.Location).Length / .001 < 15, "Release accelerates from rest instead of snapping into an upward velocity");
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
            m.Advance(10 + BranchReturn.ReachSeconds);
            Require(m.Branch == 1, "Branch attaches before lifting");
            m.Advance(10 + HangingMotion.ReturnDuration);
            Require(m.Fruit == home && m.Angle == 0 && m.Editor == 0 && !m.Animating, "Return completes and does not leave a permanent render callback");
            m.Grab(16);
            m.Pull(-20, 100);
            var picked = m.Fruit;
            m.Detach(normal, 16);
            m.Advance(16);
            Require(m.Fruit == picked, "Picking starts exactly under the pointer");
            m.Advance(16.23);
            Require(m.Branch == 0 && m.Fruit.Width > 76 && m.Fruit.Width < 220, "Branch retracts before growth finishes");
            m.Advance(17);
            Require(m.Fruit == normal && m.Editor == 1 && m.Phase == HangingPhase.Complete, "Editor handoff is exact");
            m.Return(normal, 20);
            m.Advance(20.2);
            double branch = m.Branch, readout = m.Editor;
            Rect interrupted = m.Fruit;
            m.Detach(normal, 20.2);
            m.Advance(20.2);
            Require(m.Branch == branch && m.Editor == readout && m.Fruit == interrupted, "Tray activation during branch growth must not jump or hide controls");
            m.Return(normal, 24);
            m.Advance(26);
            interrupted = m.Fruit;
            double yaw = m.Yaw, load = m.Load;
            m.Detach(normal, 26);
            m.Advance(26);
            Require(m.Fruit == interrupted && m.Yaw == yaw && m.Load == load, "Picking during depth motion preserves perspective tilt and branch loading");
            m.Advance(27);
            Require(m.Fruit == normal && m.Yaw == 0 && m.Load == 0, "Depth handoff returns to the flat editor without residual transforms");
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
                for (int i = 0; i < hz * 3; i++)
                    a.Advance(i / (double)hz);
                a.Advance(3);
                var b = Motion();
                b.Return(normal, 0);
                b.Advance(3);
                Require(a.Fruit == b.Fruit && a.Angle == b.Angle && a.Branch == b.Branch, "Motion must not depend on frame rate");
                a.Advance(1000);
                Require(a.Phase == HangingPhase.Hanging && a.Fruit == home, "Sleep/resume completes safely");
            }

            var negative = new HangingMotion(new Rect(-210, -800, 76, 76), new Point(-172, -840));
            negative.Return(new Rect(-1700, -450, 220, 220), 0);
            negative.Advance(10);
            Require(negative.Fruit.Left == -210 && negative.Fruit.Top == -800, "Secondary monitor negative coordinates");
            ReturnPhysics();
        }

        static Point Stem(Rect fruit)
        {
            return new Point(fruit.X + fruit.Width * .5, fruit.Y + fruit.Height * .22);
        }

        static void ReturnPhysics()
        {
            var screen = new Rect(0, 0, 1920, 1080);
            var root = new Point(1846, -4);
            var home = new Rect(1808, 32, 76, 76);
            var start = new Rect(700, 300, 220, 220);
            var path = new BranchReturn(root, home, start, screen);
            Point attached = Stem(start), fallen = Stem(path.Sample(.1).Fruit);
            Require(fallen.X > attached.X && fallen.Y > attached.Y, "Gravity first swings the attached fruit down and right");
            Require(path.Sample(.2).Fruit.Width > start.Width * .98, "Weight loads the branch before the fruit recedes appreciably");
            bool loaded = false, receding = false;
            for (int i = 1; i <= 1080; i++)
            {
                var pose = path.Sample(i / 240.0);
                double projection = BranchReturn.FocalLength / (BranchReturn.FocalLength + pose.WorldStem.Z);
                Require(Math.Abs(pose.Fruit.Width - start.Width * projection) < .00001, "Apparent size must come only from camera distance");
                Point projectedStem = root + new Vector(pose.WorldStem.X, pose.WorldStem.Y) * projection;
                Require((Stem(pose.Fruit) - projectedStem).Length < .00001, "Position and size must use the same perspective camera");
                loaded |= pose.Load > .1;
                receding |= pose.WorldStem.Z > 600 && Math.Abs(pose.Yaw) > 1;
            }

            Require(path.Sample(BranchReturn.SwingSeconds).Fruit == home, "Pendulum settles exactly at its vertical perch");
            Require(loaded && receding, "The tether visibly takes weight before the fruit turns and recedes");
            foreach (Rect display in new[]
            {
                screen,
                new Rect(-1920, -1080, 1920, 1080),
                new Rect(0, 0, 3440, 1440),
                new Rect(0, 0, 1280, 720),
                new Rect(0, 0, 1024, 600)
            }

            )
            {
                var perch = new Rect(display.Right - 112, display.Top + 32, 76, 76);
                var anchor = new Point(perch.X + 38, display.Top - 4);
                foreach (double x in new[]
                {
                    display.Left,
                    display.Left + display.Width / 2 - 110,
                    display.Right - 220
                }

                )
                    foreach (double y in new[]
                    {
                        display.Top,
                        display.Top + display.Height / 2 - 110,
                        display.Bottom - 220
                    }

                    )
                    {
                        var origin = new Rect(x, y, 220, 220);
                        var motion = new HangingMotion(perch, anchor, display);
                        motion.Return(origin, 0);
                        Rect previous = origin;
                        for (int i = 0; i <= 1280; i++)
                        {
                            motion.Advance(i / 240.0);
                            Require(motion.Root == anchor, "Top bezel anchor remains fixed throughout return");
                            Require(!double.IsNaN(motion.Angle) && !double.IsInfinity(motion.Fruit.X), "Pendulum remains finite");
                            Require((motion.Fruit.Location - previous.Location).Length < 40, "Return never teleports across a display boundary: " + display + " start " + origin + " time " + i / 240.0 + " fruit " + motion.Fruit);
                            Point stem = Stem(motion.Fruit);
                            double tilt = motion.Angle * Math.PI / 180, width = motion.Fruit.Width;
                            Point center = stem + new Vector(-Math.Sin(tilt) * width * .28, Math.Cos(tilt) * width * .28);
                            double radius = width * .44;
                            Require(center.X - radius >= display.Left - .1 && center.X + radius <= display.Right + .1 && center.Y + radius <= display.Bottom + .1, "Visible fruit stays on display: " + display + " start " + origin + " time " + i / 240.0 + " center " + center + " radius " + radius);
                            previous = motion.Fruit;
                        }
                    }
            }
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
                    if (time < 60 + HangingMotion.ReturnDuration + 1)
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
                    var m = new HangingMotion(new Rect(165, 32, 76, 76), new Point(203, -4), new Rect(0, 0, 300, 300));
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
                            "06 / SWING",
                            "07 / REEL",
                            "08 / REST"
                        }[i - 4];
                        m.Return(new Rect(56, 78, 220, 220), 0);
                        m.Advance(new[] { .5, 1.1, 2.2, HangingMotion.ReturnDuration }[i - 4]);
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

        public static int ReturnFrames()
        {
            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            var c = new AppController(app, Output("return-render-state.xml"), new StartupVerification.FakeRegistration());
            try
            {
                c.Launch(false, false);
                string folder = Output("return-frames");
                Directory.CreateDirectory(folder);
                var motion = new HangingMotion(new Rect(788, 32, 76, 76), new Point(826, -4), new Rect(0, 0, 900, 600));
                motion.Return(new Rect(70, 210, 220, 220), 0);
                var surface = new HangingSurface(motion, Art.TomatoImage(880), c.Window.IdleSnapshot())
                {
                    Width = 900,
                    Height = 600
                };
                surface.Measure(new Size(900, 600));
                surface.Arrange(new Rect(0, 0, 900, 600));
                for (int i = 0; i < 195; i++)
                {
                    motion.Advance(Math.Max(0, i / 30.0 - .4));
                    surface.InvalidateVisual();
                    surface.UpdateLayout();
                    var frame = new DrawingVisual();
                    using (var dc = frame.RenderOpen())
                    {
                        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(238, 243, 230)), null, new Rect(0, 0, 900, 600));
                        dc.DrawRectangle(new VisualBrush(surface) { Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 900, 600) }, null, new Rect(0, 0, 900, 600));
                    }

                    Save(frame, Path.Combine("return-frames", i.ToString("D3") + ".png"), 900, 600, 1);
                }

                return 0;
            }
            finally
            {
                c.Quit();
            }
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
                    if (now - since < (stage == 1 || stage == 3 ? HangingMotion.ReturnDuration + .5 : stage == 2 || stage == 4 ? 1 : .25))
                        return;
                    if (stage == 0)
                    {
                        c.Idle.Park(true);
                        Require(c.Idle.IsHanging && !c.Window.IsVisible, "Returning replaces the editor with one animation surface");
                        var handle = new System.Windows.Interop.WindowInteropHelper(c.Window).Handle;
                        var display = System.Windows.Forms.Screen.FromHandle(handle).Bounds;
                        var transform = PresentationSource.FromVisual(c.Window).CompositionTarget.TransformFromDevice;
                        double top = transform.Transform(new Point(display.Left, display.Top)).Y;
                        Require(Math.Abs(c.Idle.Window.Motion.Root.Y - (top - 4)) < .01, "Branch starts at the monitor top bezel, not the work area or right edge");
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
