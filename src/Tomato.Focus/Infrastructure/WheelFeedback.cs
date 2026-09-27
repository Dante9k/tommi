using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Tomato
{
    public sealed class WheelFeedback : IDisposable
    {
        readonly FeedbackGate gate = new FeedbackGate();
        readonly OptionalHaptics haptics = new OptionalHaptics();
        readonly short[][] clicks = new short[3][];
        readonly WheelAudioSource source = new WheelAudioSource();
        NativeWaveOutput output;
        int nextClick;
        public bool AudioReady
        {
            get
            {
                return output != null && output.Ready;
            }
        }

        public bool HapticsAvailable
        {
            get
            {
                return haptics.Available;
            }
        }

        public WheelFeedback()
        {
            for (int i = 0; i < clicks.Length; i++)
                using (var wave = CreateClick(i))
                {
                    wave.Position = 44;
                    var reader = new BinaryReader(wave);
                    clicks[i] = new short[(wave.Length - 44) / 2];
                    for (int n = 0; n < clicks[i].Length; n++)
                        clicks[i][n] = reader.ReadInt16();
                }
        }

        public void Prepare(bool enabled)
        {
            if (!enabled)
            {
                Stop();
                return;
            }

            // Prewarm while the editor is visible: two 5ms buffers, no per-click device startup.
            if (output == null)
                output = new NativeWaveOutput(source, uint.MaxValue, 220, 2);
        }

        public void Tick(bool sound, bool vibrate)
        {
            double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            if (!gate.Accept(now))
                return;
            // A device still opening or unavailable drops the click; it must never catch up later.
            if (sound && AudioReady)
            {
                source.Play(clicks[nextClick], now);
                nextClick = (nextClick + 1) % clicks.Length;
            }

            if (vibrate)
                haptics.Step();
        }

        public void Stop()
        {
            source.Clear();
            if (output != null)
                output.Dispose();
            output = null;
            gate.Reset();
            haptics.Stop();
        }

        public void Dispose()
        {
            Stop();
        }

        public static MemoryStream CreateClick()
        {
            return CreateClick(1);
        }

        public static MemoryStream CreateClick(int variant)
        {
            if (variant < 0 || variant > 2)
                throw new ArgumentOutOfRangeException("variant");
            const int rate = 44100, count = 1764;
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(rate);
            writer.Write(rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(count * 2);
            var random = new Random(120 + variant);
            double filtered = 0, previous = 0;
            double tuning = 1 + (variant - 1) * .018;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)rate;
                double noise = random.NextDouble() * 2 - 1;
                filtered += .34 * (noise - filtered);
                double contact = (filtered - previous) * Math.Exp(-t / .0018);
                previous = filtered;
                // A short, pitch-falling body and two dry contacts resemble a small damped detent.
                // All partials decay quickly: no sustained sine beep or long metallic ring.
                double body = .62 * Math.Sin(2 * Math.PI * tuning * (510 * t - 2600 * t * t)) * Math.Exp(-t / .0048);
                double shell = .19 * Math.Sin(2 * Math.PI * 1120 * tuning * t + .25) * Math.Exp(-t / .0024);
                double seatTime = t - .006;
                double seat = seatTime < 0 ? 0 : .14 * Math.Sin(2 * Math.PI * 760 * tuning * seatTime) * Math.Exp(-seatTime / .002);
                double attack = Math.Min(1, t / .00023);
                double tail = Math.Min(1, (count - 1 - i) / 176.0);
                double sample = (body + shell + .48 * contact + seat) * attack * tail;
                writer.Write((short)(sample * (7600 + variant * 140)));
            }

            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }

    // Late-bound WinRT keeps .NET Framework 4.8 and older Windows installations supported.
    // All calls originate on the WPF input thread. Missing APIs/devices are a normal no-op.
    public sealed class OptionalHaptics
    {
        object manager;
        MethodInfo send, stop;
        ushort waveform;
        bool checkedSupport;
        public bool Available
        {
            get
            {
                EnsureSupport();
                return manager != null;
            }
        }

        static Type WinRT(string name)
        {
            return Type.GetType(name + ", Windows, ContentType=WindowsRuntime", false);
        }

        void EnsureSupport()
        {
            if (checkedSupport)
                return;
            checkedSupport = true;
            try
            {
                var metadata = WinRT("Windows.Foundation.Metadata.ApiInformation");
                if (metadata == null || !(bool)metadata.GetMethod("IsTypePresent", new[] { typeof(string) }).Invoke(null, new object[] { "Windows.Devices.Haptics.InputHapticsManager" }))
                    return;
                var type = WinRT("Windows.Devices.Haptics.InputHapticsManager");
                if (type == null || !(bool)type.GetMethod("IsSupported").Invoke(null, null))
                    return;
                var waves = WinRT("Windows.Devices.Haptics.KnownSimpleHapticsControllerWaveforms");
                var step = waves == null ? null : waves.GetProperty("Step");
                if (step == null)
                    return;
                waveform = (ushort)step.GetValue(null, null);
                send = type.GetMethod("TrySendHapticWaveformForPlayCount"); // Public WinRT method name, not-a-secret.
                stop = type.GetMethod("TryStopFeedback");
                if (send == null || stop == null)
                    return;
                manager = type.GetMethod("GetForCurrentThread").Invoke(null, null);
            }
            catch (Exception)
            {
                manager = null;
            }
        }

        public void Step()
        {
            EnsureSupport();
            if (manager == null)
                return;
            try
            {
                send.Invoke(manager, new object[] { waveform, (ushort)0, .25, (uint)1, TimeSpan.Zero });
            }
            catch (Exception)
            {
                manager = null;
            }
        }

        public void Stop()
        {
            if (manager == null)
                return;
            try
            {
                stop.Invoke(manager, null);
            }
            catch (Exception)
            {
                manager = null;
            }
        }
    }
}
