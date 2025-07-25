using System.IO;

namespace PianoStream.Core.Services
{
    public class SoundFontService
    {
        private readonly string _sf2Folder;

        public SoundFontService(string basePath)
        {
            _sf2Folder = Path.Combine(basePath, "Assets", "SF2");
        }

        public IEnumerable<string> GetAvailableSoundFonts()
        {
            if (!Directory.Exists(_sf2Folder))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.GetFiles(_sf2Folder, "*.sf2") // Fixed the file search pattern to "*.sf2".  
                .Select(file => Path.GetFileName(file)!) // Added null-forgiving operator (!) to ensure non-nullability.  
                .ToList();
        }

        public string GetFullPath(string fileName) => Path.Combine(_sf2Folder, fileName);
    }
}
