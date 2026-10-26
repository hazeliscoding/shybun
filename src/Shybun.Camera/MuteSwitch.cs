using Microsoft.Win32;

namespace Shybun.Camera;

/// <summary>
/// Mute as the tray app last set it in the user's registry. The Frame Server runs as Local Service and reaches the
/// key through HKEY_USERS, so the tray app's key grants Local Service read access.
/// </summary>
public sealed class MuteSwitch(string? userSid, string keyPath = MuteSwitch.KeyPath)
{
    public const string KeyPath = @"Software\Shybun\Camera";
    public const string ValueName = "Mute";

    /// <summary>The virtual camera attribute that names the user, by SID, whose Mute the camera follows.</summary>
    public static readonly Guid UserAttribute = new("5d0bfc1c-3f53-4b7e-9b1e-2c47a3b8e0d4");

    private RegistryKey? _key;

    /// <summary>
    /// Read for every frame. It fails closed: a camera that can't read Mute sends the camera-off image, because
    /// showing someone who believes they're hidden is worse than hiding someone who isn't.
    /// </summary>
    public bool IsOn
    {
        get
        {
            if (userSid is null)
            {
                return true;
            }

            try
            {
                _key ??= Registry.Users.OpenSubKey($@"{userSid}\{keyPath}");
                if (_key?.GetValue(ValueName) is int mute)
                {
                    return mute != 0;
                }
            }
            catch (Exception)
            {
            }

            // Open the key again next time, in case the tray app has only now made it.
            _key?.Dispose();
            _key = null;
            return true;
        }
    }
}
