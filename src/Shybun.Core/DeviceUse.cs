namespace Shybun.Core;

/// <summary>Whether an app is using a mic or a camera, which holds back a restart into an update.</summary>
public static class DeviceUse
{
    /// <summary>
    /// Windows' privacy settings keep each app's last start and stop of a mic or a camera. While an app is using one,
    /// its stop is 0.
    /// </summary>
    public static bool InUse(IEnumerable<(long Start, long Stop)> apps) => apps.Any(app => app.Start != 0 && app.Stop == 0);
}
