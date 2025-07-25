using NAudio.Midi;
using PianoStream.Core.Models;

namespace PianoStream.Core.Services
{
    public class MidiDeviceService
    {
        private readonly System.Timers.Timer? _refreshTimer;
        private List<MidiDeviceInfo> _devices = new();

        public event Action<List<MidiDeviceInfo>>? DevicesUpdated;

        public MidiDeviceService(double refreshIntervalMs = 3000)
        {
            _refreshTimer = new System.Timers.Timer(refreshIntervalMs);
            _refreshTimer.Elapsed += (s, e) => RefreshDevices();
        }

        public void StartMonitoring()
        {
            RefreshDevices();
            _refreshTimer?.Start();
        }

        public void StopMonitoring()
        {
            _refreshTimer?.Stop();
        }

        public List<MidiDeviceInfo> GetDevices() => new(_devices);

        private void RefreshDevices()
        {
            var list = new List<MidiDeviceInfo>();
            for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            {
                var info = MidiIn.DeviceInfo(i);
                list.Add(new MidiDeviceInfo { Index = i, Name = info.ProductName });
            }

            // Simple change detection
            if (list.Count != _devices.Count ||
                !_devices.TrueForAll(d => list.Exists(ld => ld.Index == d.Index && ld.Name == d.Name)))
            {
                _devices = list;
                DevicesUpdated?.Invoke(GetDevices());
            }
        }

        public void Dispose()
        {
            StopMonitoring();
            _refreshTimer?.Dispose();
        }
    }
}
