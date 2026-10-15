using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Tomato
{
    // Grow in camera space, then project position, thickness and leaf size together.
    // A screen-space trim of a finished curve cannot convey an approaching shoot.
    public sealed class VineProjection
    {
        readonly Point root;
        readonly Vector3D a, b, c, d;
        readonly Vector bow;
        readonly double curl;
        readonly Point stem;
        readonly Vector incoming;
        public struct Section
        {
            public Point Center;
            public Vector Tangent;
            public double Scale, Depth, Width;
        }

        public VineProjection(Point root, Point stem, double fruitSize, double load, double angle = 0, double yaw = 0, double pitch = 0)
        {
            this.root = root;
            this.stem = stem;
            incoming = TomatoAttachment.Incoming(angle, yaw, pitch);
            Vector span = stem - root;
            double slack = 1 / (1 + Math.Max(0, span.Length - 54) / 90);
            double foreground = Math.Max(0, Math.Min(1, (fruitSize - HangingMotion.RestSize) / (220 - HangingMotion.RestSize)));
            // An arched shoot comes forward above the fruit, then curls down onto its stem.
            // Under load it straightens, rather than translating a rigid diagonal line.
            double arch = foreground / (1 + load * .6);
            Vector normal = new Vector(span.Y, -span.X);
            if (normal.Length > .001)
                normal.Normalize();
            bow = normal * (Math.Min(80, span.Length * .14) * arch);
            curl = Math.Min(550, Math.Max(0, stem.Y - root.Y - 20) * 2.2) * arch;
            Point bend1 = root + new Vector(-32 * slack - Math.Abs(span.X) * .12 * arch, Math.Max(16, span.Y * (.28 + .30 * arch)) + load * 12);
            Point bend2 = stem + new Vector(-span.X * .015 * arch - 23 * slack, -Math.Max(16, Math.Min(180, span.Length * .32)) * arch - 16 * (1 - arch));
            bend2.Y = Math.Max(root.Y + 12, bend2.Y);
            double far = BranchReturn.FocalLength * (220 / HangingMotion.RestSize - 1);
            double near = BranchReturn.FocalLength * (220 / fruitSize - 1);
            a = Lift(root, far);
            b = Lift(bend1, far * .98 + near * .02);
            // The forward bow crosses the fruit's depth plane before bending back to touch it.
            c = Lift(bend2, near - 650 * arch);
            d = Lift(stem, near);
        }

        Vector3D Lift(Point point, double depth)
        {
            double inverseScale = 1 + depth / BranchReturn.FocalLength;
            return new Vector3D((point.X - root.X) * inverseScale, (point.Y - root.Y) * inverseScale, depth);
        }

        public Section Sample(double position)
        {
            double t = Math.Max(0, Math.Min(1, position)), s = 1 - t;
            Vector3D p = a * (s * s * s) + b * (3 * s * s * t) + c * (3 * s * t * t) + d * (t * t * t);
            Vector3D v = (b - a) * (3 * s * s) + (c - b) * (6 * s * t) + (d - c) * (3 * t * t);
            double perspective = BranchReturn.FocalLength / (BranchReturn.FocalLength + p.Z);
            double curve = Math.Sin(Math.PI * t);
            double bend = curve * curve;
            double bendSpeed = Math.PI * Math.Sin(2 * Math.PI * t);
            double hook = curl * Math.Pow(t, 6) * (1 - t);
            double hookSpeed = curl * Math.Pow(t, 5) * (6 - 7 * t);
            Point center = root + new Vector(p.X, p.Y) * perspective + bow * bend + new Vector(0, -hook);
            Vector tangent = new Vector(v.X - p.X * v.Z / (BranchReturn.FocalLength + p.Z), v.Y - p.Y * v.Z / (BranchReturn.FocalLength + p.Z)) * perspective + bow * bendSpeed + new Vector(0, -hookSpeed);
            // Blend the final shoot into the fruit's actual stem axis, with continuous
            // position and velocity at both ends. The shared cap is also the fruit pivot.
            double u = Math.Max(0, (t - .78) / .22);
            double blend = u * u * (3 - 2 * u);
            double blendSpeed = 6 * u * (1 - u) / .22;
            Vector endVelocity = incoming * Math.Max(60, Math.Min(240, (stem - root).Length * .4));
            Point aligned = stem + endVelocity * (t - 1);
            tangent = tangent * (1 - blend) + endVelocity * blend + (aligned - center) * blendSpeed;
            center += (aligned - center) * blend;
            return new Section
            {
                // The flexible shoot bows out under its own weight. Express the bow in
                // projected units so it remains legible across different screen spans.
                Center = center,
                Tangent = tangent,
                Scale = 220.0 / 76 * perspective,
                Width = 1 - .24 * blend,
                Depth = p.Z
            };
        }
    }
}
