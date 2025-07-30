using Microsoft.Extensions.Logging;
using NAudio.Midi;
using PianoStream.Core.Models;
using System.IO;
using System.Text.Json;

namespace PianoStream.Core.Services
{
    public class RecordingService
    {
        private readonly ILogger<RecordingService> _logger;
        private readonly string _recordingsPath;

        private Recording? _currentRecording;
        private DateTime _recordingStartTime;
        private bool _isRecording;

        public event Action<Recording>? RecordingStarted;
        public event Action<Recording>? RecordingStopped;
        public event Action<Models.MidiEvent>? MidiEventRecorded;

        public bool IsRecording => _isRecording;
        public Recording? CurrentRecording => _currentRecording;

        public RecordingService(ILogger<RecordingService> logger, string basePath)
        {
            _logger = logger;
            _recordingsPath = Path.Combine(basePath, "Recordings");

            if (!Directory.Exists(_recordingsPath))
            {
                Directory.CreateDirectory(_recordingsPath);
            }
        }

        public void StartRecording(string recordingName, string soundFontUsed)
        {
            if (_isRecording)
            {
                _logger.LogWarning("Recording already in progress");
                return;
            }

            _currentRecording = new Recording
            {
                Name = string.IsNullOrWhiteSpace(recordingName) ? $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}" : recordingName,
                SoundFontUsed = soundFontUsed,
                CreatedAt = DateTime.Now
            };

            _recordingStartTime = DateTime.Now;
            _isRecording = true;

            _logger.LogInformation($"Started recording: {_currentRecording.Name} at {_recordingStartTime:HH:mm:ss.fff}");
            RecordingStarted?.Invoke(_currentRecording);
        }

        public Recording? StopRecording()
        {
            if (!_isRecording || _currentRecording == null)
            {
                _logger.LogWarning("No recording in progress");
                return null;
            }

            _currentRecording.Duration = DateTime.Now - _recordingStartTime;
            _isRecording = false;

            SaveRecording(_currentRecording);

            _logger.LogInformation($"Stopped recording: {_currentRecording.Name} (Duration: {_currentRecording.FormattedDuration})");
            RecordingStopped?.Invoke(_currentRecording);

            var completed = _currentRecording;
            _currentRecording = null;
            return completed;
        }

        public void RecordMidiEvent(MidiInMessageEventArgs e)
        {
            if (!_isRecording || _currentRecording == null)
            {
                return;
            }

            var midiEvent = new Models.MidiEvent
            {
                Timestamp = DateTime.Now,
                RawMessage = e.RawMessage,
                Command = e.RawMessage & 0xF0,
                Channel = e.RawMessage & 0x0F,
                Data1 = (e.RawMessage >> 8) & 0xFF,
                Data2 = (e.RawMessage >> 16) & 0xFF
            };

            _currentRecording.Events.Add(midiEvent);
            MidiEventRecorded?.Invoke(midiEvent);
        }

        private void SaveRecording(Recording recording)
        {
            try
            {
                var filePath = Path.Combine(_recordingsPath, $"{recording.Id}.json");
                var json = JsonSerializer.Serialize(recording, new JsonSerializerOptions
                {
                    WriteIndented = true,
                });

                File.WriteAllText(filePath, json);
                _logger.LogInformation($"Recording saved to: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save recording");
            }
        }

        public List<Recording> GetSavedRecordings()
        {
            var recordings = new List<Recording>();

            try
            {
                var jsonFiles = Directory.GetFiles(_recordingsPath, "*.json");

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var recording = JsonSerializer.Deserialize<Recording>(json);
                        if (recording != null)
                        {
                            recordings.Add(recording);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to load recording from: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load recordings directory");
            }

            return recordings.OrderByDescending(r => r.CreatedAt).ToList();
        }

        public void DeleteRecording(Recording recording)
        {
            try
            {
                var filePath = Path.Combine(_recordingsPath, $"{recording.Id}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation($"Deleted recording: {recording.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete recording: {recording.Name}");
            }
        }

        public bool RenameRecording(Recording recording, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                _logger.LogWarning("Cannot rename recording with empty name");
                return false;
            }

            try
            {
                var oldName = recording.Name;
                recording.Name = newName.Trim();

                // Sauvegarder le fichier avec le nouveau nom
                SaveRecording(recording);

                _logger.LogInformation($"Renamed recording from '{oldName}' to '{newName}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to rename recording from '{recording.Name}' to '{newName}'");
                return false;
            }
        }
    }
}
