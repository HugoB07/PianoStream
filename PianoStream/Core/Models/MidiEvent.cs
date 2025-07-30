namespace PianoStream.Core.Models
{
    public class MidiEvent
    {
        public DateTime Timestamp { get; set; }
        public int Command { get; set; }
        public int Channel { get; set; }
        public int Data1 { get; set; }
        public int Data2 { get; set; }
        public int RawMessage { get; set; }

        public MidiEventType EventType => GetEventType();

        public MidiEventType GetEventType()
        {
            return(Command & 0xF0) switch
            {
                0x90 when Data2 > 0 => MidiEventType.NoteOn,
                0x90 when Data2 == 0 => MidiEventType.NoteOff,
                0x80 => MidiEventType.NoteOff,
                0xB0 => MidiEventType.ControlChange,
                _ => MidiEventType.Unknown
            };
        }

        public override string ToString()
        {
            return $"{EventType} - Ch:{Channel} Note:{Data1} Vel:{Data2} @{Timestamp:HH:mm:ss.fff}";
        }
    }

    public enum MidiEventType
    {
        NoteOn,
        NoteOff,
        ControlChange,
        Unknown
    }
}