using System;
using System.Windows;

namespace Tomato
{
    public static class TomatoAttachment
    {
        // Center of the cut stem face in tomato-cute.png (1254 square), mapped
        // through Art's 396-pixel texture inset in its 440-pixel drawing surface.
        public const double X = (22 + 396 * 684.0 / 1254) / 440;
        public const double Y = (18 + 396 * 162.0 / 1254) / 440;
        public static Point At(Rect fruit)
        {
            return new Point(fruit.X + fruit.Width * X, fruit.Y + fruit.Height * Y);
        }

        public static Vector Incoming(double angle = 0, double yaw = 0, double pitch = 0)
        {
            double x = -.42 * Math.Cos(yaw * Math.PI / 180);
            double y = .9075 * Math.Cos(pitch * Math.PI / 180);
            x += Math.Tan(-yaw * .18 * Math.PI / 180) * y;
            double turn = angle * Math.PI / 180;
            var vector = new Vector(x * Math.Cos(turn) - y * Math.Sin(turn), x * Math.Sin(turn) + y * Math.Cos(turn));
            vector.Normalize();
            return vector;
        }
    }
}
