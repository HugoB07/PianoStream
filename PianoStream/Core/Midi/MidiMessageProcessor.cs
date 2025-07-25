using NAudio.Midi;
using NFluidsynth;

namespace PianoStream.Core.Midi
{
    public static class MidiMessageProcessor
    {
        public static void Process(MidiInMessageEventArgs e, Synth synth)
        {
            var raw = e.RawMessage;
            int command = raw & 0xF0;
            int channel = raw & 0x0F;
            int data1 = (raw >> 8) & 0xFF;
            int data2 = (raw >> 16) & 0xFF;

            switch (command)
            {
                case 0x90: if (data2 > 0) synth.NoteOn(channel, data1, data2); else synth.NoteOff(channel, data1); break;
                case 0x80: synth.NoteOff(channel, data1); break;
                case 0xB0: synth.CC(channel, data1, data2); break;
            }
        }
    }
}
