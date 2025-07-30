using Microsoft.Extensions.Logging;
using NFluidsynth;
using PianoStream.Core.Models;

namespace PianoStream.Core.Services
{
    public class PlaybackService : IDisposable
    {
        private readonly ILogger<PlaybackService> _logger;
        private CancellationTokenSource? _playbackCancellation;
        private bool _isPlaying;
        private Recording? _currentPlayback;

        public event Action<Recording>? PlaybackStarted;
        public event Action<Recording>? PlaybackStopped;
        public event Action<Models.MidiEvent>? MidiEventPlayed;
        public event Action<TimeSpan, TimeSpan>? PlaybackProgress; // current, total

        public bool IsPlaying => _isPlaying;
        public Recording? CurrentPlayback => _currentPlayback;

        public PlaybackService(ILogger<PlaybackService> logger)
        {
            _logger = logger;
        }

        public async Task PlayRecording(Recording recording, Synth synth)
        {
            if(_isPlaying)
            {
                _logger.LogWarning("Playback already in progress");
                return;
            }

            if (recording.Events.Count == 0)
            {
                _logger.LogWarning("Recording has no events to play");
                return;    
            }

            _currentPlayback = recording;
            _isPlaying = true;
            _playbackCancellation = new CancellationTokenSource();

            _logger.LogInformation($"Starting playback: {recording.Name}");
            PlaybackStarted?.Invoke(recording);

            try
            {
                await PlaybackLoop(recording, synth, _playbackCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Playback cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during playback");
            }
            finally
            {
                _isPlaying = false;
                PlaybackStopped?.Invoke(recording);
                _currentPlayback = null;
                _playbackCancellation?.Dispose();
                _playbackCancellation = null;
            }
        }

        public void StopPlayback()
        {
            if(_isPlaying && _playbackCancellation != null)
            {
                _playbackCancellation.Cancel();
                _logger.LogInformation("Playback stopped by user");
            }
        }

        private async Task PlaybackLoop(Recording recording, Synth synth, CancellationToken cancellationToken)
        {
            if (recording.Events.Count == 0) return;

            var startTime = DateTime.Now;
            var firstEventTime = recording.Events[0].Timestamp;

            foreach (var midiEvent in recording.Events)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var eventDelay = midiEvent.Timestamp - firstEventTime;
                var playbackTime = DateTime.Now - startTime;
                var waitTime = eventDelay - playbackTime;

                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken);
                }
                
                try
                {
                    PlayMidiEvent(midiEvent, synth);
                    MidiEventPlayed?.Invoke(midiEvent);

                    var currentTime = DateTime.Now - startTime;
                    PlaybackProgress?.Invoke(currentTime, recording.Duration);
                }
                catch(Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to play MIDI event: {midiEvent}");
                }
            }

            _logger.LogInformation($"Playback completed: {recording.Name}");
        }

        private void PlayMidiEvent(Models.MidiEvent midiEvent, Synth synth)
        {
            switch (midiEvent.EventType)
            {
                case MidiEventType.NoteOn:
                    synth.NoteOn(midiEvent.Channel, midiEvent.Data1, midiEvent.Data2);
                    break;

                case MidiEventType.NoteOff:
                    synth.NoteOff(midiEvent.Channel, midiEvent.Data1);
                    break;

                case MidiEventType.ControlChange:
                    synth.CC(midiEvent.Channel, midiEvent.Data1, midiEvent.Data2);
                    break;
            }
        }

        public void Dispose()
        {
            StopPlayback();
            _playbackCancellation?.Dispose();
        }
    }
}
