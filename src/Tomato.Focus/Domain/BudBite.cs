using System;

namespace Tomato
{
    // One absolute-time choreography: approach, bite, hold, then become the stalk.
    public struct BudBite
    {
        public double Presence, Mouth, Merge;
        public static BudBite Sample(double elapsed, double arrival)
        {
            double contact = elapsed - arrival;
            double merge = Ease((contact - .28) / .44);
            return new BudBite
            {
                // Keep the cheeks solid while they narrow. Fade only the last thin
                // remnant, otherwise the plant looks like a ghost over the stem.
                Presence = Ease(elapsed / .12) * (1 - Ease((merge - .78) / .22)),
                Mouth = (.15 + .85 * Ease((elapsed / arrival - .5) / .36)) * (1 - Ease(contact / .18)),
                Merge = merge
            };
        }

        public BudBite Fade(double amount)
        {
            return new BudBite
            {
                Presence = Presence * amount,
                Mouth = Mouth,
                Merge = Merge
            };
        }

        static double Ease(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * t * (10 + t * (-15 + 6 * t));
        }
    }
}
