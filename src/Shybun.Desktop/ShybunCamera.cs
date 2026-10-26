using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Shybun.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace Shybun.Desktop;

/// <summary>
/// The Shybun camera from the tray app's side. The camera itself runs in Windows' Frame Server, so the tray app only
/// makes the virtual camera around the real one and writes Mute where the camera reads it.
/// </summary>
internal sealed class ShybunCamera : IDisposable
{
    public const string Name = "Shybun camera";

    // Windows adds this to every virtual camera's name, the Shybun camera's included.
    private const string VirtualSuffix = " (Windows Virtual Camera)";

    private readonly Action<string> _changed;
    private bool _mfStarted;

    // Kept for the process's life: the camera outlives Shybun anyway, since its lifetime is the system's.
    private IMFVirtualCamera? _camera;

    /// <param name="mute">Mute as it stands, written only on the first start, so a start never changes the camera.</param>
    /// <param name="changed">Gets the new <see cref="Status"/>, on a background thread.</param>
    public ShybunCamera(bool mute, Action<string> changed)
    {
        _changed = changed;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            Status = "The Shybun camera needs Windows 11";
            return;
        }

        // The one-time install registers the media source for the whole machine. Reading that needs no admin.
        using (var installed = Registry.LocalMachine.OpenSubKey($@"Software\Classes\CLSID\{{{CameraContract.ClassId}}}\InprocServer32"))
        {
            if (installed is null)
            {
                Status = "The Shybun camera isn't installed";
                return;
            }
        }

        PrepareMute(mute);
        Status = "Starting the Shybun camera";
        // Creating it waits for Windows to load the media source, so the tray doesn't wait with it.
        Task.Run(() => _changed(Status = Create()));
    }

    /// <summary>One line for the tray menu: the camera it wraps, or why there's no Shybun camera.</summary>
    public string Status { get; private set; }

    public bool Exists => _camera is not null;

    /// <summary>Tells the camera to send the camera-off image, or the real picture again. False if it couldn't.</summary>
    public bool WriteMute(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(CameraContract.MuteKeyPath, writable: true);
            key?.SetValue(CameraContract.MuteValueName, on ? 1 : 0, RegistryValueKind.DWord);
            return key is not null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_mfStarted)
        {
            PInvoke.MFShutdown();
        }
    }

    private string Create()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return Status;
        }

        try
        {
            PInvoke.MFStartup(PInvoke.MF_VERSION, 0).ThrowOnFailure();
            _mfStarted = true;
            if (RealCameras().FirstOrDefault() is not { Link: not null } real)
            {
                return "No camera for the Shybun camera to wrap";
            }

            // The system's lifetime: apps keep the Shybun camera selected while Shybun isn't running. The same
            // arguments open the same camera again on the next start.
            PInvoke.MFCreateVirtualCamera(
                MFVirtualCameraType.MFVirtualCameraType_SoftwareCameraSource,
                MFVirtualCameraLifetime.MFVirtualCameraLifetime_System,
                MFVirtualCameraAccess.MFVirtualCameraAccess_CurrentUser,
                Name,
                $"{{{CameraContract.ClassId}}}",
                default,
                out var camera).ThrowOnFailure();
            camera.SetString(CameraContract.UserAttribute, WindowsIdentity.GetCurrent().User!.Value);
            camera.AddDeviceSourceInfo(real.Link);
            camera.Start(null);
            _camera = camera;
            return $"{Name} wraps {real.Name}";
        }
        catch (Exception)
        {
            return "The Shybun camera didn't start";
        }
    }

    // Local Service, which the Frame Server runs as, may read Mute and nothing else. A first start writes Mute as it
    // stands; without a value, the camera sends the camera-off image.
    private static void PrepareMute(bool mute)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(CameraContract.MuteKeyPath);
            using (var permissions = Registry.CurrentUser.OpenSubKey(
                CameraContract.MuteKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ReadPermissions | RegistryRights.ChangePermissions)!)
            {
                var acl = permissions.GetAccessControl();
                acl.AddAccessRule(new RegistryAccessRule(
                    new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null), RegistryRights.QueryValues, AccessControlType.Allow));
                permissions.SetAccessControl(acl);
            }

            if (key.GetValue(CameraContract.MuteValueName) is null)
            {
                key.SetValue(CameraContract.MuteValueName, mute ? 1 : 0, RegistryValueKind.DWord);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            // The camera then can't read Mute and sends the camera-off image.
        }
    }

    // Every camera Windows lists except virtual ones: a virtual camera can't wrap another.
    private static unsafe List<(string Name, string? Link)> RealCameras()
    {
        PInvoke.MFCreateAttributes(out var attributes, 1).ThrowOnFailure();
        attributes.SetGUID(PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
        PInvoke.MFEnumDeviceSources(attributes, out var activates, out var count).ThrowOnFailure();
        var cameras = new List<(string, string?)>();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var activate = ComInterfaceMarshaller<IMFActivate>.ConvertToManaged(activates[i])!;
                Marshal.Release((nint)activates[i]);
                var name = Text(activate, PInvoke.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME) ?? "";
                if (!name.EndsWith(VirtualSuffix, StringComparison.Ordinal))
                {
                    cameras.Add((name, Text(activate, PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK)));
                }
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)activates);
        }

        return cameras;
    }

    private static unsafe string? Text(IMFAttributes attributes, Guid key)
    {
        PWSTR value;
        try
        {
            attributes.GetAllocatedString(&key, &value, out _);
        }
        catch (COMException)
        {
            return null;
        }

        try
        {
            return value.ToString();
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)value.Value);
        }
    }
}
