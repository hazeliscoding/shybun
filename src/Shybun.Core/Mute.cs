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

    /// <summary>A mic can't be read back.</summary>
    Unconfirmed,
}

/// <summary>
/// Shybun's one on/off state, and what Windows last reported for each mic: <c>true</c> for muted,
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

    // Built only from readings, so a mute Shybun asked for doesn't show until Windows confirms it,
    // and a mic it can't read never shows as muted.
    public BadgeState Badge =>
        _mics.Values.Any(muted => muted is null) ? BadgeState.Unconfirmed
        : _mics.Count == 0 ? (On ? BadgeState.Muted : BadgeState.Hidden)
        : _mics.Values.All(muted => muted == true) ? BadgeState.Muted
        : On ? BadgeState.Live
        : BadgeState.Hidden;

    /// <summary>
    /// The key toggles what the badge shows: when it shows Muted, everything unmutes; otherwise,
    /// including a mic unmuted behind Shybun's back or one it can't read, everything mutes.
    /// </summary>
    /// <returns>The mute to set on every mic.</returns>
    public bool Press() => On = Badge != BadgeState.Muted;

    /// <returns>Whether the new mic must be muted, so a headset plugged in while muted isn't live.</returns>
    public bool Added(string id, bool? muted)
    {
        _mics[id] = muted;
        return On && muted != true;
    }

    public void Removed(string id) => _mics.Remove(id);

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
}
