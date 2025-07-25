using NAudio.Midi;

namespace PianoStream.Core.Midi
{
    public class MidiInputHandler : IDisposable
    {
        private MidiIn? _midiIn;
        public event EventHandler<MidiInMessageEventArgs>? MessageReceived;

        public void Start(int midiDeviceIndex)
        {
            if(MidiIn.NumberOfDevices == 0)
            {
                throw new InvalidOperationException("No MIDI input devices found.");
            }

            _midiIn = new MidiIn(midiDeviceIndex);
            _midiIn.MessageReceived += (s, e) => MessageReceived?.Invoke(s, e);
            _midiIn.Start();
        }

        public void Dispose()
        {
            _midiIn?.Stop();
            _midiIn?.Dispose();
        }
    }
}
