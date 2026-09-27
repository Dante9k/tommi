using System;

namespace Tomato
{
    // Bounded polyphony, reusable buffers and no delayed event queue.
    public sealed class EffectMixer : IPcmSource
    {
        public const int SampleRate = 44100, FramesPerBlock = 441, VoiceLimit = 16;
        readonly object sync = new object ();
        readonly Voice[] voices = new Voice[VoiceLimit];
        readonly float[] left = new float[FramesPerBlock], right = new float[FramesPerBlock];
        public int PeakVoices { get; private set; }

        public int ActiveVoices
        {
            get
            {
                lock (sync)
                {
                    int count = 0;
                    foreach (var voice in voices)
                        if (voice != null)
                            count++;
                    return count;
                }
            }
        }

        public bool Add(float[] samples, double gain, double pan, bool impact, double now)
        {
            if (samples == null || samples.Length == 0)
                return false;
            lock (sync)
            {
                int slot = -1;
                for (int i = 0; i < voices.Length; i++)
                    if (voices[i] == null)
                    {
                        slot = i;
                        break;
                    }

                if (slot < 0 && impact)
                    for (int i = 0; i < voices.Length; i++)
                        if (!voices[i].Impact && (slot < 0 || voices[i].Gain < voices[slot].Gain))
                            slot = i;
                if (slot < 0)
                    return false;
                pan = Math.Max(-1, Math.Min(1, pan));
                gain = Math.Max(0, Math.Min(1, gain));
                voices[slot] = new Voice
                {
                    Samples = samples,
                    Gain = gain,
                    Impact = impact,
                    Added = now,
                    Left = gain * Math.Sqrt((1 - pan) / 2),
                    Right = gain * Math.Sqrt((1 + pan) / 2)
                };
                PeakVoices = Math.Max(PeakVoices, ActiveVoices);
                return true;
            }
        }

        public void Clear()
        {
            lock (sync)
                Array.Clear(voices, 0, voices.Length);
        }

        public void Render(short[] output, double now)
        {
            if (output.Length != FramesPerBlock * 2)
                throw new ArgumentException("Expected one stereo audio block.", "output");
            lock (sync)
            {
                Array.Clear(left, 0, left.Length);
                Array.Clear(right, 0, right.Length);
                for (int i = 0; i < voices.Length; i++)
                {
                    var voice = voices[i];
                    if (voice == null)
                        continue;
                    // Device startup or a stalled driver must not replay old contacts later.
                    if (voice.Position == 0 && now - voice.Added > .10)
                    {
                        voices[i] = null;
                        continue;
                    }

                    int length = Math.Min(FramesPerBlock, voice.Samples.Length - voice.Position);
                    for (int j = 0; j < length; j++)
                    {
                        float sample = voice.Samples[voice.Position++];
                        left[j] += (float)(sample * voice.Left);
                        right[j] += (float)(sample * voice.Right);
                    }

                    if (voice.Position >= voice.Samples.Length)
                        voices[i] = null;
                }

                for (int i = 0; i < FramesPerBlock; i++)
                {
                    output[i * 2] = Limit(left[i]);
                    output[i * 2 + 1] = Limit(right[i]);
                }
            }
        }

        static short Limit(double sample)
        {
            return (short)(sample / (1 + Math.Abs(sample)) * 27852);
        }

        sealed class Voice
        {
            public float[] Samples;
            public int Position;
            public double Left, Right, Gain, Added;
            public bool Impact;
        }
    }
}
