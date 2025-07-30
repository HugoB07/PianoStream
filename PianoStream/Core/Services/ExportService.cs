using Microsoft.Extensions.Logging;
using NAudio.Lame;
using NAudio.Midi;
using NAudio.Wave;
using NFluidsynth;
using PianoStream.Core.Models;
using PianoStream.Utils;
using System.IO;

namespace PianoStream.Core.Services
{
    public class ExportService
    {
        private readonly ILogger<ExportService> _logger;

        public event Action<int>? ExportProgress;

        public ExportService(ILogger<ExportService> logger)
        {
            _logger = logger;
            LameDLL.LoadNativeDLL(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExternalDll"));
        }

        public void ExportToMidi(Recording recording, string outputPath, int ppq = 480, double bpm = 120.0)
        {
            try
            {
                _logger.LogInformation($"Exporting to MIDI: {outputPath}");

                if (recording?.Events == null || recording.Events.Count == 0)
                    return;

                var collection = new MidiEventCollection(1, ppq);
                var trackEvents = new List<NAudio.Midi.MidiEvent>();

                int usPerQuarter = (int)Math.Round(60_000_000.0 / bpm);
                trackEvents.Add(new TempoEvent(usPerQuarter, 0));
                trackEvents.Add(new TimeSignatureEvent(0, 4, 2, 24, 8));

                var firstEventTime = recording.Events[0].Timestamp;

                long SecToTicks(double seconds) =>
                    (long)Math.Round(seconds * (ppq * (bpm / 60.0)));

                // Helper : convertit 0–15 -> 1–16 (NAudio)
                int ToNaudioChannel(int zeroBased)
                {
                    int ch = zeroBased + 1;
                    if (ch < 1 || ch > 16) throw new ArgumentOutOfRangeException(nameof(zeroBased), $"Canal invalide {zeroBased} (attendu 0–15).");
                    return ch;
                }

                // Helper : clamp 0–127
                int Clamp7(int v) => Math.Min(127, Math.Max(0, v));

                foreach (var ev in recording.Events)
                {
                    var rel = ev.Timestamp - firstEventTime;
                    long absoluteTicks = SecToTicks(rel.TotalSeconds);
                    int ch = ToNaudioChannel(ev.Channel);

                    switch (ev.EventType)
                    {
                        case MidiEventType.NoteOn:
                            trackEvents.Add(new NoteOnEvent(
                                absoluteTicks,
                                ch,
                                Clamp7(ev.Data1),   // note
                                Clamp7(ev.Data2),   // velocity
                                0                   // duration: 0 si tu ajoutes un NoteOff séparé
                            ));
                            break;

                        case MidiEventType.NoteOff:
                            trackEvents.Add(new NoteEvent(
                                absoluteTicks,
                                ch,
                                MidiCommandCode.NoteOff,
                                Clamp7(ev.Data1),
                                0
                            ));
                            break;

                        case MidiEventType.ControlChange:
                            trackEvents.Add(new ControlChangeEvent(
                                absoluteTicks,
                                ch,
                                (MidiController)Clamp7(ev.Data1),
                                Clamp7(ev.Data2)
                            ));
                            break;
                    }
                }

                // Pas besoin d'ajouter manuellement EndTrack : NAudio l'ajoutera à l'export.
                foreach (var me in trackEvents.OrderBy(e => e.AbsoluteTime))
                    collection.AddEvent(me, 0);

                MidiFile.Export(outputPath, collection);

                ExportProgress?.Invoke(100);
                _logger.LogInformation($"MIDI export completed: {outputPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to export MIDI: {outputPath}");
                throw;
            }
        }

        public async Task ExportToWav(Recording recording, string outputPath, string soundFontPath)
        {
            try
            {
                _logger.LogInformation($"Exporting to WAV: {outputPath}");

                using var exportSettings = new Settings();
                using var exportSynth = new Synth(exportSettings);

                var currentId = exportSynth.LoadSoundFont(soundFontPath, true);
                exportSynth.ProgramSelect(0, currentId, 0, 0);

                var waveProvider = new FluidSynthWaveProvider(exportSynth, false);
                using var writer = new WaveFileWriter(outputPath, waveProvider.WaveFormat);

                var tailSeconds = 2.0;
                var totalSeconds = recording.Duration.TotalSeconds + tailSeconds;

                int bytesPerSecond = waveProvider.WaveFormat.AverageBytesPerSecond; // = sampleRate * blockAlign
                long totalBytes = (long)(totalSeconds * bytesPerSecond);

                await RenderAudioBytes(recording, exportSynth, writer, waveProvider, totalBytes, bytesPerSecond);

                ExportProgress?.Invoke(100);
                _logger.LogInformation($"WAV export completed: {outputPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to export WAV: {outputPath}");
                throw;
            }
        }

        public async Task ExportToMp3(Recording recording, string outputPath, string soundFontPath, int bitRate = 128)
        {
            try
            {
                _logger.LogInformation($"Exporting to MP3: {outputPath}");

                using var exportSettings = new Settings();
                using var exportSynth = new Synth(exportSettings);

                var currentId = exportSynth.LoadSoundFont(soundFontPath, true);
                exportSynth.ProgramSelect(0, currentId, 0, 0);

                var waveProvider = new FluidSynthWaveProvider(exportSynth, false);

                var tempWavPath = Path.GetTempFileName() + ".wav";

                try
                {
                    using(var tempWriter = new WaveFileWriter(tempWavPath, waveProvider.WaveFormat))
                    {
                        var tailSeconds = 2.0;
                        var totalSeconds = recording.Duration.TotalSeconds + tailSeconds;
                        int bytesPerSecond = waveProvider.WaveFormat.AverageBytesPerSecond;
                        long totalBytes = (long)(totalSeconds * bytesPerSecond);

                        await RenderAudioBytes(recording, exportSynth, tempWriter, waveProvider, totalBytes, bytesPerSecond);
                    }

                    using var reader = new AudioFileReader(tempWavPath);
                    using var writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, bitRate);

                    var buffer = new byte[4096];
                    long totalBytesToRead = reader.Length;
                    long bytesRead = 0;

                    int bytesFromReader;
                    while ((bytesFromReader = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        writer.Write(buffer, 0, bytesFromReader);
                        bytesRead += bytesFromReader;

                        int progress = (int)((bytesRead * 100L) / totalBytesToRead);
                        ExportProgress?.Invoke(progress);

                        await Task.Yield();
                    }
                }
                finally
                {
                    if (File.Exists(tempWavPath))
                    {
                        try
                        {
                            File.Delete(tempWavPath);
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogWarning(deleteEx, $"Failed to delete temporary file: {tempWavPath}");
                        }
                    }
                }

                ExportProgress?.Invoke(100);
                _logger.LogInformation($"MP3 export completed: {outputPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to export MP3: {outputPath}");
                throw;
            }
        }

        private async Task RenderAudioBytes(Recording recording, Synth synth, WaveFileWriter writer, FluidSynthWaveProvider waveProvider, long totalBytes, int bytesPerSecond)
        {
            if (recording.Events.Count == 0) return;

            var firstEventTime = recording.Events[0].Timestamp;
            long currentBytes = 0;

            // Choisissez un multiple de BlockAlign
            int blockAlign = waveProvider.WaveFormat.BlockAlign; // ex: 4 pour 16‑bit stéréo
            int bufferSize = Math.Max(4096, blockAlign * 1024);
            var buffer = new byte[bufferSize];

            int eventIndex = 0;
            var nextEventTime = recording.Events[0].Timestamp - firstEventTime;

            while (currentBytes < totalBytes)
            {
                int bytesToWrite = (int)Math.Min(bufferSize, totalBytes - currentBytes);

                // temps courant en secondes basé sur les octets déjà écrits
                double currentTimeSec = (double)currentBytes / bytesPerSecond;
                var currentTime = TimeSpan.FromSeconds(currentTimeSec);

                // Déclencher les événements MIDI dont le temps est écoulé
                while (eventIndex < recording.Events.Count && currentTime >= nextEventTime)
                {
                    var ev = recording.Events[eventIndex];
                    PlayMidiEventForExport(ev, synth);

                    eventIndex++;
                    if (eventIndex < recording.Events.Count)
                        nextEventTime = recording.Events[eventIndex].Timestamp - firstEventTime;
                }

                int bytesRead = waveProvider.Read(buffer, 0, bytesToWrite);
                // FluidSynthWaveProvider peut produire indéfiniment ; on borne par totalBytes.
                if (bytesRead <= 0)
                {
                    // plus rien à lire — on peut remplir de silence si on veut atteindre totalBytes
                    Array.Clear(buffer, 0, bytesToWrite);
                    writer.Write(buffer, 0, bytesToWrite);
                    currentBytes += bytesToWrite;
                }
                else
                {
                    writer.Write(buffer, 0, bytesRead);
                    currentBytes += bytesRead;
                }

                int progress = (int)((currentBytes * 100L) / totalBytes);
                ExportProgress?.Invoke(progress);

                await Task.Yield();
            }
        }

        private void PlayMidiEventForExport(Models.MidiEvent midiEvent, Synth synth)
        {
            switch(midiEvent.EventType)
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
    }
}
