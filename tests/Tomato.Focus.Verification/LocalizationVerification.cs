using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Tomato;

namespace Tomato.Tests
{
    internal static class LocalizationVerification
    {
        static void Assert(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }

        public static void Run(List<string> log)
        {
            string original = Texts.Language;
            string folder = Path.Combine(Path.GetTempPath(), "Tommi-language-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                Assert(Texts.Resolve(null, new CultureInfo("en-US")) == "en", "Legacy settings follow English Windows");
                Assert(Texts.Resolve(null, new CultureInfo("zh-TW")) == "zh-CN", "Chinese Windows selects Chinese");
                Assert(Texts.Resolve("invalid", new CultureInfo("de-DE")) == "en", "Unsupported languages safely fall back to English");
                Assert(Texts.Resolve("en", new CultureInfo("zh-CN")) == "en", "Explicit English overrides Windows");
                Assert(Texts.Resolve("zh-CN", new CultureInfo("en-US")) == "zh-CN", "Explicit Chinese overrides Windows");
                foreach (string key in Texts.Keys)
                {
                    Texts.SetLanguage("en");
                    string english = Texts.Get(key);
                    Assert(!string.IsNullOrWhiteSpace(english) && !Regex.IsMatch(english, "[\\u4e00-\\u9fff]"), "Missing English translation: " + key);
                    Texts.SetLanguage("zh-CN");
                    string chinese = Texts.Get(key);
                    Assert(!string.IsNullOrWhiteSpace(chinese), "Missing Chinese translation: " + key);
                    Assert(Regex.Matches(english, "\\{[0-9]+\\}").Count == Regex.Matches(chinese, "\\{[0-9]+\\}").Count, "Placeholder mismatch: " + key);
                }

                string path = Path.Combine(folder, "state.xml");
                File.WriteAllText(path, "<Preferences><Seconds>90</Seconds><DeadlineTicks>123456</DeadlineTicks><Active>true</Active><Sound>false</Sound><Left>21</Left><Top>32</Top></Preferences>");
                var store = new StateStore(path);
                var state = store.Read();
                Assert(state.Language == null, "Legacy XML has no forced language");
                state.Language = "en";
                Assert(store.Write(state), "Language preference saved");
                var restored = store.Read();
                Assert(restored.Language == "en" && restored.Seconds == 90 && restored.DeadlineTicks == 123456 && restored.Active && !restored.WheelSoundEnabled && restored.Left == 21 && restored.Top == 32, "Language persistence preserves timer, sound and position");
                log.Add("PASS 中英资源完整性、占位符、系统语言选择、显式覆盖、旧设置兼容与语言持久化");
            }
            finally
            {
                Texts.SetLanguage(original);
                Directory.Delete(folder, true);
            }
        }

        static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i)))
                    yield return child;
        }

        static void CheckEnglish(FrameworkElement root)
        {
            foreach (var element in Descendants(root))
            {
                var text = element as TextBlock;
                if (text != null && text.Text != "简体中文")
                    Assert(!Regex.IsMatch(text.Text, "[\\u4e00-\\u9fff]"), "Untranslated visible text: " + text.Text);
                string accessible = AutomationProperties.GetName(element);
                if (accessible != "简体中文")
                    Assert(!Regex.IsMatch(accessible, "[\\u4e00-\\u9fff]"), "Untranslated accessibility name");
            }
        }

        static Button Button(FrameworkElement root, string name)
        {
            foreach (var element in Descendants(root))
            {
                var button = element as Button;
                if (button != null && button.Name == name)
                    return button;
            }

            throw new Exception("Missing button: " + name);
        }

        static void Click(FrameworkElement root, string name)
        {
            root.UpdateLayout();
            Button(root, name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        public static void Render(AppController controller)
        {
            string original = controller.Language;
            foreach (string language in new[]
            {
                "en",
                "zh-CN"
            }

            )
            {
                controller.SetLanguage(language);
                var panel = new SettingsWindow(controller, Art.TomatoImage(112));
                var root = (FrameworkElement)panel.Content;
                root.Measure(new Size(panel.Width, double.PositiveInfinity));
                double height = root.DesiredSize.Height;
                root.Arrange(new Rect(0, 0, panel.Width, height));
                root.UpdateLayout();
                if (language == "en")
                    CheckEnglish(root);
                Art.Save(root, (int)panel.Width, (int)Math.Ceiling(height), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-" + language + ".png"));
                var widget = (FrameworkElement)controller.Window.Content;
                widget.UpdateLayout();
                if (language == "en")
                    CheckEnglish(widget);
                Art.Save(widget, 250, 250, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alarm-" + language + ".png"));
                panel.Close();
            }

            controller.SetLanguage(original);
        }

        public static void Smoke(AppController controller)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            var state = new StateStore(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-state.xml"));
            int stage = 0;
            long deadline = 0;
            TomatoWindow window = controller.Window;
            timer.Tick += delegate
            {
                try
                {
                    switch (stage++)
                    {
                        case 0:
                            controller.SetLanguage("zh-CN");
                            window.Duration = 60;
                            controller.Start();
                            deadline = state.Read().DeadlineTicks;
                            controller.OpenSettings();
                            break;
                        case 1:
                            var panel = controller.Settings;
                            Assert(panel != null, "Settings remain open");
                            Click((FrameworkElement)panel.Content, "LanguageEnglish");
                            panel.UpdateLayout();
                            Assert(controller.Settings == panel && panel.IsVisible && panel.Title == "Tommi · Preferences", "Language changes in the same settings window");
                            CheckEnglish((FrameworkElement)panel.Content);
                            Assert(controller.Window == window && controller.Phase == TimerPhase.Running && state.Read().DeadlineTicks == deadline && state.Read().Language == "en", "Switch preserves the live timer and saves English");
                            Click((FrameworkElement)panel.Content, "LanguageChinese");
                            Assert(panel.Title == "Tommi · 专注偏好" && state.Read().Language == "zh-CN", "Chinese applies immediately");
                            Click((FrameworkElement)panel.Content, "LanguageSystem");
                            Assert(controller.Language == "system", "System language selection persists");
                            panel.Close();
                            break;
                        case 2:
                            window.Hide();
                            controller.SetLanguage("en");
                            Assert(!window.IsVisible && controller.Phase == TimerPhase.Running && state.Read().DeadlineTicks == deadline, "Switching while hidden preserves visibility and deadline");
                            controller.Show();
                            controller.Cancel();
                            controller.Preview();
                            break;
                        case 3:
                            var surface = controller.Surface;
                            Assert(controller.IsThrowing, "Preview is active");
                            controller.SetLanguage("zh-CN");
                            controller.SetLanguage("en");
                            Assert(controller.IsThrowing && controller.Surface == surface && window.AlarmMode, "Switching during reminder preserves animation");
                            CheckEnglish((FrameworkElement)window.Content);
                            controller.StopAlarm();
                            timer.Stop();
                            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language-smoke-results.txt"), "PASS live English/Chinese/System buttons, same settings/window instances, saved language/deadline, hidden timer, and active reminder preserved.");
                            controller.Quit();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    Verification.SmokeExitCode = 1;
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language-smoke-results.txt"), "FAIL stage " + stage + ": " + ex);
                    controller.Quit();
                }
            };
            timer.Start();
        }
    }
}
