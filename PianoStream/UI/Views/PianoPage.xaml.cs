using PianoStream.Core;
using NAudio.Midi;
using System.Windows;
using System.Windows.Controls;
using PianoStream.Core.Services;
using PianoStream.Core.Models;

namespace PianoStream.UI.Views
{
    public partial class PianoPage : Page
    {
        private PianoSynthController? _controller;
        private SoundFontService? _soundFontService;
        private MidiDeviceService? _midiDeviceService;

        public PianoPage()
        {
            InitializeComponent();
            _midiDeviceService = new MidiDeviceService();
            _midiDeviceService.DevicesUpdated += UpdateMidiDeviceComboBox;
            _midiDeviceService.StartMonitoring();

            _soundFontService = new SoundFontService(AppDomain.CurrentDomain.BaseDirectory);
            _controller = new PianoSynthController();
            _controller.OnLog += Log;
            _controller.OnMidiRaw += HandleMidiRaw;

            LoadSoundFonts();
        }

        #region SoundFontService Methods
        private void LoadSoundFonts()
        {
            try
            {
                if (_soundFontService == null)
                {
                    Log("SoundFontService is not initialized.");
                    return;
                }

                var fonts = _soundFontService.GetAvailableSoundFonts().ToList();

                if (fonts.Count == 0)
                {
                    Log("No SoundFont (.sf2) available in Assets.");
                    return;
                }

                foreach (var sf in fonts)
                {
                    SoundFontComboBox.Items.Add(sf);
                }

                SoundFontComboBox.SelectedIndex = 0;
                Log($"SoundFonts available : {fonts.Count}");
            }
            catch (Exception ex)
            {
                Log("Loading Error SoundFonts : " + ex.Message);
            }
        }
        #endregion

        #region MidiDeviceService Methods
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
                    Log($"MIDI Devices found: {devices.Count}");
                }
            });
        }
        #endregion

        #region PianoSynthController Events
        private void HandleMidiRaw(MidiInMessageEventArgs e)
        {
            var raw = e.RawMessage;
            int command = raw & 0xF0;
            int channel = raw & 0x0F;
            int data1 = (raw >> 8) & 0xFF;
            int data2 = (raw >> 16) & 0xFF;

            Dispatcher.Invoke(() => Log($"MIDI: cmd=0x{command:X2} ch={channel} data1={data1} data2={data2}"));
        }
        #endregion

        #region UI Event Handlers
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_soundFontService == null)
            {
                Log("SoundFontService is not initialized.");
                return;
            }

            if (_controller != null && !_controller.IsInitialized)
            {
                var soundFontFile = SoundFontComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(soundFontFile))
                {
                    Log("Invalid SoundFont selected.");
                    return;
                }

                var selectedDevice = MidiDeviceComboBox.SelectedItem as MidiDeviceInfo;
                var midiIndex = selectedDevice?.Index ?? 0;

                _controller.Initialize(_soundFontService.GetFullPath(soundFontFile), NoiseCancellationCheckBox.IsChecked == true, midiIndex);
                StartButton.Content = "Stop Piano";
                VolumeSlider.IsEnabled = true;
            }
            else if(_controller != null)
            {
                _controller.Dispose();
                StartButton.Content = "Start Piano";
                VolumeSlider.IsEnabled = false;
                Log("Synth stopped.");
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_controller != null && _controller.IsInitialized)
            {
                _controller.SetGain((float)e.NewValue);
                VolumeValueText.Content = $"{(int)(e.NewValue * 100)}%";
                Log($"Volume set to {(int)(e.NewValue * 100)}%");
            }
        }

        private void NoiseCancellationCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_controller == null)
            {
                Log("Controller is not initialized.");
                return;
            }
            _controller.SetNoiseCancellation(true);
            Log("Noise cancellation: ON");
        }

        private void NoiseCancellationCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_controller == null)
            {
                Log("Controller is not initialized.");
                return;
            }
            _controller.SetNoiseCancellation(false);
            Log("Noise cancellation: OFF");
        }
        #endregion

        #region Utilities
        private void Log(string message)
        {
            LogBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
            LogBox.ScrollToEnd();
        }
        #endregion

        #region Cleanup
        private void PianoPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _midiDeviceService?.Dispose();
            _controller?.Dispose();
        }
        #endregion
    }
}
