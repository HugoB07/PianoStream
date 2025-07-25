using Microsoft.Extensions.Logging;
using NAudio.Midi;

namespace PianoStream.Core.Midi
{
    public class MidiInputHandler : IDisposable
    {
        private MidiIn? _midiIn;
        private readonly ILogger<MidiInputHandler>? _logger;

        public event EventHandler<MidiInMessageEventArgs>? MessageReceived;

        public MidiInputHandler(ILogger<MidiInputHandler> logger)
        {
            _logger = logger;
        }

        public void Start(int midiDeviceIndex)
        {
            if(MidiIn.NumberOfDevices == 0)
            {
                _logger?.LogError("No MIDI input devices found. Please connect a MIDI device and try again.");
                return;
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
