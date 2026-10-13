using System;
using System.Windows;

namespace Tomato
{
    public enum HangingPhase
    {
        Hanging,
        Pulling,
        Rebounding,
        Returning,
        Detaching,
        Complete
    }

    // Absolute, monotonic time makes the trajectory independent of refresh rate and dropped frames.
    public sealed class HangingMotion
    {
        public const double PullThreshold = 96;
        public const double ReturnDuration = BranchReturn.ReachSeconds + BranchReturn.SwingSeconds;
        public const double DetachDuration = .68;
        readonly Rect home;
        readonly Point root;
        Rect from, target;
        readonly Rect? screen;
        BranchReturn returning;
        public Rect ReturnBounds
        {
            get
            {
                return returning == null ? Fruit : returning.Bounds;
            }
        }

        Vector pull, grab;
        double started, fromAngle, fromBranch, fromEditor, fromYaw, fromLoad;
        public HangingPhase Phase { get; private set; }
        public Rect Fruit { get; private set; }
        public Point Root { get; private set; }
        public double Angle { get; private set; }
        public double Branch { get; private set; }
        public double Editor { get; private set; }
        public double Yaw { get; private set; }
        public double Load { get; private set; }

        public bool Animating
        {
            get
            {
                return Phase == HangingPhase.Rebounding || Phase == HangingPhase.Returning || Phase == HangingPhase.Detaching;
            }
        }

        public Point Stem
        {
            get
            {
                return new Point(Fruit.X + Fruit.Width * .5, Fruit.Y + Fruit.Height * .22);
            }
        }

        public HangingMotion(Rect home, Point root, Rect? screen = null)
        {
            this.home = home;
            this.root = root;
            this.screen = screen;
            Fruit = home;
            Root = root;
            Branch = 1;
        }

        public void Return(Rect editor, double time)
        {
            from = Fruit = editor;
            Root = root;
            returning = new BranchReturn(root, home, editor, screen);
            started = time;
            Phase = HangingPhase.Returning;
            Branch = 0;
            Editor = 1;
            Angle = Yaw = Load = 0;
        }

        public void Grab(double time)
        {
            Advance(time);
            if (Phase != HangingPhase.Hanging && Phase != HangingPhase.Rebounding)
                return;
            grab = new Vector(Fruit.X - home.X, Fruit.Y - home.Y);
            Phase = HangingPhase.Pulling;
        }

        public bool Pull(double x, double y)
        {
            if (Phase != HangingPhase.Pulling)
                return false;
            pull = new Vector(Math.Max(-42, Math.Min(42, grab.X + x * .45)), Math.Max(-8, Math.Min(100, grab.Y + y * .72)));
            Fruit = new Rect(home.Location + pull, home.Size);
            Angle = -pull.X * .28;
            return y >= PullThreshold;
        }

        public void Release(double time)
        {
            if (Phase != HangingPhase.Pulling)
                return;
            from = Fruit;
            fromAngle = Angle;
            started = time;
            Phase = HangingPhase.Rebounding;
        }

        public void Detach(Rect editor, double time)
        {
            from = Fruit;
            fromAngle = Angle;
            fromBranch = Branch;
            fromEditor = Editor;
            fromYaw = Yaw;
            fromLoad = Load;
            target = editor;
            started = time;
            Phase = HangingPhase.Detaching;
        }

        public void Advance(double time)
        {
            double elapsed = Math.Max(0, time - started);
            if (Phase == HangingPhase.Rebounding)
            {
                double t = Clamp(elapsed / .72);
                // Released from tension: zero initial velocity, then a damped overshoot.
                double spring = Math.Exp(-6 * t) * (Math.Cos(9 * t) + 6.0 / 9 * Math.Sin(9 * t)) * (1 - Smooth(t));
                Fruit = new Rect(home.Location + (from.Location - home.Location) * spring, home.Size);
                Angle = fromAngle * spring;
                if (t == 1)
                    Phase = HangingPhase.Hanging;
            }
            else if (Phase == HangingPhase.Returning)
            {
                Branch = Smooth(elapsed / BranchReturn.ReachSeconds);
                double swing = Math.Max(0, elapsed - BranchReturn.ReachSeconds);
                var pose = returning.Sample(swing);
                Fruit = pose.Fruit;
                Angle = pose.Angle;
                Yaw = pose.Yaw;
                Load = pose.Load;
                Root = root;
                Editor = 1 - Smooth(swing / .55);
                if (elapsed >= ReturnDuration)
                {
                    Fruit = home;
                    Root = root;
                    Angle = Editor = Yaw = Load = 0;
                    Phase = HangingPhase.Hanging;
                }
            }
            else if (Phase == HangingPhase.Detaching)
            {
                double t = Clamp(elapsed / DetachDuration);
                double growth = Smooth(t);
                Fruit = Mix(from, target, growth);
                Angle = fromAngle * (1 - growth);
                Yaw = fromYaw * (1 - growth);
                Load = fromLoad * (1 - growth);
                Branch = fromBranch * (1 - Smooth(elapsed / .22));
                Editor = fromEditor + (1 - fromEditor) * Smooth((t - .45) / .55);
                if (t == 1)
                    Phase = HangingPhase.Complete;
            }
        }

        static Rect Mix(Rect a, Rect b, double t)
        {
            return new Rect(Mix(a.Location, b.Location, t), new Size(a.Width + (b.Width - a.Width) * t, a.Height + (b.Height - a.Height) * t));
        }

        static Point Mix(Point a, Point b, double t)
        {
            return a + (b - a) * t;
        }

        static double Clamp(double t)
        {
            return Math.Max(0, Math.Min(1, t));
        }

        static double Smooth(double t)
        {
            t = Clamp(t);
            return t * t * t * (10 + t * (-15 + 6 * t));
        }
    }

    public sealed class IdleDeadline
    {
        double last;
        public void Touch(double time)
        {
            last = time;
        }

        public bool Due(double time, bool eligible)
        {
            if (!eligible)
            {
                last = time;
                return false;
            }

            return time - last >= 60;
        }
    }
}
