using NFluidsynth;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace PianoStream
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set up custom DLL load path
            NativeLibrary.SetDllImportResolver(typeof(Synth).Assembly, ResolveFluidsynth);
        }

        private static IntPtr ResolveFluidsynth(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != "fluidsynth")
                return IntPtr.Zero;

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExternalDll", "fluidsynth.dll");

            if (!File.Exists(dllPath))
                throw new DllNotFoundException($"Can't find '{dllPath}'.");

            return NativeLibrary.Load(dllPath);
        }
    }
}
