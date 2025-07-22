# PianoStream

**PianoStream** is a WPF application that uses FluidSynth and NAudio to play MIDI input through customizable SoundFonts.

## Features

- Load and select SoundFont (.sf2) files from the `Assets` folder
- Play MIDI input from connected MIDI devices
- Adjustable volume control
- Optional noise cancellation feature
- Real-time MIDI message logging

## Requirements

- Windows OS
- .NET Framework (version compatible with WPF app)
- MIDI input device (e.g., MIDI keyboard)
- SoundFont files (.sf2) placed in the `Assets` folder

## How to Use

1. Place your `.sf2` SoundFont files into the `Assets` folder.  
2. Launch the application.
3. Select a SoundFont from the dropdown.
4. Click **Start** to initialize the synthesizer and MIDI input.
5. Play notes on your MIDI device to hear sound.
6. Adjust volume and toggle noise cancellation as needed.

## Technologies Used

- [NAudio](https://github.com/naudio/NAudio) — for MIDI input and audio playback
- [NFluidsynth](https://github.com/FluidSynth/fluidsynth) — for software synthesis using SoundFonts
- WPF — user interface framework

## License

Specify your license here.
