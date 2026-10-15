using System;
using System.Windows;

namespace Tomato
{
    // Integrate a smooth velocity envelope: accelerate, travel, then brake.
    // This controls distance, rather than easing a non-uniform Bezier parameter.
    public sealed class TravelTiming
    {
        readonly double duration, accelerate, brake, area;
        public TravelTiming(double duration, double accelerate, double brake)
        {
            this.duration = duration;
            double fit = Math.Min(1, duration / (accelerate + brake));
            this.accelerate = accelerate * fit;
            this.brake = brake * fit;
            area = duration - (this.accelerate + this.brake) / 2;
        }

        public double Fraction(double seconds)
        {
            if (seconds <= 0)
                return 0;
            if (seconds >= duration - 1e-9)
                return 1;
            if (seconds < accelerate)
            {
                double u = seconds / accelerate;
                return accelerate * (u * u * u - .5 * u * u * u * u) / area;
            }

            double cruise = duration - accelerate - brake;
            if (seconds <= duration - brake)
                return (accelerate / 2 + seconds - accelerate) / area;
            double v = (seconds - duration + brake) / brake;
            return (accelerate / 2 + cruise + brake * (v - v * v * v + .5 * v * v * v * v)) / area;
        }
    }

    public sealed class VineReach
    {
        const int Samples = 512;
        readonly double[] distances = new double[Samples + 1];
        readonly TravelTiming timing;
        public double Length { get; private set; }
        public double Duration { get; private set; }

        public VineReach(VineProjection vine)
        {
            Point previous = vine.Sample(0).Center;
            for (int i = 1; i <= Samples; i++)
            {
                Point point = vine.Sample(i / (double)Samples).Center;
                distances[i] = distances[i - 1] + (point - previous).Length;
                previous = point;
            }

            Length = distances[Samples];
            Duration = Math.Max(.55, Math.Min(3.2, Length / 1300 + .22));
            timing = new TravelTiming(Duration, Duration * .30, Duration * .65);
        }

        public double Progress(double seconds)
        {
            double fraction = timing.Fraction(seconds);
            if (fraction == 0 || fraction == 1)
                return fraction;
            double distance = Length * fraction;
            int low = 0, high = Samples;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (distances[middle] < distance)
                    low = middle;
                else
                    high = middle;
            }

            double blend = (distance - distances[low]) / Math.Max(.000001, distances[high] - distances[low]);
            return (low + blend) / Samples;
        }
    }
}
