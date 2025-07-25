using Microsoft.Extensions.Logging;
using NFluidsynth;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace PianoStream
{
    public partial class App : Application
    {
        public static ILoggerFactory LoggerFactory { get; private set; } = null!;
        public static ILogger<T> GetLogger<T>() => LoggerFactory.CreateLogger<T>();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
#if DEBUG
            AllocConsole();
#endif

            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .AddDebug()
                    .SetMinimumLevel(LogLevel.Information);
            });

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                var logger = GetLogger<App>();
                logger.LogCritical(ev.ExceptionObject as Exception, "Unhandled exception");
            };

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
