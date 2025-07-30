using System.Text.Json.Serialization;

namespace PianoStream.Core.Models
{
    public class Recording
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public TimeSpan Duration { get; set; }
        public string SoundFontUsed { get; set;} = string.Empty;
        public List<MidiEvent> Events { get; set; } = new();

        [JsonIgnore]
        public string FormattedDuration =>
            $"{Duration.Minutes:D2}:{Duration.Seconds:D2}.{Duration.Milliseconds:D3}";

        [JsonIgnore]
        public string FormattedCreatedAt => CreatedAt.ToString("dd/MM/yyyy HH:mm");
    }
}
