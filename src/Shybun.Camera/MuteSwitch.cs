using System.Security.AccessControl;
using Microsoft.Win32;
using Shybun.Core;

namespace Shybun.Camera;

/// <summary>
/// Mute as the tray app last set it in the user's registry. The Frame Server runs as Local Service and reaches the
/// key through HKEY_USERS, so the tray app's key grants Local Service read access.
/// </summary>
public sealed class MuteSwitch(string? userSid, string keyPath = CameraContract.MuteKeyPath)
{
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
                // Only the right the key grants Local Service: a plain open asks for full read access and is refused.
                _key ??= Registry.Users.OpenSubKey($@"{userSid}\{keyPath}", RegistryRights.QueryValues);
                if (_key?.GetValue(CameraContract.MuteValueName) is int mute)
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
