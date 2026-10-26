namespace Shybun.Core;

/// <summary>What the badge shows, worked out from what Windows last reported.</summary>
public enum BadgeState
{
    /// <summary>Mute is off and nothing needs saying.</summary>
    Hidden,

    /// <summary>Every mic reads muted.</summary>
    Muted,

    /// <summary>Mute is on, but a mic reads live.</summary>
    Live,

    /// <summary>A mic or the Shybun camera can't be read back.</summary>
    Unconfirmed,
}

/// <summary>The devices a badge state is about.</summary>
[Flags]
public enum BadgeDevices
{
    None = 0,
    Mic = 1,
    Camera = 2,
}

/// <summary>
/// Shybun's one on/off state, and what was last read back for each mic and the Shybun camera: <c>true</c> for muted,
/// <c>false</c> for live, and <c>null</c> when it couldn't be read.
/// </summary>
public sealed class Mute
{
    private readonly Dictionary<string, bool?> _mics = [];

    /// <summary>Starts from the mics' current readings, without changing any of them.</summary>
    public Mute(IEnumerable<KeyValuePair<string, bool?>> readings)
    {
        foreach (var (id, muted) in readings)
        {
            _mics[id] = muted;
        }

        On = _mics.Count > 0 && _mics.Values.All(muted => muted == true);
    }

    public bool On { get; private set; }

    public IReadOnlyDictionary<string, bool?> Mics => _mics;

    /// <summary>Whether a Shybun camera is installed. Without one, the badge is about the mics alone.</summary>
    public bool HasCamera { get; private set; }

    /// <summary>
    /// The Shybun camera's Mute, read back: <c>true</c> while it sends the camera-off image. The camera reads the same
    /// value for every frame, so this is what it sends.
    /// </summary>
    public bool? Camera { get; private set; }

    /// <summary>Whether Windows reports an app streaming a real camera other than through the Shybun camera.</summary>
    public bool RealCameraInUse { get; private set; }

    /// <summary>
    /// Everything reads muted, but an app streams a real camera that Shybun can't turn off: past the Shybun camera, or
    /// with none installed. The badge then can't confirm the camera.
    /// </summary>
    public bool CameraBypassed => On && RealCameraInUse && FromReadings == BadgeState.Muted;

    public BadgeState Badge => CameraBypassed ? BadgeState.Unconfirmed : FromReadings;

    /// <summary>Which devices <see cref="Badge"/> is about: the ones it can't read, the live ones, or every muted one.</summary>
    public BadgeDevices BadgeDevices => CameraBypassed ? BadgeDevices.Camera : FromReadings switch
    {
        BadgeState.Unconfirmed => Devices(r => r.Muted is null),
        BadgeState.Live => Devices(r => r.Muted == false),
        BadgeState.Muted => Devices(_ => true),
        _ => BadgeDevices.None,
    };

    // Built only from readings, so a mute Shybun asked for doesn't show until it's read back,
    // and a device it can't read never shows as muted.
    private BadgeState FromReadings =>
        Readings.Any(r => r.Muted is null) ? BadgeState.Unconfirmed
        : !Readings.Any() ? (On ? BadgeState.Muted : BadgeState.Hidden)
        : Readings.All(r => r.Muted == true) ? BadgeState.Muted
        : On ? BadgeState.Live
        : BadgeState.Hidden;

    private IEnumerable<(BadgeDevices Device, bool? Muted)> Readings =>
        _mics.Values.Select(muted => (BadgeDevices.Mic, muted))
            .Concat(HasCamera ? [(BadgeDevices.Camera, Camera)] : Array.Empty<(BadgeDevices, bool?)>());

    /// <summary>
    /// The key toggles what the readings show: when they read muted, everything unmutes; otherwise,
    /// including a mic unmuted behind Shybun's back or one it can't read, everything mutes. An app on
    /// the real camera is left out, since the key can't change it: the key still unmutes.
    /// </summary>
    /// <returns>The mute to set on every mic.</returns>
    public bool Press() => On = FromReadings != BadgeState.Muted;

    /// <returns>Whether the new mic must be muted, so a headset plugged in while muted isn't live.</returns>
    public bool Added(string id, bool? muted)
    {
        _mics[id] = muted;
        return On && muted != true;
    }

    public void Removed(string id) => _mics.Remove(id);

    /// <summary>Records the Shybun camera's Mute as read back, after every write.</summary>
    public void ReadCamera(bool? off)
    {
        HasCamera = true;
        Camera = off;
    }

    /// <summary>Records whether an app streams a real camera other than through the Shybun camera.</summary>
    public void ReadRealCamera(bool inUse) => RealCameraInUse = inUse;

    /// <summary>No Shybun camera is installed, so the badge leaves the camera out.</summary>
    public void RemoveCamera()
    {
        HasCamera = false;
        Camera = null;
    }

    /// <summary>
    /// Records what Windows reports. A mic unmuted behind Shybun's back shows as live, and is
    /// never muted again until the key is pressed: the unmute may have been on purpose.
    /// </summary>
    public void Read(string id, bool? muted)
    {
        if (_mics.ContainsKey(id))
        {
            _mics[id] = muted;
        }
    }

    private BadgeDevices Devices(Func<(BadgeDevices Device, bool? Muted), bool> match) =>
        Readings.Where(match).Aggregate(BadgeDevices.None, (all, r) => all | r.Device);
}
