using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Tomato
{
    // A weighted fruit on an elastic tether in camera space. Position and apparent size
    // share one perspective projection; there is no independent size animation.
    public sealed class BranchReturn
    {
        public const double ReachSeconds = .8;
        public const double SwingSeconds = 4.5;
        public const double FocalLength = 800;
        const double Step = 1.0 / 240;
        const double DefaultGravity = 1700;
        const double Stiffness = 110;
        readonly Point root;
        readonly Rect start, home;
        readonly Vector3D anchor, initial, destination;
        readonly double initialLength, finalLength, gravity;
        readonly Pose[] poses = new Pose[(int)(SwingSeconds / Step) + 1];
        double reelDelay = .42, reelSeconds = 2.15;
        public Rect Bounds { get; private set; }

        public struct Pose
        {
            public Rect Fruit;
            public double Angle, Yaw, Load;
            public Vector3D WorldStem;
        }

        public BranchReturn(Point root, Rect home, Rect start, Rect? screen)
        {
            this.root = root;
            this.home = home;
            this.start = start;
            // Scale the visible drop near the bottom bezel instead of rushing the reel.
            gravity = screen.HasValue ? Math.Min(DefaultGravity, Math.Max(100, (screen.Value.Bottom - start.Y - start.Height * .94) * 6)) : DefaultGravity;
            double far = FocalLength * (start.Width / home.Width - 1);
            anchor = new Vector3D(0, 0, far);
            Vector delta = Stem(start) - root;
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
                // Keep near-edge pickups visible by engaging the reel earlier, while
                // preserving the same continuous force law and perspective projection.
                reelDelay *= .8;
            }

            Rect bounds = new Rect(root, new Size(1, 1));
            foreach (Pose pose in poses)
            {
                var turn = new System.Windows.Media.RotateTransform(pose.Angle, Stem(pose.Fruit).X, Stem(pose.Fruit).Y);
                bounds.Union(turn.TransformBounds(pose.Fruit));
            }

            bounds.Inflate(42, 42);
            Bounds = bounds;
        }

        public Pose Sample(double seconds)
        {
            double index = Math.Max(0, Math.Min(poses.Length - 1, seconds / Step));
            int i = (int)index;
            if (index == 0 || i == poses.Length - 1)
                return poses[i];
            double t = index - i;
            Pose a = poses[i], b = poses[i + 1];
            return Project(a.WorldStem + (b.WorldStem - a.WorldStem) * t, a.Angle + (b.Angle - a.Angle) * t, a.Yaw + (b.Yaw - a.Yaw) * t, a.Load + (b.Load - a.Load) * t);
        }

        Pose Project(Vector3D position, double angle, double yaw, double load)
        {
            double perspective = FocalLength / (FocalLength + position.Z);
            double size = start.Width * perspective;
            Point stem = root + new Vector(position.X, position.Y) * perspective;
            return new Pose
            {
                Fruit = new Rect(stem.X - size * .5, stem.Y - size * .22, size, size),
                WorldStem = position,
                Angle = angle,
                Yaw = yaw,
                Load = load
            };
        }

        void Integrate()
        {
            Vector3D p = initial, v = new Vector3D();
            double tilt = 0, tiltSpeed = 0, yaw = 0;
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
                    double target = Math.Max(-16, Math.Min(16, v.X * .025));
                    tiltSpeed += (48 * (target - tilt) - 11 * tiltSpeed) * Step;
                    tilt += tiltSpeed * Step;
                    yaw += (Math.Max(-18, Math.Min(18, v.Z * .018)) - yaw) * Step * 7;
                }

                // Dissipate the last small residual motion smoothly, without snapping at handoff.
                double settle = Smooth((t - (SwingSeconds - .8)) / .8);
                Vector3D displayed = p + (destination - p) * settle;
                double tension = Tension(t, p, v);
                double load = Math.Min(1.5, tension / gravity) * (1 - Smooth((t - reelDelay) / reelSeconds));
                poses[i] = Project(displayed, tilt * (1 - settle), yaw * (1 - settle), load * (1 - settle));
            }

            poses[0] = Project(initial, 0, 0, 0);
            poses[0].Fruit = start;
            poses[poses.Length - 1] = Project(destination, 0, 0, 0);
            poses[poses.Length - 1].Fruit = home;
        }

        double RestLength(double time)
        {
            // At rest the spring extends by g/k. Compensate so the weighted endpoint
            // rests precisely at the requested perch rather than below it.
            return initialLength + (Math.Max(1, finalLength - gravity / Stiffness) - initialLength) * Smooth((time - reelDelay) / reelSeconds);
        }

        double Tension(double time, Vector3D p, Vector3D v)
        {
            Vector3D offset = p - anchor;
            double length = Math.Max(1, offset.Length);
            Vector3D direction = offset / length;
            return Math.Max(0, Stiffness * (length - RestLength(time)) + 17 * Vector3D.DotProduct(v, direction));
        }

        Vector3D Acceleration(double time, Vector3D p, Vector3D v)
        {
            Vector3D offset = p - anchor;
            Vector3D direction = offset / Math.Max(1, offset.Length);
            double damping = .65 + 3.8 * Smooth((time - reelDelay) / reelSeconds);
            return new Vector3D(0, gravity, 0) - direction * Tension(time, p, v) - v * damping;
        }

        bool Fits(Rect screen)
        {
            foreach (Pose pose in poses)
            {
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
