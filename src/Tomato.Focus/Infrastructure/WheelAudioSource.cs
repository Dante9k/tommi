using System;

namespace Tomato
{
    public interface IPcmSource
    {
        void Render(short[] output, double now);
    }

    // One current click, never a queue. Keep the original mono PCM unchanged in both channels.
    public sealed class WheelAudioSource : IPcmSource
    {
        readonly object sync = new object ();
        short[] clip;
        int position;
        double started;
        public void Play(short[] samples, double now)
        {
            lock (sync)
            {
                clip = samples;
                position = 0;
                started = now;
            }
        }

        public void Clear()
        {
            lock (sync)
                clip = null;
        }

        public void Render(short[] output, double now)
        {
            Array.Clear(output, 0, output.Length);
            lock (sync)
            {
                // Drop a delayed start or tail after a driver stall instead of replaying it late.
                if (clip == null)
                    return;
                if (now - started - position / 44100.0 > .025)
                {
                    clip = null;
                    return;
                }

                for (int i = 0; i + 1 < output.Length && position < clip.Length; i += 2)
                    output[i] = output[i + 1] = clip[position++];
                if (position == clip.Length)
                    clip = null;
            }
        }
    }
}
