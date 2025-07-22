using NAudio.Midi;
using NAudio.Wave;
using NFluidsynth;
using PianoStream.Utils;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace PianoStream
{
    public partial class MainWindow : FluentWindow
    {
        private MidiIn? midiIn; 
        private Synth? synth; 
        private Settings? settings; 
        private WaveOutEvent? waveOut;
        private FluidSynthWaveProvider? waveProvider;
        private uint sfId;

        public MainWindow()
        {
            InitializeComponent();
            LoadSoundFonts();
        }

        #region SoundFonts Methods
        private void LoadSoundFonts()
        {
            try
            {
                var assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                if (!Directory.Exists(assetsPath))
                {
                    Log("Directory doesn't exists");
                    return;
                }

                var sf2Files = Directory.GetFiles(assetsPath, "*.sf2");
                if (sf2Files.Length == 0)
                {
                    Log("No SoundFont (.sf2) available in Assets.");
                    return;
                }

                foreach (var sf2File in sf2Files)
                {
                    SoundFontComboBox.Items.Add(Path.GetFileName(sf2File));
                }

                SoundFontComboBox.SelectedIndex = 0; 
                Log($"SoundFonts available : {sf2Files.Length}");
            }
            catch (Exception ex)
            {
                Log("Loading Error SoundFonts : " + ex.Message);
            }
        }
        #endregion

        #region UI Event Handlers
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (synth != null)
            {
                Log("Synth already started");
                return; 
            }

            try
            {
                // Init FluidSynth  
                settings = new Settings();
                synth = new Synth(settings);
                synth.Gain = 1;

                waveOut = new WaveOutEvent
                {
                    DesiredLatency = 50
                };
                waveProvider = new FluidSynthWaveProvider(synth, NoiseCancellationCheckBox.IsChecked == true);
                waveOut.Init(waveProvider);
                waveOut.Play();

                var soundFontFileName = SoundFontComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(soundFontFileName))
                {
                    Log("Invalid SoundFont selection.");
                    return;
                }

                var soundFontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", soundFontFileName);
                sfId = synth.LoadSoundFont(soundFontPath, true);
                Log($"SoundFont loaded: {soundFontPath}");

                synth.ProgramSelect(0, sfId, 0, 0);

                if (MidiIn.NumberOfDevices == 0)
                {
                    Log("No MIDI device found.");
                    return;
                }

                midiIn = new MidiIn(0);
                midiIn.MessageReceived += MidiIn_MessageReceived;
                midiIn.ErrorReceived += (s, ev) => Log("MIDI Error : " + ev.RawMessage);
                midiIn.Start();
                Log("MIDI input started.");
            }
            catch (Exception ex)
            {
                Log("Error : " + ex.Message);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (synth != null)
            {
                synth.Gain = (float)e.NewValue;
                VolumeValueText.Text = $"{(int)(synth.Gain * 100)}%";
                Log($"Volume set to {(int)(synth.Gain * 100)}%");
            }
        }

        private void NoiseCancellationCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (waveProvider != null)
            {
                waveProvider.EnableNoiseCancellation = true;
            }
            Log("Noise cancellation: ON");
        }

        private void NoiseCancellationCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (waveProvider != null)
            {
                waveProvider.EnableNoiseCancellation = false;
            }
            Log("Noise cancellation: OFF");
        }
        #endregion

        #region MIDI Methods
        private void MidiIn_MessageReceived(object? sender, MidiInMessageEventArgs e)
        {
            var raw = e.RawMessage;
            int command = raw & 0xF0;
            int channel = raw & 0x0F;
            int data1 = (raw >> 8) & 0xFF;
            int data2 = (raw >> 16) & 0xFF;

            Dispatcher.Invoke(() => Log($"MIDI: cmd=0x{command:X2} ch={channel} data1={data1} data2={data2}"));

            switch (command)
            {
                case 0x90: // Note On    
                    if (data2 > 0)
                        synth?.NoteOn(channel, data1, data2);
                    else
                        synth?.NoteOff(channel, data1);
                    break;

                case 0x80: // Note Off    
                    synth?.NoteOff(channel, data1);
                    break;

                case 0xB0: // Contrôleur (ex: pédale sustain)    
                    synth?.CC(channel, data1, data2);
                    break;
            }
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
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            DisposeResources();
        }

        private void DisposeResources()
        {
            midiIn?.Stop();
            midiIn?.Dispose();
            synth?.Dispose();
            waveOut?.Dispose();
        }
        #endregion
    }
}