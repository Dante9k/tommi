using System;

namespace Tomato
{
    public sealed class DetentTracker
    {
        int index;
        public void Reset(double position)
        {
            index = (int)Math.Round(position);
        }

        // Coalesce multiple crossed cells in one frame. Never enqueue stale clicks.
        public bool Move(double position)
        {
            int next = (int)Math.Round(position);
            if (next == index)
                return false;
            index = next;
            return true;
        }
    }

    public sealed class FeedbackGate
    {
        double last = double.NegativeInfinity;
        public void Reset()
        {
            last = double.NegativeInfinity;
        }

        public bool Accept(double seconds)
        {
            if (seconds - last < .05)
                return false;
            last = seconds;
            return true;
        }
    }
}
