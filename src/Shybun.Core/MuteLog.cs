using System.Globalization;

namespace Shybun.Core;

public enum MuteCause
{
    /// <summary>The mute key was pressed.</summary>
    Key,

    /// <summary>A mic appeared while muted, and Shybun muted it.</summary>
    NewMic,

    /// <summary>Something other than Shybun changed a mic.</summary>
    Windows,
}

/// <summary>
/// Rows for the dogfooding log. A row holds the time, the cause and counts of what was read back,
/// never a device name, a sound or anything else about the call.
/// </summary>
public static class MuteLog
{
    public const string Header = "time,cause,mute,mics,muted,live,unconfirmed";

    public static string Row(DateTimeOffset time, MuteCause cause, Mute mute)
    {
        var readings = mute.Mics.Values;
        return string.Join(',',
            time.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            cause switch
            {
                MuteCause.Key => "key",
                MuteCause.NewMic => "new-mic",
                _ => "windows",
            },
            mute.On ? "on" : "off",
            mute.Mics.Count,
            readings.Count(muted => muted == true),
            readings.Count(muted => muted == false),
            readings.Count(muted => muted is null));
    }
}
