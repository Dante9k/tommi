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
            return new HangingMotion(new Rect(628, 54, 60, 60), new Point(658, -4));
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
            Require(home.Width == HangingMotion.RestSize && Math.Abs(m.Opacity - .62) < .00001, "Idle tomato is compact and translucent");
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
            Require(m.Fruit == normal && m.Branch > 0 && m.Branch < 1 && m.Editor == 1 && m.Opacity == 1, "Branch must reach the unchanged opaque editor before lifting");
            m.Advance(10 + m.ReachDuration);
            Require(m.Branch == 1 && m.Contact == 1 && m.Fruit == normal, "Branch meets the stem cap before lifting");
            m.Advance(10 + m.ReachDuration - BranchReturn.CatchSeconds / 2);
            Require(m.Branch == 1 && m.Contact > 0 && m.Contact < 1 && m.Fruit == normal, "Contact briefly settles while the tomato still rests");
            m.Advance(10 + m.ReturnDuration);
            Require(m.Fruit == home && m.Angle == 0 && m.Editor == 0 && !m.Animating, "Return completes and does not leave a permanent render callback");
            m.Grab(16);
            m.Pull(-20, 100);
            var picked = m.Fruit;
            m.Detach(normal, 16);
            m.Advance(16);
            Require(m.Fruit == picked, "Picking starts exactly under the pointer");
            m.Advance(16.23);
            Require(m.Branch == 0 && m.Fruit.Width > 60 && m.Fruit.Width < 220 && m.Opacity > .62 && m.Opacity < 1, "Branch retracts before growth finishes and opacity restores continuously");
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
            double yaw = m.Yaw, pitch = m.Pitch, contact = m.Contact, load = m.Load, opacity = m.Opacity;
            m.Detach(normal, 26);
            m.Advance(26);
            Require(m.Fruit == interrupted && m.Yaw == yaw && m.Pitch == pitch && m.Contact == contact && m.Load == load && m.Opacity == opacity, "Picking during depth motion preserves perspective tilt, contact, opacity and branch loading");
            m.Advance(27);
            Require(m.Fruit == normal && m.Yaw == 0 && m.Pitch == 0 && m.Contact == 0 && m.Load == 0, "Depth handoff returns to the flat editor without residual transforms");
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

            var negative = new HangingMotion(new Rect(-210, -800, 60, 60), new Point(-180, -840));
            negative.Return(new Rect(-1700, -450, 220, 220), 0);
            negative.Advance(10);
            Require(negative.Fruit.Left == -210 && negative.Fruit.Top == -800, "Secondary monitor negative coordinates");
            Attachment();
            ReturnPhysics();
            VineDepth();
            TravelSpeed();
            BiteSequence();
        }

        static void BiteSequence()
        {
            var m = Motion();
            Require(m.Bud.Presence == 0, "Resting vine has no permanent face");
            var origin = new Rect(180, 230, 220, 220);
            m.Return(origin, 0);
            double arrival = m.ReachDuration - BranchReturn.CatchSeconds;
            m.Advance(arrival);
            Require(m.Bud.Mouth > .99 && m.Bud.Presence == 1 && m.Fruit == origin, "Bud arrives with open jaws while fruit remains still");
            m.Advance(arrival + .22);
            Require(m.Bud.Mouth == 0 && m.Bud.Presence == 1 && m.Bud.Merge == 0 && m.Fruit == origin, "Bite closes and holds before merging or lifting");
            m.Advance(arrival + .5);
            Require(m.Bud.Merge > .2 && m.Bud.Merge < .9 && m.Bud.Presence > 0 && m.Fruit == origin, "Cheeks narrow and features fade before weight is lifted");
            m.Advance(m.ReachDuration);
            Require(m.Bud.Presence < .000001 && m.Bud.Merge > .999999, "Face has merged into stalk before retraction");
            foreach (double offset in new[]
            {
                -.1,
                .08,
                .4
            }

            )
            {
                m.Return(origin, 0);
                double time = arrival + offset;
                m.Advance(time);
                var pose = m.Bud;
                m.Detach(origin, time);
                m.Advance(time);
                Require(m.Bud.Presence == pose.Presence && m.Bud.Mouth == pose.Mouth && m.Bud.Merge == pose.Merge, "Interrupting bite preserves the exact face pose");
                m.Advance(time + .19);
                Require(m.Bud.Presence == 0, "Interrupted face fades out before editor handoff");
            }

            foreach (int hz in new[]
            {
                30,
                60,
                144
            }

            )
            {
                m.Return(origin, 0);
                double time = arrival + .4;
                for (int i = 0; i / (double)hz < time; i++)
                    m.Advance(i / (double)hz);
                m.Advance(time);
                var direct = BudBite.Sample(time, arrival);
                Require(m.Bud.Presence == direct.Presence && m.Bud.Mouth == direct.Mouth && m.Bud.Merge == direct.Merge, "Bite animation is independent of refresh rate");
            }
        }

        static void Attachment()
        {
            // Check against the rendered resource, not another use of the same anchor
            // formula. This catches source-image dimension changes and wrong cap pixels.
            var texture = Art.TomatoImage(880);
            var pixels = new byte[880 * 880 * 4];
            texture.CopyPixels(pixels, 880 * 4, 0);
            double capX = 0, capY = 0;
            int count = 0;
            for (int y = 125; y < 151; y++)
                for (int x = 465; x < 497; x++)
                {
                    int offset = (y * 880 + x) * 4;
                    if (pixels[offset + 3] > 240 && pixels[offset + 2] > 175 && pixels[offset + 1] > 180 && pixels[offset] > 110)
                    {
                        capX += x + .5;
                        capY += y + .5;
                        count++;
                    }
                }

            Require(count > 50, "Actual embedded artwork contains the expected light stem cut face");
            Point measured = new Point(capX / count, capY / count);
            Require((TomatoAttachment.At(new Rect(0, 0, 880, 880)) - measured).Length < 2, "Attachment must match the visible cut face in the rendered artwork, within half a logical pixel at editor size");
            foreach (double size in new[]
            {
                60.0,
                120,
                220
            }

            )
                foreach (double angle in new[]
                {
                    -23.0,
                    0,
                    23
                }

                )
                    foreach (double yaw in new[]
                    {
                        -32.0,
                        0,
                        32
                    }

                    )
                    {
                        var fruit = new Rect(100, 200, size, size);
                        Point cap = TomatoAttachment.At(fruit);
                        var vine = new VineProjection(new Point(826, -4), cap, size, .5, angle, yaw, 28);
                        var tip = vine.Sample(1);
                        var transforms = new TransformGroup();
                        transforms.Children.Add(new ScaleTransform(Math.Cos(yaw * Math.PI / 180), Math.Cos(28 * Math.PI / 180), cap.X, cap.Y));
                        transforms.Children.Add(new SkewTransform(-yaw * .18, 0, cap.X, cap.Y));
                        transforms.Children.Add(new RotateTransform(angle, cap.X, cap.Y));
                        Require((tip.Center - transforms.Transform(cap)).Length < .000001, "Vine and artwork cap share one pivot at every size and orientation");
                        Vector axis = transforms.Transform(cap + new Vector(-.42, .9075)) - cap;
                        axis.Normalize();
                        tip.Tangent.Normalize();
                        Require(Vector.Multiply(axis, tip.Tangent) > .999999, "Vine meets the cap along the transformed stem axis without a clasp");
                    }
        }

        static void TravelSpeed()
        {
            var vine = new VineProjection(new Point(826, -4), new Point(180, 258.4), 220, 0);
            var reach = new VineReach(vine);
            var nearby = new VineReach(new VineProjection(new Point(826, -4), new Point(730, 90), 220, 0));
            Require(reach.Duration > nearby.Duration + .2, "Longer reaches need more travel time instead of a faster curve-parameter sweep");
            Require(reach.Progress(0) == 0 && reach.Progress(reach.Duration) == 1, "Arc-length reach has exact endpoints");
            const double step = .001;
            double previousSpeed = 0, peak = 0;
            for (double t = step; t < reach.Duration; t += step)
            {
                Point p = vine.Sample(reach.Progress(t)).Center;
                Point previous = vine.Sample(reach.Progress(t - step)).Center;
                double speed = (p - previous).Length / step;
                Require(Math.Abs(speed - previousSpeed) < 35, "Tip velocity stays continuous through acceleration and braking");
                previousSpeed = speed;
                peak = Math.Max(peak, speed);
            }

            foreach (double fraction in new[]
            {
                .08,
                .85
            }

            )
            {
                double t = reach.Duration * fraction;
                double slow = (vine.Sample(reach.Progress(t)).Center - vine.Sample(reach.Progress(t - step)).Center).Length / step;
                Require(peak > slow * 5, "Reach has a clearly visible burst and slow approach, not a near-uniform sweep");
            }

            double startSpeed = (vine.Sample(reach.Progress(.01)).Center - vine.Sample(0).Center).Length / .01;
            double stopSpeed = (vine.Sample(1).Center - vine.Sample(reach.Progress(reach.Duration - .01)).Center).Length / .01;
            Require(startSpeed < peak * .03 && stopSpeed < peak * .03, "The shoot starts and touches down gently");
        }

        static void VineDepth()
        {
            Point root = new Point(826, -4), stem = new Point(180, 258.4);
            var vine = new VineProjection(root, stem, 220, 0);
            var far = vine.Sample(0);
            var near = vine.Sample(1);
            Require((far.Center - root).Length < .00001 && (near.Center - stem).Length < .00001, "Projected vine stays anchored to the top bezel and attaches exactly to the stem");
            Require(near.Scale / far.Scale > 3.6, "Approaching shoot has a visible far-to-near scale difference");
            double minimumDepth = far.Depth, maximumSize = far.Scale;
            Point previous = root;
            for (int i = 1; i <= 100; i++)
            {
                var part = vine.Sample(i / 100.0);
                Require(part.Depth > -BranchReturn.FocalLength * .3 && (part.Center - previous).Length < 25, "The forward arch stays in front of the camera without projection discontinuities");
                Require(Math.Abs(part.Scale - 220.0 / 76 * BranchReturn.FocalLength / (BranchReturn.FocalLength + part.Depth)) < .00001, "Vine thickness and leaf size share the depth camera");
                minimumDepth = Math.Min(minimumDepth, part.Depth);
                maximumSize = Math.Max(maximumSize, part.Scale);
                previous = part.Center;
            }

            Require(minimumDepth < -80 && maximumSize > near.Scale * 1.1, "The shoot bows forward past the stem plane before curling back to catch it");
            Require((vine.Sample(.7 * .5).Center - vine.Sample(.7).Center).Length > 60, "Leaf nodes travel forward with the shoot instead of revealing at fixed positions");
            Require((vine.Sample(.99999).Center - near.Center).Length < .1, "Contact approaches the attached geometry without a jump");
        }

        static Point Stem(Rect fruit)
        {
            return TomatoAttachment.At(fruit);
        }

        static void ReturnPhysics()
        {
            var screen = new Rect(0, 0, 1920, 1080);
            var root = new Point(1846, -4);
            var home = new Rect(1816, 32, 60, 60);
            var start = new Rect(700, 300, 220, 220);
            var path = new BranchReturn(root, home, start, screen);
            Point attached = Stem(start), fallen = Stem(path.Sample(.1).Fruit);
            Require(fallen.X > attached.X && fallen.Y > attached.Y, "Gravity first swings the attached fruit down and right");
            Require(path.Sample(.2).Fruit.Width > start.Width * .98, "Weight loads the branch before the fruit recedes appreciably");
            bool loaded = false, receding = false;
            double minimumSize = start.Width;
            Point previousStem = attached;
            Vector previousVelocity = new Vector();
            for (int i = 1; i <= 1080; i++)
            {
                var pose = path.Sample(i / 240.0);
                Point currentStem = Stem(pose.Fruit);
                Vector velocity = (currentStem - previousStem) * 240;
                Require(currentStem.Y - attached.Y < 100, "Pickup must not drop the tomato a large fraction of the desktop height");
                Require((velocity - previousVelocity).Length < 30, "Projected velocity stays continuous through pickup, reel and final handoff: t=" + i / 240.0 + " dv=" + (velocity - previousVelocity).Length);
                Require(pose.Fruit.Width <= minimumSize + .15, "Receding fruit must not visibly inflate again before settling");
                minimumSize = Math.Min(minimumSize, pose.Fruit.Width);
                previousStem = currentStem;
                previousVelocity = velocity;
                double projection = BranchReturn.FocalLength / (BranchReturn.FocalLength + pose.WorldStem.Z);
                Require(Math.Abs(pose.Fruit.Width - start.Width * projection) < .00001, "Apparent size must come only from camera distance");
                Point projectedStem = root + new Vector(pose.WorldStem.X, pose.WorldStem.Y) * projection;
                Require((Stem(pose.Fruit) - projectedStem).Length < .00001, "Position and size must use the same perspective camera");
                loaded |= pose.Load > .1;
                receding |= pose.WorldStem.Z > 600 && Math.Abs(pose.Yaw) > 20 && Math.Abs(pose.Pitch) > 15;
            }

            Require(path.Sample(path.Duration).Fruit == home, "Pendulum settles exactly at its vertical perch");
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
                var perch = new Rect(display.Right - 104, display.Top + 32, 60, 60);
                var anchor = new Point(perch.X + 30, display.Top - 4);
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
                        for (int i = 0; i <= (int)Math.Ceiling(motion.ReturnDuration * 240); i++)
                        {
                            motion.Advance(i / 240.0);
                            Require(motion.Root == anchor, "Top bezel anchor remains fixed throughout return");
                            Require(!double.IsNaN(motion.Angle) && !double.IsInfinity(motion.Fruit.X), "Pendulum remains finite");
                            Require((motion.Fruit.Location - previous.Location).Length < 40, "Return never teleports across a display boundary: " + display + " start " + origin + " time " + i / 240.0 + " fruit " + motion.Fruit);
                            Point stem = Stem(motion.Fruit);
                            double tilt = motion.Angle * Math.PI / 180, width = motion.Fruit.Width;
                            Vector offset = new Vector(width * (.5 - TomatoAttachment.X), width * (.5 - TomatoAttachment.Y));
                            Point center = stem + new Vector(offset.X * Math.Cos(tilt) - offset.Y * Math.Sin(tilt), offset.X * Math.Sin(tilt) + offset.Y * Math.Cos(tilt));
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
                    if (time < 60 + HangingMotion.MaxReturnDuration + 1)
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
                    var m = new HangingMotion(new Rect(173, 32, 60, 60), new Point(203, -4), new Rect(0, 0, 300, 300));
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
                        m.Advance(new[] { m.ReachDuration * .45, m.ReachDuration + .2, m.ReachDuration + 1.1, m.ReturnDuration }[i - 4]);
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
                var motion = new HangingMotion(new Rect(796, 32, 60, 60), new Point(826, -4), new Rect(0, 0, 900, 600));
                motion.Return(new Rect(70, 210, 220, 220), 0);
                var surface = new HangingSurface(motion, Art.TomatoImage(880), c.Window.IdleSnapshot())
                {
                    Width = 900,
                    Height = 600
                };
                surface.Measure(new Size(900, 600));
                surface.Arrange(new Rect(0, 0, 900, 600));
                int frameCount = (int)Math.Ceiling((motion.ReturnDuration + 1.2) * 30);
                for (int i = 0; i < frameCount; i++)
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

                File.WriteAllText(Output("return-frame-count.txt"), frameCount.ToString());
                File.WriteAllText(Output("return-timing.txt"), "Reach including contact: " + motion.ReachDuration.ToString("F3") + " s; complete return: " + motion.ReturnDuration.ToString("F3") + " s; frames: " + frameCount);
                AttachmentDetails(motion, surface);
                BiteDetails(motion, surface);
                return 0;
            }
            finally
            {
                c.Quit();
            }
        }

        static void AttachmentDetails(HangingMotion motion, HangingSurface surface)
        {
            var sheet = new DrawingVisual();
            double[] times =
            {
                motion.ReachDuration,
                motion.ReachDuration + .35,
                motion.ReachDuration + .85,
                motion.ReturnDuration
            };
            string[] labels =
            {
                "CONTACT",
                "LOAD",
                "RETRACT",
                "REST"
            };
            using (var dc = sheet.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(238, 243, 230)), null, new Rect(0, 0, 800, 260));
                for (int i = 0; i < times.Length; i++)
                {
                    motion.Return(new Rect(70, 210, 220, 220), 0);
                    motion.Advance(times[i]);
                    surface.InvalidateVisual();
                    surface.UpdateLayout();
                    double span = 42 * motion.Fruit.Width / 220;
                    var bounds = new Rect(motion.Stem.X - span / 2, motion.Stem.Y - span / 2, span, span);
                    var visual = new DrawingVisual();
                    using (var view = visual.RenderOpen())
                        view.DrawRectangle(new VisualBrush(surface) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds }, null, new Rect(0, 0, 200, 200));
                    var bitmap = new RenderTargetBitmap(400, 400, 192, 192, PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    byte[] center = new byte[4];
                    bitmap.CopyPixels(new Int32Rect(200, 200, 1, 1), center, 4, 0);
                    Require(center[1] > center[2] * 1.15 && center[0] < center[2] * .7, "Actual composited attachment must cover the pale cut face at " + labels[i]);
                    dc.DrawImage(bitmap, new Rect(i * 200, 10, 200, 200));
                    dc.DrawText(new FormattedText(labels[i], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.DarkOliveGreen, 1), new Point(i * 200 + 12, 228));
                }
            }

            Save(sheet, "attachment-details.png", 800, 260, 1.5);
        }

        static void BiteDetails(HangingMotion motion, HangingSurface surface)
        {
            double arrival = motion.ReachDuration - BranchReturn.CatchSeconds;
            double[] times =
            {
                arrival * .65,
                arrival,
                arrival + .09,
                arrival + .24,
                arrival + .49,
                motion.ReachDuration
            };
            string[] labels =
            {
                "01 / APPROACH",
                "02 / OPEN",
                "03 / BITE",
                "04 / HOLD",
                "05 / MERGE",
                "06 / JOINED"
            };
            var sheet = new DrawingVisual();
            using (var dc = sheet.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(238, 243, 230)), null, new Rect(0, 0, 900, 600));
                for (int i = 0; i < times.Length; i++)
                {
                    motion.Return(new Rect(70, 210, 220, 220), 0);
                    motion.Advance(times[i]);
                    surface.InvalidateVisual();
                    surface.UpdateLayout();
                    var tip = new VineProjection(motion.Root, motion.Stem, motion.Fruit.Width, motion.Load).Sample(motion.Branch);
                    var bounds = new Rect(tip.Center.X - 65, tip.Center.Y - 85, 130, 120);
                    var visual = new DrawingVisual();
                    using (var view = visual.RenderOpen())
                        view.DrawRectangle(new VisualBrush(surface) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds }, null, new Rect(0, 0, 260, 240));
                    var bitmap = new RenderTargetBitmap(520, 480, 192, 192, PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    double x = i % 3 * 300, y = i / 3 * 300;
                    dc.DrawImage(bitmap, new Rect(x + 20, y + 10, 260, 240));
                    dc.DrawText(new FormattedText(labels[i], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.DarkOliveGreen, 1), new Point(x + 20, y + 270));
                }
            }

            Save(sheet, "bite-storyboard.png", 900, 600, 1.5);
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
                    if (now - since < (stage == 1 || stage == 3 ? (c.Idle.Window == null ? HangingMotion.MaxReturnDuration : c.Idle.Window.Motion.ReturnDuration) + .5 : stage == 2 || stage == 4 ? 1 : .25))
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
                        Require(c.Idle.Window.Motion.Fruit.Width == 60 && Math.Abs(c.Idle.Window.Motion.Opacity - .62) < .00001, "Production idle window uses the smaller translucent appearance");
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
