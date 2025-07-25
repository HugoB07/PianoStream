namespace PianoStream.Core.Models
{
    public class MidiDeviceInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{Index}: {Name}";
        }
    }
}
