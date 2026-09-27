using Tomato;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Tomato.Tests
{
    internal static class Verification
    {
        internal static int SmokeExitCode;
        static string Output(string name)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        public static int Run()
        {
            var lines = new List<string>();
            try
            {
                StartupVerification.Run();
                lines.Add("PASS 登录启动默认值、显式开关、升级路径、外部移除、失败恢复及路径限制；不修改系统启动项");
                var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var clock = new Countdown();
                bool rejected = false;
                try
                {
                    clock.Start(0, now);
                }
                catch (ArgumentOutOfRangeException)
                {
                    rejected = true;
                }

                Assert(rejected, "零时长必须拒绝");
                lines.Add("PASS 零时长保护");
                rejected = false;
                try
                {
                    clock.Start(86400, now);
                }
                catch (ArgumentOutOfRangeException)
                {
                    rejected = true;
                }

                Assert(rejected, "24 小时上界");
                lines.Add("PASS 时长上界");
                clock.Start(1500, now);
                Assert(clock.Remaining(now.AddMilliseconds(1)) == 1500, "向上取整");
                Assert(!clock.Tick(now.AddSeconds(1499.9)), "不得提前响铃");
                Assert(clock.Tick(now.AddSeconds(1500)), "到期触发");
                Assert(!clock.Tick(now.AddSeconds(1501)), "响铃仅触发一次");
                lines.Add("PASS 截止时刻及单次触发");
                clock.Start(5, now);
                Assert(clock.Tick(now.AddHours(2)), "休眠唤醒");
                lines.Add("PASS 休眠跨越截止时间");
                clock.Restore(1500, now.AddMinutes(25), now.AddMinutes(2));
                Assert(clock.Phase == TimerPhase.Running && clock.Remaining(now.AddMinutes(2)) == 1380, "重启恢复");
                clock.Restore(1500, now, now.AddHours(1));
                Assert(clock.Phase == TimerPhase.Ringing, "重启时过期");
                clock.Stop();
                Assert(clock.Phase == TimerPhase.Editing, "取消");
                lines.Add("PASS 未到期 / 已到期恢复及取消");
                string testPath = Output("verification-state.xml");
                var store = new StateStore(testPath);
                var pref = new Preferences
                {
                    Seconds = 3599,
                    Active = true,
                    DeadlineTicks = now.Ticks,
                    Left = -900,
                    Top = 100
                };
                Assert(store.Write(pref), "保存失败");
                var restored = store.Read();
                Assert(restored.Seconds == 3599 && restored.Active && restored.Left == -900, "保存恢复不一致");
                pref.Seconds = 42;
                Assert(store.Write(pref) && store.Read().Seconds == 42, "原子覆盖");
                File.WriteAllText(testPath, "broken xml");
                Assert(store.Read().Seconds == 1500, "损坏文件恢复");
                lines.Add("PASS 设置保存、覆盖及损坏恢复");
                File.WriteAllText(testPath, "<Preferences><Seconds>1260</Seconds><Sound>false</Sound><Active>true</Active><DeadlineTicks>123456</DeadlineTicks></Preferences>");
                restored = store.Read();
                Assert(restored.LaunchAtLogin && !restored.LoginStartupInitialized, "旧设置的登录启动默认值");
                restored.LaunchAtLogin = false;
                restored.LoginStartupInitialized = true;
                Assert(!restored.WheelSoundEnabled && restored.Active && restored.DeadlineTicks == 123456, "旧静音及计时迁移");
                restored.WheelSound = true;
                restored.EffectsSound = true;
                Assert(store.Write(restored) && store.Read().WheelSoundEnabled && !store.Read().Sound, "独立拨轮音效设置");
                Assert(store.Read().EffectsSound == true && !store.Read().Sound, "独立投掷音效设置");
                Assert(!store.Read().LaunchAtLogin && store.Read().LoginStartupInitialized, "登录启动关闭偏好持久化");
                File.WriteAllText(testPath, "<Preferences><Sound>true</Sound></Preferences>");
                Assert(store.Read().WheelSoundEnabled, "旧有声设置迁移");
                Assert(store.Read().EffectsSound == null && (store.Read().EffectsSound ?? store.Read().Sound), "旧有声设置默认启用投掷音效");
                lines.Add("PASS 旧设置兼容与音效独立保存");
                var shake = new ShakeDetector();
                shake.Reset(0, 0);
                Assert(!shake.Move(25, 0, 0) && !shake.Move(0, 0, .2) && !shake.Move(25, 0, .4) && shake.Move(0, 0, .6), "水平摇晃");
                Assert(!shake.Move(25, 0, .7), "每次拖动只触发一次");
                shake.Reset(-100, -100);
                Assert(!shake.Move(-100, -75, 0) && !shake.Move(-100, -100, .2) && !shake.Move(-100, -75, .4) && shake.Move(-100, -100, .6), "垂直摇晃和负坐标");
                shake.Reset(0, 0);
                for (int i = 0; i < 50; i++)
                    Assert(!shake.Move(i * 5, i, i * .02), "正常拖动不取消");
                shake.Reset(0, 0);
                for (int i = 0; i < 50; i++)
                    Assert(!shake.Move(i % 2 * 19, 0, i * .02), "微小抖动不取消");
                shake.Reset(0, 0);
                for (int i = 0; i < 10; i++)
                    Assert(!shake.Move(i % 2 * 25, 0, i * .6), "缓慢往返不取消");
                shake.Reset(0, 0);
                shake.Move(25, 0, 0);
                shake.Move(0, 0, .1);
                shake.Move(25, 0, .2);
                shake.Reset(25, 0);
                Assert(!shake.Move(0, 0, .3), "松手后不继承反向次数");
                lines.Add("PASS 摇晃方向、幅度、时限、单次触发与重置");
                var detents = new DetentTracker();
                detents.Reset(59);
                Assert(!detents.Move(59.4) && detents.Move(59.6) && !detents.Move(60), "可见数字边界及环绕");
                Assert(detents.Move(65) && !detents.Move(65), "高速跨格合并");
                detents.Reset(-1);
                Assert(detents.Move(-.4) && !detents.Move(0), "反向环绕");
                var gate = new FeedbackGate();
                Assert(gate.Accept(0) && !gate.Accept(.01) && gate.Accept(.051) && !gate.Accept(.052) && gate.Accept(1), "音效限频、不补播");
                for (int variant = 0; variant < 3; variant++)
                    using (var wave = WheelFeedback.CreateClick(variant))
                    {
                        Assert(wave.Length == 44 + 1764 * 2, "40ms PCM音效");
                        var reader = new BinaryReader(wave);
                        wave.Position = 44;
                        double sum = 0, energy = 0;
                        int samples = 0;
                        while (wave.Position < wave.Length)
                        {
                            int sample = reader.ReadInt16();
                            Assert(Math.Abs(sample) < 8000, "音效无削波且控制峰值");
                            if (samples == 0 || samples == 1763)
                                Assert(sample == 0, "音效首尾无断点");
                            if (samples > 1543)
                                Assert(Math.Abs(sample) < 30, "尾音及时衰减");
                            sum += sample;
                            energy += sample * sample;
                            samples++;
                        }

                        Assert(Math.Abs(sum / samples) < 100, "音效无明显直流偏移");
                        Assert(Math.Sqrt(energy / samples) > 300 && Math.Sqrt(energy / samples) < 2000, "有效的轻量卡点响度");
                    }

                double reference = 0, referenceVelocity = 0;
                WheelMotion.Advance(ref reference, ref referenceVelocity, 1, .4);
                foreach (int fps in new[]
                {
                    30,
                    60,
                    120
                }

                )
                {
                    double position = 0, velocity = 0;
                    int detentCount = 0;
                    var tracker = new DetentTracker();
                    tracker.Reset(0);
                    for (int i = 0; i < fps * 2 / 5; i++)
                    {
                        WheelMotion.Advance(ref position, ref velocity, 1, 1.0 / fps);
                        if (tracker.Move(position))
                            detentCount++;
                        Assert(position >= 0 && position < 1.025, "吸附轻微回落不能跳到相邻格");
                    }

                    Assert(Math.Abs(position - reference) < .000001 && Math.Abs(velocity - referenceVelocity) < .000001, "不同帧率运动一致");
                    Assert(detentCount == 1, "弹性停靠不重复触发卡点");
                    WheelMotion.Advance(ref position, ref velocity, -3, 1);
                    Assert(Math.Abs(position + 3) < .00001, "快速反向后收敛到目标");
                }

                lines.Add("PASS 滚筒惯性在30/60/120Hz一致，停靠不多响，快速反向收敛");
                new OptionalHaptics().Stop();
                lines.Add("PASS 逐格反馈、限频、PCM音效和可选震动安全降级");
                var flight = new Flight
                {
                    X = 600,
                    Y = 120,
                    Vx = -300,
                    Vy = -250,
                    Size = 90,
                    Spin = 220
                };
                double expectedX = 600 - 300 * .5;
                for (int i = 0; i < 50; i++)
                    flight.Step(.01, 1920, 1080);
                Assert(Math.Abs(flight.X - expectedX) < .0001, "水平轨迹");
                Assert(Math.Abs(flight.Y - (120 - 250 * .5 + .5 * 920 * .25)) < .001, "重力轨迹");
                for (int i = 0; i < 200; i++)
                    flight.Step(.01, 1920, 1080);
                Assert(flight.Bounces == 1, "落地反弹");
                Assert(!flight.Step(7, 1920, 1080), "寿命回收");
                lines.Add("PASS 抛物线、反弹与粒子回收");
                EffectVerification.Run(lines);
                WheelVerification.Run(lines);
                var wheel = new TimeWheel(60, "秒");
                int feedbackCount = 0;
                wheel.DetentCrossed += delegate
                {
                    feedbackCount++;
                };
                wheel.Value = 60;
                Assert(wheel.Value == 0, "向上环绕");
                wheel.Value = -1;
                Assert(wheel.Value == 59, "向下环绕");
                wheel.Value = 25;
                wheel.Settle();
                Assert(wheel.Value == 25, "吸附");
                Assert(feedbackCount == 0, "恢复/预设/吸附不能播放拨轮声");
                lines.Add("PASS 时间滚轮循环与吸附");
                var texture = Art.TomatoImage(192);
                var surface = new ThrowSurface(texture)
                {
                    Origin = new Point(1660, 160)
                };
                surface.Measure(new Size(1920, 1080));
                surface.Arrange(new Rect(0, 0, 1920, 1080));
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 60 * 120; i++)
                {
                    surface.Advance(1.0 / 60, 1920, 1080);
                    if (i % 60 == 0)
                        surface.Draw();
                }

                watch.Stop();
                Assert(surface.PeakParticles <= 36 && surface.PeakParticles > 0, "粒子数量");
                lines.Add("PASS 120 秒动画模拟，峰值 " + surface.PeakParticles + " 个，模拟耗时 " + watch.ElapsedMilliseconds + " ms");
                lines.Add("全部验证通过。 " + DateTime.UtcNow.ToString("o"));
                File.WriteAllLines(Output("test-results.txt"), lines, System.Text.Encoding.UTF8);
                return 0;
            }
            catch (Exception ex)
            {
                lines.Add("FAIL " + ex);
                File.WriteAllLines(Output("test-results.txt"), lines, System.Text.Encoding.UTF8);
                return 1;
            }
        }

        public static int Render()
        {
            try
            {
                var app = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                new StateStore(Output("preview-state.xml")).Write(new Preferences { Left = 120, Top = 90 });
                var controller = new AppController(app, Output("preview-state.xml"), new StartupVerification.FakeRegistration());
                Assert(controller.Window.Left == 150 && controller.Window.Top == 90 && controller.Window.ExpandedLeft == 120, "兼容旧250像素窗口的右上角位置");
                controller.ToggleLaunchAtLogin();
                controller.Window.Duration = 1500;
                double width = controller.Window.Width, height = controller.Window.Height;
                var root = (FrameworkElement)controller.Window.Content;
                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();
                Art.Save(root, (int)Math.Ceiling(width), (int)Math.Ceiling(height), Output("tomato-widget.png"));
                Assert(FindNamed(root, "Units") == null, "不再显示时分秒标签");
                SaveAudioPreview();
                EffectVerification.RenderPreview(Output("throw-impacts.wav"));
                Assert(width == 220 && height == 220, "220 DIP待机界面");
                var wheelOrigin = FindWheel(root).TranslatePoint(new Point(), root);
                Assert(Math.Abs(wheelOrigin.X - 53 * .88) < 1, "拨轮布局与点击坐标同步缩放");
                foreach (double scale in new[]
                {
                    1.25,
                    1.5,
                    2.0
                }

                )
                {
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using (var file = File.Create(Output("tomato-widget-" + (int)(scale * 100) + ".png")))
                        encoder.Save(file);
                }

                var iconPng = new System.Windows.Media.Imaging.PngBitmapEncoder();
                iconPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(Art.TomatoImage(256)));
                using (var bytes = new MemoryStream())
                {
                    iconPng.Save(bytes);
                    byte[] payload = bytes.ToArray();
                    using (var writer = new BinaryWriter(File.Create(Output("Tomato.ico"))))
                    {
                        writer.Write((short)0);
                        writer.Write((short)1);
                        writer.Write((short)1);
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                        writer.Write((short)1);
                        writer.Write((short)32);
                        writer.Write(payload.Length);
                        writer.Write(22);
                        writer.Write(payload);
                    }
                }

                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Art.Brush("#EAE9DF"), null, new Rect(0, 0, 1440, 960));
                    dc.DrawRectangle(Art.Brush("#F5F4ED"), null, new Rect(30, 30, 1380, 900));
                    Art.Text(dc, "Tommi", 34, Art.Brush("#243C30"), 87, 79, "Microsoft YaHei UI", true);
                    Art.Text(dc, "T O M M I   /   F O C U S", 11, Art.Brush("#648069"), 88, 135, "Segoe UI", false);
                    Art.Text(dc, "让时间，", 64, Art.Brush("#233D2E"), 87, 281, "Microsoft YaHei UI", true);
                    Art.Text(dc, "慢慢成熟。", 64, Art.Brush("#233D2E"), 87, 365, "Microsoft YaHei UI", true);
                    Art.Text(dc, "一颗番茄，一段完整的专注。", 17, Art.Brush("#657665"), 93, 480, "Microsoft YaHei UI", false);
                    Art.Text(dc, "滚动设置时间。双击绿蒂，然后进入心流。", 14, Art.Brush("#7E8B79"), 93, 520, "Microsoft YaHei UI", false);
                    dc.PushTransform(new TranslateTransform(853, 248));
                    dc.PushTransform(new ScaleTransform(1.22, 1.22));
                    dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, width, height));
                    dc.Pop();
                    dc.Pop();
                    dc.DrawLine(new Pen(Art.Brush("#D8DED1"), 1), new Point(90, 726), new Point(1347, 726));
                    string[] ns =
                    {
                        "01",
                        "02",
                        "03"
                    };
                    string[] titles =
                    {
                        "设定节奏",
                        "进入专注",
                        "收获休息"
                    };
                    string[] desc =
                    {
                        "时 · 分 · 秒，轻滑即定",
                        "双击绿蒂，原位显示倒计时",
                        "番茄跃入桌面，提醒适时停下"
                    };
                    for (int i = 0; i < 3; i++)
                    {
                        double x = 92 + i * 427;
                        Art.Text(dc, ns[i], 12, Art.Brush("#C3553C"), x, 770, "Segoe UI", true);
                        Art.Text(dc, titles[i], 19, Art.Brush("#284333"), x + 38, 764, "Microsoft YaHei UI", true);
                        Art.Text(dc, desc[i], 13, Art.Brush("#7D8977"), x + 38, 809, "Microsoft YaHei UI", false);
                    }

                    Art.Text(dc, "DESKTOP EDITION     /     1.1", 10, Art.Brush("#899580"), 92, 887, "Segoe UI", false);
                    Art.Text(dc, "专注有时，休息有声。", 11, Art.Brush("#899580"), 1210, 883, "Microsoft YaHei UI", false);
                }

                Art.Save(visual, 1440, 960, Output("preview.png"));
                foreach (int remaining in new[]
                {
                    3661,
                    3600,
                    3599,
                    60,
                    59,
                    0
                }

                )
                {
                    controller.Window.ShowCountdown(remaining);
                    AssertCountdown(controller.Window, remaining);
                    Assert(controller.Window.Duration == 1500, "倒计时显示不能覆盖原预设时间");
                }

                controller.Window.ShowCountdown(1499);
                Assert(controller.Window.Width == 125 && controller.Window.Height == 125, "专注窗口缩为一半且命中区域同步缩小");
                Assert(Math.Abs(FindNamed(root, "FruitArtwork").Opacity - .32) < .001, "专注果实32%不透明度");
                Assert(controller.Window.Topmost && FindNamed(root, "CountdownReadout").Opacity == 1, "置顶并保持读数不透明");
                root.Measure(new Size(125, 125));
                root.Arrange(new Rect(0, 0, 125, 125));
                root.UpdateLayout();
                Art.Save(root, 125, 125, Output("tomato-countdown.png"));
                var contrastPreview = new DrawingVisual();
                using (var dc = contrastPreview.RenderOpen())
                {
                    dc.DrawRectangle(Art.Brush("#F5F4ED"), null, new Rect(0, 0, 290, 290));
                    dc.DrawRectangle(Art.Brush("#1C2925"), null, new Rect(290, 0, 290, 290));
                    dc.DrawRectangle(new VisualBrush(root), null, new Rect(82, 82, 125, 125));
                    dc.DrawRectangle(new VisualBrush(root), null, new Rect(372, 82, 125, 125));
                }

                Art.Save(contrastPreview, 580, 290, Output("tomato-focus-backgrounds.png"));
                var glyphSheet = new DrawingVisual();
                using (var dc = glyphSheet.RenderOpen())
                {
                    dc.DrawRectangle(Art.Brush("#27382F"), null, new Rect(0, 0, 580, 130));
                    for (int i = 0; i < 11; i++)
                    {
                        var glyph = new RecessedGlyph
                        {
                            Text = i == 10 ? ":" : i.ToString(),
                            FontSize = 40,
                            Width = 24
                        };
                        glyph.Measure(new Size(24, 56));
                        glyph.Arrange(new Rect(0, 0, 24, 56));
                        glyph.UpdateLayout();
                        dc.DrawRectangle(new VisualBrush(glyph) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 24, 56), Stretch = Stretch.Fill }, null, new Rect(21 + i * 49, 20, 36, 84));
                    }
                }

                Art.Save(glyphSheet, 580, 130, Output("recessed-glyphs.png"));
                controller.Window.ShowEditor();
                Assert(controller.Window.Width == 220 && controller.Window.Height == 220 && FindNamed(root, "FruitArtwork").Opacity == 1, "取消恢复紧凑尺寸与原色");
                Assert(FindWheel(root).IsEnabled && FindWheel(root).Visibility == Visibility.Visible, "取消恢复编辑");
                Assert(FindNamed(root, "TimeEditorFrame").Visibility == Visibility.Visible && FindNamed(root, "TomatoMenu").Visibility == Visibility.Visible, "取消后恢复编辑底板和菜单");
                Assert(FindNamed(root, "CountdownReadout").Visibility == Visibility.Hidden, "取消隐藏倒计时");
                controller.Window.SetAlarm(true);
                Assert(controller.Window.Width == 250 && controller.Window.Height == 250, "提醒保持250 DIP");
                width = height = 250;
                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();
                Art.Save(root, (int)Math.Ceiling(width), (int)Math.Ceiling(height), Output("tomato-alarm.png"));
                Assert(FindNamed(root, "CountdownReadout").Visibility == Visibility.Hidden, "提醒隐藏倒计时");
                var settings = new SettingsWindow(controller, Art.TomatoImage(112));
                var settingsRoot = (FrameworkElement)settings.Content;
                settingsRoot.Measure(new Size(352, double.PositiveInfinity));
                var settingsSize = new Size(352, settingsRoot.DesiredSize.Height);
                settingsRoot.Arrange(new Rect(new Point(), settingsSize));
                settingsRoot.UpdateLayout();
                Art.Save(settingsRoot, 352, (int)Math.Ceiling(settingsSize.Height), Output("settings-panel.png"));
                var effects = (System.Windows.Controls.Primitives.ToggleButton)FindNamed(settingsRoot, "ToggleEffects");
                bool wasEnabled = controller.EffectsSound;
                effects.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert(controller.EffectsSound != wasEnabled && effects.IsChecked == controller.EffectsSound, "现代面板声音开关与持久化偏好同步");
                effects.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert(controller.EffectsSound == wasEnabled, "面板开关可恢复原值");
                var startup = (System.Windows.Controls.Primitives.ToggleButton)FindNamed(settingsRoot, "ToggleStartup");
                Assert(startup.IsEnabled && startup.IsChecked == true, "设置面板显示登录启动状态");
                startup.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert(!controller.LaunchAtLogin && !new StateStore(Output("preview-state.xml")).Read().LaunchAtLogin, "设置面板关闭登录启动并保存");
                Assert(FindNamed(settingsRoot, "Preset1500") != null && FindNamed(settingsRoot, "QuitTomato") != null, "面板保留预设和退出入口");
                settings.Close();
                File.WriteAllText(Output("render-results.txt"), "PASS WPF editor/countdown/alarm renders; read-only countdown; hour/minute rollover; preset preserved; editor restored");
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Output("render-results.txt"), ex.ToString());
                return 1;
            }
        }

        public static void SettingsSmoke(AppController controller)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            int stage = 0;
            timer.Tick += delegate
            {
                try
                {
                    if (stage == 0)
                    {
                        controller.Window.Duration = 600;
                        controller.Start();
                        controller.OpenSettings();
                    }
                    else if (stage == 1)
                    {
                        Assert(controller.Settings != null && controller.Settings.IsVisible && controller.Settings.Topmost, "托盘入口打开置顶配置面板");
                        var root = (FrameworkElement)controller.Settings.Content;
                        Assert(!FindNamed(root, "PreviewThrow").IsEnabled && FindNamed(root, "CancelFocus").IsEnabled, "专注时预览禁用、取消可用");
                        bool before = controller.Sound;
                        var toggle = (System.Windows.Controls.Primitives.ToggleButton)FindNamed(root, "ToggleSound");
                        toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        Assert(controller.Sound != before && controller.Settings.IsVisible, "设置后面板保持打开并保存偏好");
                        toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        var preset = (System.Windows.Controls.Button)FindNamed(root, "Preset300");
                        preset.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        Assert(controller.Settings == null && controller.Window.Duration == 300 && controller.Phase == TimerPhase.Editing, "预设关闭面板并恢复相应时长");
                    }
                    else if (stage == 2)
                    {
                        controller.OpenSettings();
                        var panel = controller.Settings;
                        panel.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(panel), Environment.TickCount, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
                        Assert(controller.Settings == null, "Esc关闭并清理面板引用");
                    }
                    else
                    {
                        controller.OpenSettings();
                        var quit = (System.Windows.Controls.Button)FindNamed((FrameworkElement)controller.Settings.Content, "QuitTomato");
                        timer.Stop();
                        quit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        Assert(controller.Exiting && controller.Settings == null, "退出按钮清理面板与应用");
                        File.WriteAllText(Output("settings-smoke-results.txt"), "PASS settings popup display/topmost; running state controls; sound toggle/persistence; preset; Esc dismissal; reopen; normal exit");
                    }

                    stage++;
                }
                catch (Exception ex)
                {
                    SmokeExitCode = 1;
                    timer.Stop();
                    File.WriteAllText(Output("settings-smoke-results.txt"), "FAIL " + ex);
                    controller.Quit();
                }
            };
            timer.Start();
        }

        public static void FocusSmoke(AppController controller)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(650)
            };
            var root = (FrameworkElement)controller.Window.Content;
            int stage = 0;
            bool intermediate = false;
            double anchoredRight = 0, anchoredTop = 0, maxAnchorDrift = 0;
            string anchorDiagnostic = "";
            EventHandler sample = delegate
            {
                double opacity = FindNamed(root, "FruitArtwork").Opacity;
                Rect visible = root.TransformToAncestor(controller.Window).TransformBounds(new Rect(root.RenderSize));
                intermediate |= visible.Width > 126 && visible.Width < 249 && opacity > .321 && opacity < .999;
                maxAnchorDrift = Math.Max(maxAnchorDrift, Math.Abs(controller.Window.Left + visible.Right - anchoredRight));
                maxAnchorDrift = Math.Max(maxAnchorDrift, Math.Abs(controller.Window.Top + visible.Top - anchoredTop));
                if (Math.Abs(controller.Window.Left + visible.Right - anchoredRight) > 1.5)
                    anchorDiagnostic = " left=" + controller.Window.Left + " window=" + controller.Window.Width + " root=" + root.ActualWidth + " rect=" + visible + " anchor=" + anchoredRight;
            };
            CompositionTarget.Rendering += sample;
            controller.Window.Left = SystemParameters.WorkArea.Left + 24;
            controller.Window.Top = SystemParameters.WorkArea.Top + 220;
            anchoredRight = controller.Window.Left + controller.Window.Width;
            anchoredTop = controller.Window.Top;
            controller.Window.Duration = 60;
            controller.Start();
            timer.Tick += delegate
            {
                try
                {
                    if (stage == 0 || stage == 1)
                    {
                        Assert(Math.Abs(controller.Window.ActualWidth - 125) < 1 && Math.Abs(controller.Window.ActualHeight - 125) < 1, "实际窗口与内容缩为一半");
                        Assert(Math.Abs(root.ActualWidth - 125) < 1 && Math.Abs(root.ActualHeight - 125) < 1, "内容没有保留原尺寸的透明占位");
                        Assert(controller.Window.Topmost && Math.Abs(FindNamed(root, "FruitArtwork").Opacity - .32) < .001, "专注透明与置顶");
                        Assert(!SystemParameters.ClientAreaAnimation || intermediate, "同步缩小与淡出中间帧");
                        Assert(maxAnchorDrift < 1.5, "缩放全过程右上角固定，阶段=" + stage + "，最大偏差=" + maxAnchorDrift + anchorDiagnostic);
                        Assert(Math.Abs(controller.Window.Left + controller.Window.Width - anchoredRight) < 1.5, "缩小完成右边界保持不动");
                        Assert(Math.Abs(new StateStore(Output("smoke-state.xml")).Read().Left - (anchoredRight - 250)) < 1.5, "持久化展开坐标，重启不会重复偏移");
                        if (stage == 0)
                        {
                            controller.Window.Hide();
                            controller.Show();
                            Assert(controller.Window.IsVisible && controller.Phase == TimerPhase.Running, "隐藏恢复不重置计时");
                            controller.Cancel();
                            controller.Start();
                        }
                        else
                            controller.Cancel();
                    }
                    else
                    {
                        Assert(Math.Abs(controller.Window.Width - 220) < 1 && Math.Abs(controller.Window.Height - 220) < 1 && controller.Window.Topmost, "取消后恢复220 DIP且置顶");
                        Assert(FindNamed(root, "FruitArtwork").Opacity == 1 && controller.Window.Duration == 60, "恢复原色并保留预设");
                        Assert(Math.Abs(controller.Window.Left - (anchoredRight - 220)) < 1.5 && maxAnchorDrift < 1.5, "反向恢复仍固定右上角并回到原位");
                        controller.Start();
                        controller.Window.SetAlarm(true);
                        Assert(controller.Window.AlarmMode && Math.Abs(controller.Window.Left + controller.Window.Width - anchoredRight) < 1.5, "提醒打断缩小动画且保留原处锚点；left=" + controller.Window.Left + ", width=" + controller.Window.Width + ", anchor=" + anchoredRight);
                        controller.Cancel();
                        timer.Stop();
                        CompositionTarget.Rendering -= sample;
                        File.WriteAllText(Output("focus-smoke-results.txt"), "PASS top-right anchored compositor animation (max drift " + maxAnchorDrift.ToString("F3") + " DIP); stable saved expanded position; 125 DIP native/content bounds; synchronized intermediate frames; 32% artwork opacity; topmost; hide/show; rapid reversal; cancel restores size/color/preset; alarm interrupts transition");
                        controller.Quit();
                    }

                    stage++;
                }
                catch (Exception ex)
                {
                    SmokeExitCode = 1;
                    timer.Stop();
                    CompositionTarget.Rendering -= sample;
                    File.WriteAllText(Output("focus-smoke-results.txt"), "FAIL " + ex);
                    controller.Quit();
                }
            };
            timer.Start();
        }

        public static void Smoke(AppController controller)
        {
            // This suite invokes actions directly; desktop clicks must not dismiss its alarm mid-check.
            controller.Window.IsHitTestVisible = false;
            if (!controller.EffectsSound)
                controller.ToggleEffectsSound();
            var watch = Stopwatch.StartNew();
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            int stage = -2;
            int mutedLaunches = 0;
            bool sawFocusTransition = false;
            bool sawAlarmTransition = false;
            double alarmRight = 0, alarmTop = 0, alarmDrift = 0;
            var log = new List<string>();
            var transitionSamples = new List<string>();
            EventHandler sampleTransition = delegate
            {
                if (stage == 2 && controller.IsThrowing)
                {
                    var content = (FrameworkElement)controller.Window.Content;
                    Rect visible = content.TransformToAncestor(controller.Window).TransformBounds(new Rect(content.RenderSize));
                    sawAlarmTransition |= visible.Width > 126 && visible.Width < 249;
                    alarmDrift = Math.Max(alarmDrift, Math.Abs(controller.Window.Left + visible.Right - alarmRight));
                    alarmDrift = Math.Max(alarmDrift, Math.Abs(controller.Window.Top + visible.Top - alarmTop));
                }

                if (stage != 1)
                    return;
                var root = (FrameworkElement)controller.Window.Content;
                double size = root.ActualWidth * root.RenderTransform.Value.M11;
                double opacity = FindNamed((FrameworkElement)controller.Window.Content, "FruitArtwork").Opacity;
                if (transitionSamples.Count < 30)
                    transitionSamples.Add(size.ToString("F2") + "/" + opacity.ToString("F3"));
                sawFocusTransition |= size > 126 && size < 249 && opacity > .321 && opacity < .999;
            };
            CompositionTarget.Rendering += sampleTransition;
            var inputWheel = FindWheel((System.Windows.Media.Visual)controller.Window.Content);
            Assert(inputWheel != null, "真实时间拨轮存在");
            int clicks = 0;
            inputWheel.DetentCrossed += delegate
            {
                clicks++;
            };
            timer.Tick += delegate
            {
                try
                {
                    double elapsed = watch.Elapsed.TotalSeconds;
                    if (stage == 1 && controller.Window.Width > 125 && controller.Window.Width < 250)
                    {
                        double opacity = FindNamed((FrameworkElement)controller.Window.Content, "FruitArtwork").Opacity;
                        sawFocusTransition |= opacity > .32 && opacity < 1;
                    }

                    if (stage == -2 && elapsed > .15)
                    {
                        inputWheel.Value = inputWheel.Limit - 1;
                        inputWheel.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.MouseWheelEvent });
                        if (SystemParameters.ClientAreaAnimation)
                            Assert(clicks == 0, "反馈不能提前于可见数字变化");
                        stage++;
                    }
                    else if (stage == -1 && elapsed > .75)
                    {
                        Assert(inputWheel.Value == 0 && clicks == 1, "真实WPF滚轮动画跨界反馈一次");
                        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(inputWheel);
                        var range = (System.Windows.Automation.Provider.IRangeValueProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.RangeValue);
                        range.SetValue(5);
                        Assert(clicks == 1 && inputWheel.Value == 5, "手动数值输入在绘制前保持安静");
                        log.Add("PASS 实际拨轮事件、环绕动画、逐格反馈与数值输入");
                        stage++;
                    }
                    else if (stage == 0 && elapsed > 1.1)
                    {
                        Assert(clicks == 2, "手动输入在真实绘制时反馈一次");
                        controller.Window.Duration = 2;
                        controller.Start();
                        Assert(controller.Phase == TimerPhase.Running && controller.Window.IsVisible, "启动后原位显示");
                        AssertCountdown(controller.Window, 2);
                        controller.Start();
                        Assert(controller.Window.IsVisible && controller.Window.Duration == 2, "重复开始不能隐藏或修改预设");
                        log.Add("PASS 启动后原位显示只读倒计时，重复开始不重置");
                        stage++;
                    }
                    else if (stage == 1 && elapsed > 2.4)
                    {
                        AssertCountdown(controller.Window, 1);
                        Assert(Math.Abs(controller.Window.Width - 125) < 1 && Math.Abs(controller.Window.Height - 125) < 1 && controller.Window.Topmost, "专注缩为125 DIP并置顶，实际=" + controller.Window.Width + "×" + controller.Window.Height + "，置顶=" + controller.Window.Topmost);
                        Assert(!SystemParameters.ClientAreaAnimation || sawFocusTransition, "缩小与透明动画同步存在中间帧：" + string.Join(", ", transitionSamples));
                        Assert(Math.Abs(FindNamed((FrameworkElement)controller.Window.Content, "FruitArtwork").Opacity - .32) < .001, "透明动画到达目标值");
                        log.Add("PASS 同步缩小与淡出、125 DIP窗口、32%果实不透明度与置顶");
                        log.Add("PASS 原编辑区数字随真实计时递减");
                        // Move the focused miniature away from both its initial position and the screen corner.
                        controller.Window.Left = SystemParameters.WorkArea.Left + 380;
                        controller.Window.Top = SystemParameters.WorkArea.Top + 240;
                        controller.ClampWindow();
                        controller.Save();
                        alarmRight = controller.Window.Left + controller.Window.Width;
                        alarmTop = controller.Window.Top;
                        stage++;
                    }
                    else if (stage == 2 && elapsed > 4)
                    {
                        Assert(controller.IsThrowing && controller.Window.IsVisible && controller.Window.AlarmMode, "到期提醒");
                        Assert(Math.Abs(controller.Window.Width - 250) < 1 && Math.Abs(controller.Window.Height - 250) < 1, "到时原处恢复尺寸，实际=" + controller.Window.Width + "×" + controller.Window.Height);
                        Assert(alarmDrift < 1.5 && Math.Abs(controller.Window.Left + controller.Window.Width - alarmRight) < 1.5 && Math.Abs(controller.Window.Top - alarmTop) < 1.5, "到时保持移动后的位置，不跳回屏幕角落");
                        Assert(!SystemParameters.ClientAreaAnimation || sawAlarmTransition, "到时平滑恢复而非瞬间放大");
                        Assert(controller.EffectsAudioReady && controller.Surface.LaunchCues > 0, "投出事件与真实音频设备就绪");
                        log.Add("PASS 到期自动显示番茄与动画");
                        stage++;
                    }
                    else if (stage == 3 && elapsed > 8)
                    {
                        Assert(controller.Surface.Frames > 20, "动画渲染");
                        Point emitter = controller.Surface.PointToScreen(controller.Surface.Origin);
                        Point stem = controller.Window.PointToScreen(controller.Window.ThrowOrigin);
                        Assert((emitter - stem).Length < 2, "发射原点跟随番茄实际果蒂位置");
                        log.Add("PASS 移动后的当前位置到时提醒、原处平滑恢复和真实果蒂发射起点");
                        Assert(controller.Surface.LaunchCues > 5 && controller.Surface.ImpactCues > 0, "投掷与真实碰撞均产生音效");
                        log.Add("PASS 真实音频输出就绪，投出事件=" + controller.Surface.LaunchCues + "，碰撞事件=" + controller.Surface.ImpactCues);
                        log.Add("PASS 实际动画帧=" + controller.Surface.Frames + "，平均帧间隔=" + controller.Surface.MeanFrameMs.ToString("F2") + " ms，粒子峰值=" + controller.Surface.PeakParticles + "，渲染层级=" + controller.Surface.RenderTier + "，最大帧间隔=" + controller.Surface.MaxFrameMs.ToString("F2") + " ms，每帧应用更新=" + (controller.Surface.UpdateMs / controller.Surface.Frames).ToString("F3") + " ms");
                        controller.StopAlarmForDrag();
                        Assert(!controller.IsThrowing && controller.Phase == TimerPhase.Editing, "拖动停止");
                        Assert(!controller.EffectsAudioActive, "拖动停止同时回收音频输出");
                        log.Add("PASS 拖动停止路径与动画回收");
                        controller.Preview();
                        stage++;
                    }
                    else if (stage == 4 && elapsed > 9)
                    {
                        Assert(controller.EffectsAudioReady, "预览重新打开音效");
                        controller.ToggleEffectsSound();
                        Assert(!controller.EffectsAudioActive, "静音立即停止输出");
                        mutedLaunches = controller.Surface.LaunchCues;
                        stage++;
                    }
                    else if (stage == 5 && elapsed > 9.7)
                    {
                        Assert(controller.Surface.LaunchCues > mutedLaunches, "静音不影响动画");
                        controller.ToggleEffectsSound();
                        stage++;
                    }
                    else if (stage == 6 && elapsed > 10.3)
                    {
                        Assert(controller.EffectsAudioReady, "重新启用音效设备");
                        log.Add("PASS 预览音效、静音即时收声、继续动画与恢复音效");
                        stage++;
                    }
                    else if (stage == 7 && elapsed > 17)
                    {
                        Assert(!controller.IsThrowing, "预览自动停止");
                        Assert(!controller.EffectsAudioActive, "预览结束清空音频");
                        log.Add("PASS 8 秒预览自动结束");
                        controller.Window.Duration = 1500;
                        controller.Start();
                        controller.Show();
                        AssertCountdown(controller.Window, 1500);
                        Assert(controller.Phase == TimerPhase.Running && controller.Window.IsVisible && !inputWheel.IsEnabled, "显示计时并锁定拨轮");
                        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(inputWheel);
                        var range = (System.Windows.Automation.Provider.IRangeValueProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.RangeValue);
                        Assert(range.IsReadOnly, "辅助输入也必须只读");
                        controller.Cancel();
                        Assert(controller.Window.IsVisible && controller.Phase == TimerPhase.Editing, "托盘取消");
                        Assert(controller.Window.Duration == 1500 && inputWheel.IsEnabled && inputWheel.IsVisible, "取消保留预设并恢复拨轮");
                        Assert(FindNamed((FrameworkElement)controller.Window.Content, "CountdownReadout").Visibility == Visibility.Hidden, "取消隐藏读数");
                        log.Add("PASS 取消与重新编辑");
                        stage++;
                    }
                    else if (stage == 8 && elapsed > 17.7)
                    {
                        Assert(Math.Abs(controller.Window.Width - 220) < 1 && Math.Abs(controller.Window.Height - 220) < 1 && controller.Window.Topmost, "快速开始取消后动画恢复紧凑尺寸和置顶");
                        timer.Stop();
                        CompositionTarget.Rendering -= sampleTransition;
                        File.WriteAllLines(Output("smoke-results.txt"), log, System.Text.Encoding.UTF8);
                        controller.Quit();
                    }
                }
                catch (Exception ex)
                {
                    SmokeExitCode = 1;
                    timer.Stop();
                    CompositionTarget.Rendering -= sampleTransition;
                    log.Add("FAIL " + ex);
                    File.WriteAllLines(Output("smoke-results.txt"), log, System.Text.Encoding.UTF8);
                    controller.Quit();
                }
            };
            timer.Start();
        }

        static TimeWheel FindWheel(Visual parent)
        {
            var wheel = parent as TimeWheel;
            if (wheel != null)
                return wheel;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i) as Visual;
                if (child == null)
                    continue;
                var found = FindWheel(child);
                if (found != null)
                    return found;
            }

            return null;
        }

        static FrameworkElement FindNamed(Visual parent, string name)
        {
            var element = parent as FrameworkElement;
            if (element != null && element.Name == name)
                return element;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i) as Visual;
                if (child == null)
                    continue;
                var found = FindNamed(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        static void SaveAudioPreview()
        {
            const int rate = 44100;
            var audio = new short[rate * 3];
            double[] times =
            {
                .15,
                .5,
                .85,
                1.2,
                1.42,
                1.6,
                1.75,
                1.88,
                1.99,
                2.09,
                2.18,
                2.26,
                2.33,
                2.39
            };
            for (int i = 0; i < times.Length; i++)
                using (var click = WheelFeedback.CreateClick(i % 3))
                {
                    click.Position = 44;
                    var reader = new BinaryReader(click);
                    int offset = (int)(times[i] * rate);
                    for (int j = 0; j < 1764; j++)
                        audio[offset + j] = reader.ReadInt16();
                }

            using (var writer = new BinaryWriter(File.Create(Output("wheel-detents.wav"))))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + audio.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(audio.Length * 2);
                foreach (short sample in audio)
                    writer.Write(sample);
            }
        }

        static void AssertCountdown(TomatoWindow window, int remaining)
        {
            var root = (FrameworkElement)window.Content;
            var panel = (System.Windows.Controls.StackPanel)FindNamed(root, "CountdownReadout");
            Assert(panel.Visibility == Visibility.Visible && !panel.IsHitTestVisible, "编辑区显示不可交互的倒计时");
            Assert(panel.Effect == null, "镂空光线由字形内部绘制，不使用整层外发光滤镜");
            Assert(panel.Children[0].Visibility == (remaining >= 3600 ? Visibility.Visible : Visibility.Collapsed), "小时按需显示以减少视觉拥挤");
            string expected = TimeSpan.FromSeconds(remaining).ToString(@"hh\:mm\:ss");
            var values = new List<string>();
            foreach (RecessedGlyph text in panel.Children)
                values.Add(text.Text);
            Assert(string.Concat(values.ToArray()) == expected, "倒计时数字不正确");
            Assert(FindNamed(root, "TimeEditorFrame").Visibility == Visibility.Hidden && FindNamed(root, "TomatoMenu").Visibility == Visibility.Hidden, "专注仅显示时间，无底板边框或菜单装饰");
            Assert(!FindWheel(root).IsEnabled && ((UIElement)FindWheel(root).Parent).Visibility == Visibility.Hidden, "计时锁定并隐藏原拨轮");
        }
    }
}
