using NFluidsynth;

namespace PianoStream.Core.Audio
{
    public class SoundFontManager
    {
        private readonly Synth? _synth;
        public uint CurrentSoundFontId { get; private set; }

        public SoundFontManager(Synth synth)
        {
            _synth = synth;
        }

        public void Load(string sfPath)
        {
            if(_synth != null)
            {
                CurrentSoundFontId = _synth.LoadSoundFont(sfPath, true);
                _synth.ProgramSelect(0, CurrentSoundFontId, 0, 0);
            }
        }
    }
}
