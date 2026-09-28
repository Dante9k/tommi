namespace Tomato
{
    public sealed class Preferences
    {
        public string Language;
        public int Seconds = 1500;
        public long DeadlineTicks;
        public bool Active;
        public bool Sound = true;
        public bool? WheelSound;
        public bool? EffectsSound;
        public bool Haptics = true;
        public bool LaunchAtLogin = true;
        public bool LoginStartupInitialized;
        public bool WheelSoundEnabled
        {
            get
            {
                return WheelSound ?? Sound;
            }
        }

        public double Left = double.NaN;
        public double Top = double.NaN;
    }
}
