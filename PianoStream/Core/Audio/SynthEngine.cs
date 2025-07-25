using NFluidsynth;

namespace PianoStream.Core.Audio
{
    public class SynthEngine : IDisposable
    {
        public Synth? Synth { get; private set; }
        public Settings? Settings { get; private set; }

        public SynthEngine()
        {
            Settings = new Settings();
            Synth = new Synth(Settings);
        }

        public void SetGain(float value)
        {
            if (Synth != null)
            {
                Synth.Gain = value; // Ensure Synth is not null before accessing Gain
            }
        }

        public void Dispose()
        {
            Synth?.Dispose();
        }
    }
}
