namespace Shybun.Core;

/// <summary>
/// What the tray app and the Shybun camera agree on. The camera is a separate DLL that Windows loads into other
/// processes, so these values are everything they share.
/// </summary>
public static class CameraContract
{
    /// <summary>The media source's COM class, which the virtual camera names and Windows loads.</summary>
    public const string ClassId = "807a9636-e6fd-4ac3-b184-27c3027c1cac";

    /// <summary>Under HKEY_CURRENT_USER: where the tray app writes Mute and the camera reads it.</summary>
    public const string MuteKeyPath = @"Software\Shybun\Camera";

    /// <summary>A DWORD, 1 while Mute is on.</summary>
    public const string MuteValueName = "Mute";

    /// <summary>The virtual camera attribute that names, by SID, the user whose Mute the camera follows.</summary>
    public static readonly Guid UserAttribute = new("5d0bfc1c-3f53-4b7e-9b1e-2c47a3b8e0d4");
}
