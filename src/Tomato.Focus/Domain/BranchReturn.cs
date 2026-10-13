using System;
using System.Windows;

namespace Tomato
{
    // A driven, damped pendulum. The reel changes the tether length; gravity supplies
    // the initial down/right swing. Integrate once at 240Hz, then sample at any display rate.
    public sealed class BranchReturn
    {
        public const double ReachSeconds = .8;
        public const double SwingSeconds = 4.5;
        const double Step = 1.0 / 240;
        const double Gravity = 2000;
        readonly Point root;
        readonly Rect start, home;
        readonly double initialLength, finalLength, initialTheta;
        readonly Pose[] poses = new Pose[1081];
        double reelDelay, reelSeconds, shrinkSeconds;
        public Rect Bounds { get; private set; }

        public struct Pose
        {
            public Rect Fruit;
            public double Angle;
        }

        public BranchReturn(Point root, Rect home, Rect start, Rect? screen)
        {
            this.root = root;
            this.home = home;
            this.start = start;
            Vector displacement = Stem(start) - root;
            initialLength = Math.Max(1, displacement.Length);
            finalLength = Math.Max(1, (Stem(home) - root).Length);
            initialTheta = Math.Atan2(displacement.X, displacement.Y);
            double scale = Math.Sqrt(initialLength / 900);
            // Small pendulums swing faster, so their reel and shrink timings follow sqrt(length).
            reelDelay = Math.Max(.10, Math.Min(.35, .35 * scale));
            reelSeconds = Math.Max(.8, Math.Min(1.9, 2.2 * scale));
            shrinkSeconds = Math.Max(.22, Math.Min(1.2, 1.05 * scale));
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Integrate();
                if (!screen.HasValue || Fits(screen.Value))
                    break;
                // Near an edge the reel catches the weight sooner, rather than clipping or
                // clamping individual frames (which would break position and velocity continuity).
                reelDelay *= .78;
                reelSeconds *= .78;
                shrinkSeconds *= .78;
            }

            Bounds = new Rect(root, new Size(1, 1));
            for (int i = 0; i < poses.Length; i++)
            {
                var rotation = new System.Windows.Media.RotateTransform(poses[i].Angle, Stem(poses[i].Fruit).X, Stem(poses[i].Fruit).Y);
                Rect bounds = Bounds;
                bounds.Union(rotation.TransformBounds(poses[i].Fruit));
                Bounds = bounds;
            }

            Rect expanded = Bounds;
            expanded.Inflate(28, 28);
            Bounds = expanded;
        }

        public Pose Sample(double seconds)
        {
            double index = Math.Max(0, Math.Min(poses.Length - 1, seconds / Step));
            int i = (int)index;
            if (i == poses.Length - 1)
                return poses[i];
            double t = index - i;
            Pose a = poses[i], b = poses[i + 1];
            return new Pose
            {
                Fruit = new Rect(a.Fruit.Location + (b.Fruit.Location - a.Fruit.Location) * t, new Size(a.Fruit.Width + (b.Fruit.Width - a.Fruit.Width) * t, a.Fruit.Height + (b.Fruit.Height - a.Fruit.Height) * t)),
                Angle = a.Angle + (b.Angle - a.Angle) * t
            };
        }

        void Integrate()
        {
            double theta = initialTheta, velocity = 0, tilt = 0, tiltVelocity = 0;
            for (int i = 0; i < poses.Length; i++)
            {
                double t = i * Step;
                if (i > 0)
                {
                    double time = t - Step;
                    double a = Acceleration(time, theta, velocity);
                    double bv = velocity + a * Step / 2;
                    double b = Acceleration(time + Step / 2, theta + velocity * Step / 2, bv);
                    double cv = velocity + b * Step / 2;
                    double c = Acceleration(time + Step / 2, theta + bv * Step / 2, cv);
                    double dv = velocity + c * Step;
                    double d = Acceleration(t, theta + cv * Step, dv);
                    theta += Step / 6 * (velocity + 2 * bv + 2 * cv + dv);
                    velocity += Step / 6 * (a + 2 * b + 2 * c + d);
                    tiltVelocity += (-55 * (tilt + theta * .55) - 12 * tiltVelocity) * Step;
                    tilt += tiltVelocity * Step;
                }

                double length, rate;
                Length(t, out length, out rate);
                double settle = 1 - Smooth((t - (SwingSeconds - .65)) / .65);
                double angle = theta * settle;
                double size = start.Width + (home.Width - start.Width) * Smooth(t / shrinkSeconds);
                Point stem = root + new Vector(length * Math.Sin(angle), length * Math.Cos(angle));
                poses[i] = new Pose
                {
                    Fruit = new Rect(stem.X - size * .5, stem.Y - size * .22, size, size),
                    Angle = tilt * settle * 180 / Math.PI
                };
            }

            poses[0] = new Pose
            {
                Fruit = start,
                Angle = 0
            };
            poses[poses.Length - 1] = new Pose
            {
                Fruit = home,
                Angle = 0
            };
        }

        double Acceleration(double t, double theta, double velocity)
        {
            double length, rate;
            Length(t, out length, out rate);
            // Retraction increases angular velocity (2 L'/L); a damped reel dissipates
            // most of that energy while retaining a visible overshoot near the final perch.
            double damping = .6 + 3 * Smooth(t / (reelDelay + reelSeconds)) + Math.Max(0, -2 * rate / length) * .8;
            return -Gravity / length * Math.Sin(theta) - (2 * rate / length + damping) * velocity;
        }

        void Length(double time, out double length, out double rate)
        {
            double t = Math.Max(0, Math.Min(1, (time - reelDelay) / reelSeconds));
            length = initialLength + (finalLength - initialLength) * Smooth(t);
            rate = (finalLength - initialLength) * 30 * t * t * (1 - t) * (1 - t) / reelSeconds;
        }

        bool Fits(Rect screen)
        {
            // The rounded fruit's visible body is a circle below its stem; transparent
            // texture corners should not force unnecessary acceleration near screen edges.
            for (int i = 0; i < poses.Length; i++)
            {
                Pose pose = poses[i];
                Point stem = Stem(pose.Fruit);
                double angle = pose.Angle * Math.PI / 180, size = pose.Fruit.Width;
                Point center = stem + new Vector(-Math.Sin(angle) * size * .28, Math.Cos(angle) * size * .28);
                double radius = size * .44;
                if (center.X - radius < screen.Left || center.X + radius > screen.Right || center.Y + radius > screen.Bottom)
                    return false;
            }

            return true;
        }

        static Point Stem(Rect fruit)
        {
            return new Point(fruit.X + fruit.Width * .5, fruit.Y + fruit.Height * .22);
        }

        static double Smooth(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * t * (10 + t * (-15 + 6 * t));
        }
    }
}
