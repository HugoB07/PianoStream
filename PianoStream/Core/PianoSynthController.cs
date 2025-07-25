using NAudio.Midi;
using NFluidsynth;
using PianoStream.Core.Audio;
using PianoStream.Core.Midi;
using System.Windows;

namespace PianoStream.Core
{
    public class PianoSynthController : IDisposable
    {
        private SynthEngine? _synthEngine;
        private WaveEngine? _waveEngine;
        private SoundFontManager? _soundFontManager;
        private MidiInputHandler? _midiHandler;

        public event Action<string>? OnLog;
        public event Action<MidiInMessageEventArgs>? OnMidiRaw;

        public bool IsInitialized => _synthEngine != null;

        public void Initialize(string soundFontPath, bool noiseCancel, int midiDeviceIndex)
        {
            _synthEngine = new SynthEngine();
            _waveEngine = new WaveEngine();

            // Ensure _synthEngine.Synth is not null before passing it to SoundFontManager  
            if (_synthEngine.Synth == null)
            {
                throw new InvalidOperationException("SynthEngine.Synth is null. Initialization failed.");
            }

            _soundFontManager = new SoundFontManager(_synthEngine.Synth);
            _midiHandler = new MidiInputHandler();

            _synthEngine.SetGain(1.0f);
            _waveEngine.Init(_synthEngine.Synth, noiseCancel);
            _soundFontManager.Load(soundFontPath);

            _midiHandler.MessageReceived += (s, e) =>
            {
                try
                {
                    OnMidiRaw?.Invoke(e);
                    MidiMessageProcessor.Process(e, _synthEngine.Synth); // sécurisé
                }
                catch (FluidSynthInteropException ex)
                {
                    Log("Synth error: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Log("Unhandled MIDI error: " + ex.Message);
                }
            };

            _midiHandler.Start(midiDeviceIndex);
            Log("Synth and MIDI initialized");
        }

        public void SetGain(float gain) => _synthEngine?.SetGain(gain);
        public void SetNoiseCancellation(bool enabled) => _waveEngine?.SetNoiseCancellation(enabled);

        public void Dispose()
        {
            _midiHandler?.Dispose();
            _waveEngine?.Dispose();
            _synthEngine?.Dispose();

            _midiHandler = null;
            _waveEngine = null;
            _synthEngine = null;
            Log("Synth and MIDI disposed");
        }

        private void Log(string msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnLog?.Invoke(msg);
            });
        }
    }
}
