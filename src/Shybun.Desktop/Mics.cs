using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.Media.Audio.Endpoints;
using Windows.Win32.System.Com;

namespace Shybun.Desktop;

/// <summary>
/// Every mic Windows lists, muted and read back through each one's <c>IAudioEndpointVolume</c>.
/// It only flips Windows' mute switch and never opens an audio stream.
/// </summary>
internal sealed partial class Mics : IDisposable
{
    // Tags Shybun's own changes, so their notifications aren't logged as something else's doing.
    private static readonly Guid ShybunChange = new("5b3c2d9e-6a41-4f0e-9a7c-1f2e8d4b6a10");

    private readonly IMMDeviceEnumerator _enumerator = MMDeviceEnumerator.CreateInstance<IMMDeviceEnumerator>();
    private readonly Dictionary<string, Mic> _mics = [];
    private readonly Action<Action> _post;
    private readonly DeviceSink _deviceSink;

    /// <param name="post">Runs an action on the UI thread. Windows calls back on its own threads.</param>
    public Mics(Action<Action> post)
    {
        _post = post;
        _deviceSink = new DeviceSink(this);
        Refresh(announce: false);
        _enumerator.RegisterEndpointNotificationCallback(_deviceSink);
    }

    /// <summary>A mic appeared, with what Windows reports for it.</summary>
    public event Action<string, bool?>? Added;

    public event Action<string>? Removed;

    /// <summary>Windows reported a mic's mute, and whether Shybun made the change.</summary>
    public event Action<string, bool, bool>? Changed;

    public IEnumerable<KeyValuePair<string, bool?>> Read() =>
        _mics.Select(mic => KeyValuePair.Create(mic.Key, mic.Value.Read()));

    /// <returns>What Windows reports afterwards, which isn't always what was asked for.</returns>
    public bool? Set(string id, bool muted) => _mics.TryGetValue(id, out var mic) ? mic.Set(muted) : null;

    public void Dispose()
    {
        _enumerator.UnregisterEndpointNotificationCallback(_deviceSink);
        foreach (var mic in _mics.Values)
        {
            mic.Dispose();
        }

        _mics.Clear();
    }

    // Windows reports adds, removals and state changes per device and for every data flow, so
    // diffing the active mics after any of them catches a plug-in however it's reported.
    private void Refresh(bool announce)
    {
        var active = ActiveMics();
        foreach (var id in _mics.Keys.Where(id => !active.ContainsKey(id)).ToList())
        {
            _mics.Remove(id, out var gone);
            gone!.Dispose();
            if (announce)
            {
                Removed?.Invoke(id);
            }
        }

        foreach (var (id, device) in active.Where(pair => !_mics.ContainsKey(pair.Key)))
        {
            var mic = new Mic(this, id, device);
            _mics[id] = mic;
            if (announce)
            {
                Added?.Invoke(id, mic.Read());
            }
        }
    }

    private unsafe Dictionary<string, IMMDevice> ActiveMics()
    {
        var active = new Dictionary<string, IMMDevice>();
        _enumerator.EnumAudioEndpoints(EDataFlow.eCapture, DEVICE_STATE.DEVICE_STATE_ACTIVE, out var devices);
        devices.GetCount(out var count);
        for (uint i = 0; i < count; i++)
        {
            devices.Item(i, out var device);
            device.GetId(out PWSTR id);
            active[id.ToString()] = device;
            Marshal.FreeCoTaskMem((nint)id.Value);
        }

        return active;
    }

    private sealed class Mic : IDisposable
    {
        private readonly IAudioEndpointVolume? _volume;
        private readonly VolumeSink? _sink;

        public unsafe Mic(Mics owner, string id, IMMDevice device)
        {
            try
            {
                device.Activate(typeof(IAudioEndpointVolume).GUID, CLSCTX.CLSCTX_ALL, null, out var volume);
                _volume = (IAudioEndpointVolume)volume;
                _sink = new VolumeSink(owner, id);
                _volume.RegisterControlChangeNotify(_sink);
            }
            catch (Exception e) when (e is COMException or InvalidCastException)
            {
                // Left without a volume, the mic reads as unconfirmed rather than vanishing.
            }
        }

        // Any failure reads as unconfirmed: the badge must never call a mic it can't read muted.
        public bool? Read()
        {
            if (_volume is null)
            {
                return null;
            }

            try
            {
                _volume.GetMute(out BOOL muted);
                return muted;
            }
            catch (COMException)
            {
                return null;
            }
        }

        public bool? Set(bool muted)
        {
            try
            {
                _volume?.SetMute(muted, ShybunChange);
            }
            catch (COMException)
            {
                // The read below reports whatever Windows has instead.
            }

            return Read();
        }

        public void Dispose()
        {
            try
            {
                if (_sink is not null)
                {
                    _volume?.UnregisterControlChangeNotify(_sink);
                }
            }
            catch (COMException)
            {
                // A device that's already gone has nothing left to unregister.
            }
        }
    }

    [GeneratedComClass]
    private sealed partial class VolumeSink(Mics owner, string id) : IAudioEndpointVolumeCallback
    {
        public unsafe void OnNotify(AUDIO_VOLUME_NOTIFICATION_DATA* data)
        {
            bool muted = data->bMuted;
            var byShybun = data->guidEventContext == ShybunChange;
            owner._post(() => owner.Changed?.Invoke(id, muted, byShybun));
        }
    }

    [GeneratedComClass]
    private sealed partial class DeviceSink(Mics owner) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(PCWSTR deviceId, DEVICE_STATE newState) => Refresh();

        public void OnDeviceAdded(PCWSTR deviceId) => Refresh();

        public void OnDeviceRemoved(PCWSTR deviceId) => Refresh();

        public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, PCWSTR defaultDeviceId)
        {
        }

        public void OnPropertyValueChanged(PCWSTR deviceId, PROPERTYKEY key)
        {
        }

        private void Refresh() => owner._post(() => owner.Refresh(announce: true));
    }
}
