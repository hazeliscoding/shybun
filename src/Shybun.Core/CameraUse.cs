namespace Shybun.Core;

/// <summary>Who is using a camera, from what Windows reports: each camera's name and the processes streaming it.</summary>
public static class CameraUse
{
    /// <summary>Windows adds this to every virtual camera's name, the Shybun camera's included.</summary>
    public const string VirtualSuffix = " (Windows Virtual Camera)";

    /// <summary>
    /// Whether an app streams a real camera other than through the Shybun camera. Windows credits a camera's use to
    /// the app, so an app on the Shybun camera streams the real camera too; one streaming only the real camera, or
    /// another virtual camera around it, holds it past the Shybun camera.
    /// </summary>
    public static bool Bypassed(IEnumerable<(string Name, IReadOnlyCollection<uint> Streaming)> cameras, string shybunCamera)
    {
        var all = cameras.ToList();
        var throughShybun = all.Where(c => c.Name == shybunCamera).SelectMany(c => c.Streaming).ToHashSet();
        return all.Where(c => !c.Name.EndsWith(VirtualSuffix, StringComparison.Ordinal))
            .SelectMany(c => c.Streaming)
            .Any(id => !throughShybun.Contains(id));
    }
}
