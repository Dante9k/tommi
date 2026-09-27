using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Threading;
using Tomato;

namespace Tomato.Tests
{
    internal static class WheelVerification
    {
        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        public static void Run(List<string> log)
        {
            var source = new WheelAudioSource();
            var block = new short[440];
            for (int variant = 0; variant < 3; variant++)
                using (var wave = WheelFeedback.CreateClick(variant))
                {
                    wave.Position = 44;
                    var reader = new BinaryReader(wave);
                    var samples = new short[1764];
                    for (int i = 0; i < samples.Length; i++)
                        samples[i] = reader.ReadInt16();
                    source.Play(samples, 0);
                    for (int offset = 0; offset < samples.Length; offset += 220)
                    {
                        source.Render(block, offset / 44100.0);
                        for (int i = 0; i < 220; i++)
                        {
                            short expected = offset + i < samples.Length ? samples[offset + i] : (short)0;
                            Assert(block[i * 2] == expected && block[i * 2 + 1] == expected, "PCM timbre preserved in both channels");
                        }
                    }
                }

            source.Play(new short[] { 100, 100 }, 0);
            source.Play(new short[] { 200, 200 }, .01);
            source.Render(block, .01);
            Assert(block[0] == 200 && block[4] == 0, "Latest click replaces a pending click, no queue");
            source.Render(block, .02);
            Assert(block[0] == 0, "No replay after completion");
            var longClip = new short[1764];
            for (int i = 0; i < longClip.Length; i++)
                longClip[i] = 100;
            source.Play(longClip, 0);
            source.Render(block, .026);
            Assert(block[0] == 0, "Delayed start is discarded");
            source.Play(longClip, 1);
            source.Render(block, 1);
            Assert(block[0] == 100, "Fresh start plays");
            source.Render(block, 1.04);
            Assert(block[0] == 0, "Driver stall discards the remaining tail");
            source.Play(longClip, 2);
            source.Clear();
            source.Render(block, 2);
            Assert(block[0] == 0, "Stop clears pending audio");
            var gate = new FeedbackGate();
            Assert(gate.Accept(0), "First click accepted");
            gate.Reset();
            Assert(gate.Accept(.001), "Reopened editor has no inherited cooldown");
            log.Add("PASS 拨轮PCM音色保持、单声部替换、无积压、延迟起音与停顿尾音丢弃、停止清空及限频重置");
        }

        public static void Smoke(AppController controller)
        {
            var wheel = Find((Visual)controller.Window.Content);
            var peer = UIElementAutomationPeer.CreatePeerForElement(wheel);
            var range = (IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue);
            int clicks = 0, stage = 0, openingWaits = 0;
            wheel.DetentCrossed += delegate
            {
                clicks++;
            };
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            timer.Tick += delegate
            {
                try
                {
                    switch (stage++)
                    {
                        case 0:
                            if (!controller.WheelSound)
                                controller.ToggleWheelSound();
                            wheel.Value = 0;
                            break;
                        case 1:
                            if (!controller.WheelAudioReady && openingWaits++ < 20)
                            {
                                stage--;
                                break;
                            }

                            var feedback = (WheelFeedback)typeof(AppController).GetField("wheelFeedback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(controller);
                            var output = (NativeWaveOutput)typeof(WheelFeedback).GetField("output", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(feedback);
                            Assert(controller.WheelAudioReady, "Editor prewarms the native audio device; visible=" + controller.Window.IsVisible + "; phase=" + controller.Phase + "; enabled=" + controller.WheelSound + "; output=" + (output == null ? "null" : "failed=" + output.Failed + ",stopped=" + output.Stopped));
                            for (int i = 1; i < 20; i++)
                                range.SetValue(i % wheel.Limit);
                            Assert(clicks == 0, "An input burst cannot play before paint");
                            break;
                        case 2:
                            Assert(clicks == 1, "One painted frame coalesces the burst");
                            range.SetValue(1);
                            range.SetValue(19 % wheel.Limit);
                            Assert(clicks == 1, "Reversal before paint stays silent");
                            break;
                        case 3:
                            Assert(clicks == 1, "Returning to the drawn cell emits no phantom click");
                            range.SetValue(3);
                            wheel.Settle();
                            break;
                        case 4:
                            Assert(clicks == 1, "Settle clears pending feedback");
                            range.SetValue(4);
                            wheel.Value = 8;
                            break;
                        case 5:
                            Assert(clicks == 1, "Preset cancels unpainted input feedback");
                            range.SetValue(9);
                            controller.Window.Hide();
                            Assert(!controller.WheelAudioReady, "Hidden editor releases its audio stream");
                            break;
                        case 6:
                            controller.Show();
                            break;
                        case 7:
                            Assert(clicks == 1 && controller.WheelAudioReady, "Reopen stays silent and prewarms audio");
                            controller.ToggleWheelSound();
                            Assert(!controller.WheelAudioReady, "Mute releases audio device");
                            controller.ToggleWheelSound();
                            break;
                        case 8:
                            Assert(controller.WheelAudioReady, "Unmute prewarms before next detent");
                            range.SetValue(10);
                            controller.Start();
                            Assert(!controller.WheelAudioReady, "Focus stops the wheel stream");
                            break;
                        case 9:
                            Assert(clicks == 1, "Starting focus drops unpainted clicks");
                            controller.Cancel();
                            break;
                        case 10:
                            Assert(controller.WheelAudioReady, "Cancel restores a prepared editor");
                            range.SetValue(11);
                            Assert(clicks == 1, "Direct input also waits for paint");
                            break;
                        case 11:
                            Assert(clicks == 2, "Painted direct input emits exactly once");
                            wheel.Value = wheel.Limit - 1;
                            wheel.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.MouseWheelEvent });
                            break;
                        case 12:
                            // Allow the spring to settle before entering the same wrapped value.
                            break;
                        case 13:
                            Assert(clicks == 3 && wheel.Value == 0, "Animated wrap emits once");
                            range.SetValue(0);
                            break;
                        case 14:
                            Assert(clicks == 3, "Same wrapped value emits no phantom detent");
                            timer.Stop();
                            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wheel-sync-results.txt"), "PASS WPF rendered feedback: burst/reversal/settle/preset/hide/reopen/focus; native device: prepare/mute/unmute/release/restore.");
                            controller.Quit();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    Verification.SmokeExitCode = 1;
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wheel-sync-results.txt"), "FAIL stage " + stage + ": " + ex);
                    controller.Quit();
                }
            };
            timer.Start();
        }

        static TimeWheel Find(Visual root)
        {
            var wheel = root as TimeWheel;
            if (wheel != null)
                return wheel;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i) as Visual;
                if (child == null)
                    continue;
                var found = Find(child);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
