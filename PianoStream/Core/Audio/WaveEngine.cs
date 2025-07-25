using NAudio.Wave;
using NFluidsynth;
using PianoStream.Utils;

namespace PianoStream.Core.Audio
{
    public class WaveEngine : IDisposable
    {
        public WaveOutEvent? Output { get; private set; }
        public FluidSynthWaveProvider? Provider { get; private set; }

        public void Init(Synth synth, bool noiseCancel)
        {
            Provider = new FluidSynthWaveProvider(synth, noiseCancel);
            Output = new WaveOutEvent { DesiredLatency = 50 };
            Output.Init(Provider);
            Output.Play();
        }

        public void SetNoiseCancellation(bool enabled)
        {
            if (Provider != null)
            {
                Provider.EnableNoiseCancellation = enabled;
            }
        }

        public void Dispose()
        {
            Output?.Stop();
            Output?.Dispose();
        }
    }
}
