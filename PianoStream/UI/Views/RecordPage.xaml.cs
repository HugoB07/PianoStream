using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NFluidsynth;
using PianoStream.Core;
using PianoStream.Core.Models;
using PianoStream.Core.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace PianoStream.UI.Views
{
    public partial class RecordPage : Page
    {
        private readonly ILogger<RecordPage> _logger;
        private PianoSynthController? _controller;
        private RecordingService? _recordingService;
        private PlaybackService? _playbackService;
        private ExportService? _exportService;
        private SoundFontService? _soundFontService;
        private MidiDeviceService? _midiDeviceService;

        private DispatcherTimer? _recordingTimer;
        private DispatcherTimer? _playbackTimer;
        private DateTime _recordingStartTime;

        private ObservableCollection<Recording> _recordings;
        private Recording? _selectedRecording;

        public RecordPage()
        {
            InitializeComponent();
            _logger = App.GetLogger<RecordPage>();
            _recordings = new ObservableCollection<Recording>();

            InitializeServices();
            LoadSoundFonts();
            RefreshRecordings();
            SetupTimers();
        }

        #region Initilization
        private void InitializeServices()
        {
            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;

                _soundFontService = new SoundFontService(basePath);
                _midiDeviceService = new MidiDeviceService();
                _recordingService = new RecordingService(App.GetLogger<RecordingService>(), basePath);
                _playbackService = new PlaybackService(App.GetLogger<PlaybackService>());
                _exportService = new ExportService(App.GetLogger<ExportService>());
                _controller = new PianoSynthController(App.GetLogger<PianoSynthController>());

                _recordingService.RecordingStarted += OnRecordingStarted;
                _recordingService.RecordingStopped += OnRecordingStopped;

                _playbackService.PlaybackStarted += OnPlaybackStarted;
                _playbackService.PlaybackStopped += OnPlaybackStopped;
                _playbackService.PlaybackProgress += OnPlaybackProgress;

                _controller.OnMidiRaw += OnMidiRaw;

                _midiDeviceService.DevicesUpdated += UpdateMidiDeviceComboBox;
                _midiDeviceService.StartMonitoring();

                _exportService.ExportProgress += OnExportProgress;

                _logger.LogInformation("Services initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize services");
                ShowDialog("Error", "OK", $"Failed to initialize services: {ex.Message}");
            }
        }

        private void InitializePianoController(string soundFont, int midiDeviceIndex = 0)
        {
            if (_controller == null || _soundFontService == null) return;

            _controller.Initialize(
                _soundFontService.GetFullPath(soundFont),
                false, // noise cancellation
                midiDeviceIndex
            );
        }

        private void LoadSoundFonts()
        {
            try
            {
                if (_soundFontService == null) return;

                var fonts = _soundFontService.GetAvailableSoundFonts().ToList();

                RecordSoundFontComboBox.Items.Clear();
                PlaybackSoundFontComboBox.Items.Clear();

                foreach (var font in fonts)
                {
                    RecordSoundFontComboBox.Items.Add(font);
                    PlaybackSoundFontComboBox.Items.Add(font);
                }

                if (RecordSoundFontComboBox.Items.Count > 0)
                {
                    RecordSoundFontComboBox.SelectedIndex = 0;
                    PlaybackSoundFontComboBox.SelectedIndex = 0;
                }

                _logger.LogInformation($"Loaded {fonts.Count} SoundFonts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load SoundFonts");
            }
        }

        private void UpdateMidiDeviceComboBox(List<MidiDeviceInfo> devices)
        {
            Dispatcher.Invoke(() =>
            {
                MidiDeviceComboBox.Items.Clear();
                foreach (var device in devices)
                {
                    MidiDeviceComboBox.Items.Add(device);
                }

                if (MidiDeviceComboBox.Items.Count > 0 && MidiDeviceComboBox.SelectedIndex == -1)
                {
                    MidiDeviceComboBox.SelectedIndex = 0;
                    _logger.LogInformation($"MIDI Devices found: {devices.Count}");
                }
            });
        }

        private void RefreshRecordings()
        {
            try
            {
                if (_recordingService == null) return;

                var recordings = _recordingService.GetSavedRecordings();

                _recordings.Clear();
                foreach (var recording in recordings)
                {
                    _recordings.Add(recording);
                }

                RecordingsDataGrid.ItemsSource = _recordings;
                ExportRecordingComboBox.ItemsSource = _recordings;

                _logger.LogInformation($"Loaded {recordings.Count} recordings");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh recordings");
            }
        }

        private void SetupTimers()
        {
            _recordingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _recordingTimer.Tick += (s, e) => UpdateRecordingTime();

            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
        }
        #endregion

        #region Recording Events
        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recordingService == null || _controller == null) return;

                if (!_recordingService.IsRecording)
                {
                    var recordingName = RecordingNameTextBox.Text.Trim();
                    var soundFont = RecordSoundFontComboBox.SelectedItem?.ToString() ?? "";
                    var midiDeviceIndex = Math.Max(0, MidiDeviceComboBox.SelectedIndex);

                    if (string.IsNullOrEmpty(recordingName))
                    {
                        recordingName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}";
                        RecordingNameTextBox.Text = recordingName;
                    }

                    if (_controller != null && _soundFontService != null)
                    {
                        _controller.Dispose();
                        InitializePianoController(soundFont, midiDeviceIndex);
                    }

                    _recordingService.StartRecording(recordingName, soundFont);
                }
                else
                {
                    _recordingService.StopRecording();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RecordButton_Click");
                ShowDialog("Error", "OK", $"Recording error: {ex.Message}");
            }
        }

        private void OnRecordingStarted(Recording recording)
        {
            Dispatcher.Invoke(() =>
            {
                RecordButton.Content = "⏹️ Stop Recording";
                StatusLabel.Content = "Recording...";
                StatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                _recordingStartTime = DateTime.Now;
                _recordingTimer?.Start();

                RecordingNameTextBox.IsEnabled = false;
                RecordSoundFontComboBox.IsEnabled = false;
                MidiDeviceComboBox.IsEnabled = false;
            });
        }

        private void OnRecordingStopped(Recording recording)
        {
            Dispatcher.Invoke(() =>
            {
                RecordButton.Content = "🔴 Start Recording";
                StatusLabel.Content = "Ready";
                StatusLabel.Foreground = System.Windows.Media.Brushes.Green;
                _recordingTimer?.Stop();

                RecordingNameTextBox.IsEnabled = true;
                RecordSoundFontComboBox.IsEnabled = true;
                MidiDeviceComboBox.IsEnabled = true;

                RecordingNameTextBox.Text = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}";

                RefreshRecordings();

                _logger.LogInformation($"Recording saved: {recording.Name}");
            });
        }

        private void OnMidiRaw(NAudio.Midi.MidiInMessageEventArgs e)
        {
            _recordingService?.RecordMidiEvent(e);
        }

        private void UpdateRecordingTime()
        {
            var elapsed = DateTime.Now - _recordingStartTime;
            DurationLabel.Content = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
        }
        #endregion

        #region Export Events
        private void ExportRecordingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = ExportRecordingComboBox.SelectedItem != null;

            ExportMidiButton.IsEnabled = hasSelection;
            ExportWavButton.IsEnabled = hasSelection;
            ExportMp3Button.IsEnabled = hasSelection;
        }

        private async void ExportMidiButton_Click(object sender, RoutedEventArgs e)
        {
            await ExportRecording("MIDI", "mid", async (recording, path) =>
            {
                _exportService!.ExportToMidi(recording, path);
                await Task.CompletedTask;
            });
        }

        private async void ExportWavButton_Click(object sender, RoutedEventArgs e)
        {
            await ExportRecording("WAV", "wav", async (recording, path) =>
            {
                var soundFont = GetSelectedPlaybackSoundFont(recording);
                await _exportService!.ExportToWav(recording, path, _soundFontService!.GetFullPath(soundFont));
            });
        }

        private async void ExportMp3Button_Click(object sender, RoutedEventArgs e)
        {
            await ExportRecording("MP3", "mp3", async (recording, path) =>
            {
                var soundFont = GetSelectedPlaybackSoundFont(recording);
                await _exportService!.ExportToMp3(recording, path, _soundFontService!.GetFullPath(soundFont));
            });
        }

        private string GetSelectedPlaybackSoundFont(Recording recording)
        {
            // Utiliser la SoundFont sélectionnée pour le playback, ou celle d'origine si aucune n'est sélectionnée
            return PlaybackSoundFontComboBox.SelectedItem?.ToString() ?? recording.SoundFontUsed;
        }

        private Synth? GetOrCreateSynth(string soundFont)
        {
            if (_controller == null || _soundFontService == null) return null;

            if(_controller.IsInitialized)
            {
                return _controller?.Synth;
            }

            _controller?.Initialize(_soundFontService.GetFullPath(soundFont), false, 0);
            return _controller?.Synth;
        }

        private async Task ExportRecording(string format, string extension, Func<Recording, string, Task> exportFunc)
        {
            try
            {
                var selectedRecording = ExportRecordingComboBox.SelectedItem as Recording;
                if (selectedRecording == null)
                {
                    ShowDialog("No Recording Selected", "OK", "Please select a recording to export.");
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Title = $"Export to {format}",
                    Filter = $"{format} files (*.{extension})|*.{extension}|All files (*.*)|*.*",
                    FileName = $"{selectedRecording.Name}.{extension}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportStatusLabel.Text = $"Exporting to {format}...";
                    ExportProgressBar.Value = 0;

                    SetExportButtonsEnabled(false);

                    await exportFunc(selectedRecording, saveDialog.FileName);

                    ExportStatusLabel.Text = $"Export to {format} completed!";

                    ShowDialog("Exort Complete", "OK", $"Recording exported successfully to:\n{saveDialog.FileName}");

                    _logger.LogInformation($"Exported {selectedRecording.Name} to {format}: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting to {format}");
                ExportStatusLabel.Text = $"Export to {format} failed!";

                ShowDialog("Export Error", "OK", $"Failed to export recording: {ex.Message}");
            }
            finally
            {
                SetExportButtonsEnabled(true);
                ExportProgressBar.Value = 0;
            }
        }

        private void SetExportButtonsEnabled(bool enabled)
        {
            ExportMidiButton.IsEnabled = enabled && ExportRecordingComboBox.SelectedItem != null;
            ExportWavButton.IsEnabled = enabled && ExportRecordingComboBox.SelectedItem != null;
            ExportMp3Button.IsEnabled = enabled && ExportRecordingComboBox.SelectedItem != null;
        }

        private void OnExportProgress(int progress)
        {
            Dispatcher.Invoke(() =>
            {
                ExportProgressBar.Value = progress;
            });
        }
        #endregion

        #region Playback Events
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedRecording == null || _playbackService == null)
                {
                    ShowDialog("No Recording Selected", "OK", "Please select a recording to play.");
                    return;
                }

                var soundFont = GetSelectedPlaybackSoundFont(_selectedRecording);
                if (_controller != null && _controller.IsInitialized)
                {
                    _controller.Dispose(); 
                    var midiDeviceIndex = Math.Max(0, MidiDeviceComboBox.SelectedIndex);
                    InitializePianoController(soundFont, midiDeviceIndex);
                }
                var synth = GetOrCreateSynth(soundFont);

                if (synth == null)
                {
                    ShowDialog("Synth Error", "OK", "Failed to initialize synthesizer.");
                    return;
                }

                _ = _playbackService.PlayRecording(_selectedRecording, synth);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting playback");
                ShowDialog("Error", "OK", $"Playback error: {ex.Message}");
            }
        }

        private void StopPlayButton_Click(object sender, RoutedEventArgs e)
        {
            _playbackService?.StopPlayback();
        }

        private void OnPlaybackStarted(Recording recording)
        {
            Dispatcher.Invoke(() =>
            {
                PlayButton.IsEnabled = false;
                StopPlayButton.IsEnabled = true;
                PlaybackProgressBar.Value = 0;
                _playbackTimer?.Start();
            });
        }

        private void OnPlaybackStopped(Recording recording)
        {
            Dispatcher.Invoke(() =>
            {
                PlayButton.IsEnabled = true;
                StopPlayButton.IsEnabled = false;
                PlaybackProgressBar.Value = 0;
                PlaybackTimeLabel.Text = "00:00 / 00:00";
                _playbackTimer?.Stop();
            });
        }

        private void OnPlaybackProgress(TimeSpan current, TimeSpan total)
        {
            Dispatcher.Invoke(() =>
            {
                if (total.TotalSeconds > 0)
                {
                    var progress = (current.TotalSeconds / total.TotalSeconds) * 100;
                    PlaybackProgressBar.Value = Math.Min(100, progress);
                }

                PlaybackTimeLabel.Text = $"{current:mm\\:ss} / {total:mm\\:ss}";
            });
        }
        #endregion

        #region UI Events
        private void RecordingsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedRecording = RecordingsDataGrid.SelectedItem as Recording;
            PlayButton.IsEnabled = _selectedRecording != null;

            if (_selectedRecording != null)
            {
                // Mettre à jour la SoundFont de playback avec celle de l'enregistrement
                var soundFontIndex = PlaybackSoundFontComboBox.Items.Cast<string>()
                    .ToList().FindIndex(sf => sf == _selectedRecording.SoundFontUsed);

                if (soundFontIndex >= 0)
                {
                    PlaybackSoundFontComboBox.SelectedIndex = soundFontIndex;
                }

                _logger.LogInformation($"Selected recording: {_selectedRecording.Name}");
            }
        }

        private void DeleteRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement button && button.Tag is Recording recording)
                {
                    var window = Application.Current.MainWindow as MainWindow;

                    var contentDialog = new ContentDialog(window?.GlobalDialogHost);
                    contentDialog.SetCurrentValue(ContentDialog.TitleProperty, "Confirm Deletion");
                    contentDialog.SetCurrentValue(ContentDialog.ContentProperty, new Wpf.Ui.Controls.TextBlock { Text = $"Are you sure you want to delete '{recording.Name}'?" });
                    contentDialog.SetCurrentValue(ContentDialog.PrimaryButtonTextProperty, "Yes");
                    contentDialog.SetCurrentValue(ContentDialog.CloseButtonTextProperty, "No");

                    contentDialog.ButtonClicked += (s, args) =>
                    {
                        if (args.Button == ContentDialogButton.Primary)
                        {
                            _recordingService?.DeleteRecording(recording);
                            RefreshRecordings();
                            _logger.LogInformation($"Deleted recording: {recording.Name}");
                        }
                    };

                    _ = contentDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recording");
                ShowDialog("Error", "OK", $"Failed to delete recording: {ex.Message}");
            }
        }

        private async void RenameRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement button && button.Tag is Recording recording)
                {
                    var window = Application.Current.MainWindow as MainWindow;

                    var contentDialog = new ContentDialog(window?.GlobalDialogHost);
                    contentDialog.SetCurrentValue(ContentDialog.TitleProperty, "Rename Recording");
                    contentDialog.SetCurrentValue(ContentControl.ContentProperty, new Wpf.Ui.Controls.TextBox { Text = recording.Name });
                    contentDialog.SetCurrentValue(ContentDialog.PrimaryButtonTextProperty, "Ok");
                    contentDialog.SetCurrentValue(ContentDialog.CloseButtonTextProperty, "Cancel");

                    contentDialog.ButtonClicked += (s, args) =>
                    {
                        if (args.Button == ContentDialogButton.Primary && contentDialog.Content is Wpf.Ui.Controls.TextBox textBox && !string.IsNullOrWhiteSpace(textBox.Text))
                        {
                            var newName = textBox.Text.Trim();
                            if (newName != recording.Name)
                            {
                                _recordingService?.RenameRecording(recording, newName);
                                RefreshRecordings();
                                _logger.LogInformation($"Renamed recording from '{recording.Name}' to '{newName}'");
                            }
                        }
                    };

                    await contentDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming recording");
                ShowDialog("Error", "OK", $"Failed to rename recording: {ex.Message}");
            }
        }
        #endregion

        #region Cleanup
        private void RecordPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _recordingTimer?.Stop();
                _playbackTimer?.Stop();

                _midiDeviceService?.Dispose();
                _recordingService = null;
                _playbackService?.Dispose();
                _controller?.Dispose();
                _midiDeviceService?.Dispose();

                _logger.LogInformation("RecordPage unloaded and resources disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during RecordPage cleanup");
            }
        }
        #endregion

        #region Utilities
        private async void ShowDialog(string title, string primaryButton, string text)
        {
            var window = Application.Current.MainWindow as MainWindow;

            var dialog = new ContentDialog(window?.GlobalDialogHost)
            {
                Title = title,
                PrimaryButtonText = primaryButton,
                Content = new Wpf.Ui.Controls.TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(8)
                }
            };

            await dialog.ShowAsync();
        }
        #endregion
    }
}