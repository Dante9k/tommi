using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Tomato
{
    // A weighted fruit on an elastic tether in camera space. Position and apparent size
    // share one perspective projection; there is no independent size animation.
    public sealed class BranchReturn
    {
        public const double CatchSeconds = .72;
        public const double FocalLength = 800;
        const double Step = 1.0 / 240;
        const double DefaultGravity = 450;
        const double Stiffness = 70;
        readonly Point root;
        readonly Rect start, home;
        readonly Vector3D anchor, initial, destination;
        readonly double initialLength, finalLength;
        double gravity;
        readonly Pose[] poses;
        readonly TravelTiming reel;
        const double ReelDelay = .34;
        public double Duration { get; private set; }
        public Rect Bounds { get; private set; }

        public struct Pose
        {
            public Rect Fruit;
            public double Angle, Yaw, Pitch, Load;
            public Vector3D WorldStem;
        }

        public BranchReturn(Point root, Rect home, Rect start, Rect? screen)
        {
            this.root = root;
            this.home = home;
            this.start = start;
            // Scale the visible drop near the bottom bezel instead of rushing the reel.
            gravity = screen.HasValue ? Math.Min(DefaultGravity, Math.Max(12, (screen.Value.Bottom - start.Y - start.Height * .94) * 1.2)) : DefaultGravity;
            double far = FocalLength * (start.Width / home.Width - 1);
            anchor = new Vector3D(0, 0, far);
            Vector delta = Stem(start) - root;
            double reelSeconds = Math.Max(1.35, Math.Min(2.35, .9 + delta.Length / 1300));
            reel = new TravelTiming(reelSeconds, reelSeconds * .30, reelSeconds * .65);
            int steps = (int)Math.Ceiling((ReelDelay + reelSeconds + 1.35) / Step);
            Duration = steps * Step;
            poses = new Pose[steps + 1];
            initial = new Vector3D(delta.X, delta.Y, 0);
            Vector resting = (Stem(home) - root) * (start.Width / home.Width);
            destination = new Vector3D(resting.X, resting.Y, far);
            initialLength = (initial - anchor).Length;
            finalLength = (destination - anchor).Length;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Integrate();
                if (!screen.HasValue || Fits(screen.Value))
                    break;
                // Reduce sag near a bezel without changing the timing of the reel.
                // Rushing retraction to fit the screen made the pickup look like a jerk.
                gravity *= .8;
            }

            Rect bounds = new Rect(root, new Size(1, 1));
            foreach (Pose pose in poses)
            {
                var turn = new System.Windows.Media.RotateTransform(pose.Angle, Stem(pose.Fruit).X, Stem(pose.Fruit).Y);
                bounds.Union(turn.TransformBounds(pose.Fruit));
            }

            bounds.Inflate(64, 64);
            Bounds = bounds;
        }

        public Pose Sample(double seconds)
        {
            if (seconds >= Duration - 1e-9)
                return poses[poses.Length - 1];
            double index = Math.Max(0, Math.Min(poses.Length - 1, seconds / Step));
            int i = (int)index;
            if (index == 0 || i == poses.Length - 1)
                return poses[i];
            double t = index - i;
            Pose a = poses[i], b = poses[i + 1];
            return Project(a.WorldStem + (b.WorldStem - a.WorldStem) * t, a.Angle + (b.Angle - a.Angle) * t, a.Yaw + (b.Yaw - a.Yaw) * t, a.Pitch + (b.Pitch - a.Pitch) * t, a.Load + (b.Load - a.Load) * t);
        }

        Pose Project(Vector3D position, double angle, double yaw, double pitch, double load)
        {
            double perspective = FocalLength / (FocalLength + position.Z);
            double size = start.Width * perspective;
            Point stem = root + new Vector(position.X, position.Y) * perspective;
            return new Pose
            {
                Fruit = new Rect(stem.X - size * TomatoAttachment.X, stem.Y - size * TomatoAttachment.Y, size, size),
                WorldStem = position,
                Angle = angle,
                Yaw = yaw,
                Pitch = pitch,
                Load = load
            };
        }

        void Integrate()
        {
            Vector3D p = initial, v = new Vector3D();
            double tilt = 0, tiltSpeed = 0, yaw = 0, pitch = 0, pitchSpeed = 0;
            for (int i = 0; i < poses.Length; i++)
            {
                double t = i * Step;
                if (i > 0)
                {
                    double previous = t - Step;
                    Vector3D a = Acceleration(previous, p, v);
                    Vector3D bv = v + a * (Step / 2);
                    Vector3D b = Acceleration(previous + Step / 2, p + v * (Step / 2), bv);
                    Vector3D cv = v + b * (Step / 2);
                    Vector3D c = Acceleration(previous + Step / 2, p + bv * (Step / 2), cv);
                    Vector3D dv = v + c * Step;
                    Vector3D d = Acceleration(t, p + cv * Step, dv);
                    p += (v + 2 * bv + 2 * cv + dv) * (Step / 6);
                    v += (a + 2 * b + 2 * c + d) * (Step / 6);
                    double perspective = FocalLength / (FocalLength + p.Z);
                    double screenSpeed = perspective * (v.X - p.X * v.Z / (FocalLength + p.Z));
                    double target = Math.Max(-23, Math.Min(23, screenSpeed * .065));
                    tiltSpeed += (38 * (target - tilt) - 7 * tiltSpeed) * Step;
                    tilt += tiltSpeed * Step;
                    yaw += (Math.Max(-32, Math.Min(32, v.Z * .032)) - yaw) * Step * 5;
                    double pitchTarget = Math.Max(-8, Math.Min(28, v.Z * .028));
                    pitchSpeed += (32 * (pitchTarget - pitch) - 7 * pitchSpeed) * Step;
                    pitch += pitchSpeed * Step;
                }

                // Dissipate the last small residual motion smoothly, without snapping at handoff.
                double settle = Smooth((t - (Duration - .8)) / .8);
                Vector3D displayed = p + (destination - p) * settle;
                double tension = Tension(t, p, v);
                double load = Math.Min(1.5, tension / gravity) * (1 - reel.Fraction(t - ReelDelay));
                poses[i] = Project(displayed, tilt * (1 - settle), yaw * (1 - settle), pitch * (1 - settle), load * (1 - settle));
            }

            poses[0] = Project(initial, 0, 0, 0, 0);
            poses[0].Fruit = start;
            poses[poses.Length - 1] = Project(destination, 0, 0, 0, 0);
            poses[poses.Length - 1].Fruit = home;
        }

        double RestLength(double time)
        {
            // At rest the spring extends by g/k. Compensate so the weighted endpoint
            // rests precisely at the requested perch rather than below it.
            return initialLength + (Math.Max(1, finalLength - gravity / Stiffness) - initialLength) * reel.Fraction(time - ReelDelay);
        }

        double Tension(double time, Vector3D p, Vector3D v)
        {
            Vector3D offset = p - anchor;
            double length = Math.Max(1, offset.Length);
            Vector3D direction = offset / length;
            return Math.Max(0, Stiffness * (length - RestLength(time)) + 2 * Math.Sqrt(Stiffness) * Vector3D.DotProduct(v, direction));
        }

        Vector3D Acceleration(double time, Vector3D p, Vector3D v)
        {
            Vector3D offset = p - anchor;
            Vector3D direction = offset / Math.Max(1, offset.Length);
            double damping = 2.5 + 4.5 * reel.Fraction(time - ReelDelay);
            return new Vector3D(0, gravity, 0) - direction * Tension(time, p, v) - v * damping;
        }

        bool Fits(Rect screen)
        {
            foreach (Pose pose in poses)
            {
                Point stem = Stem(pose.Fruit);
                double angle = pose.Angle * Math.PI / 180, size = pose.Fruit.Width;
                Vector offset = new Vector(size * (.5 - TomatoAttachment.X), size * (.5 - TomatoAttachment.Y));
                Point center = stem + new Vector(offset.X * Math.Cos(angle) - offset.Y * Math.Sin(angle), offset.X * Math.Sin(angle) + offset.Y * Math.Cos(angle));
                double radius = size * .44;
                if (center.X - radius < screen.Left || center.X + radius > screen.Right || center.Y + radius > screen.Bottom)
                    return false;
            }

            return true;
        }

        static Point Stem(Rect fruit)
        {
            return TomatoAttachment.At(fruit);
        }

        static double Smooth(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * t * (10 + t * (-15 + 6 * t));
        }
    }
}
