using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Shybun.Camera;
using Windows.Win32;
using Windows.Win32.Media.MediaFoundation;

namespace Shybun.Tests;

/// <summary>
/// M2's checks on the real camera. They open the webcam, so run them only when nobody is on a call. Each read keeps
/// only a frame count and an average brightness, never a frame. Install the Shybun camera's media source first:
/// scripts/install-camera.ps1, elevated.
/// </summary>
public class CameraLiveTests
{
    private const string TestCameraName = "Shybun camera (test)";

    [Fact(Explicit = true)]
    public void The_Shybun_camera_passes_frames_through_and_sends_the_camera_off_image_while_muted()
    {
        PInvoke.MFStartup(PInvoke.MF_VERSION, 0).ThrowOnFailure();
        var hadKey = Registry.CurrentUser.OpenSubKey(@"Software\Shybun") is not null;
        try
        {
            var real = Cameras().First(camera => camera.Name != TestCameraName);
            var direct = Watch(Open(real.Link));
            Report($"{real.Name} on its own", direct);
            Assert.True(direct.Frames >= 10, $"{real.Name} gave {direct.Frames} frames on its own");
            Assert.True(direct.Brightness > 20, $"{real.Name} is too dark to compare ({direct.Brightness:F0}); light the room");

            using var mute = MuteKey();
            var shybun = CreateTestCamera(real.Link);
            try
            {
                mute.SetValue(MuteSwitch.ValueName, 0);
                var live = Watch(Open(WaitForCamera(TestCameraName).Link));
                Report("Through Shybun, live", live);
                Assert.True(live.Frames >= 10, $"The Shybun camera gave {live.Frames} frames while live");
                Assert.InRange(live.Brightness, direct.Brightness * 0.7, direct.Brightness * 1.3);

                mute.SetValue(MuteSwitch.ValueName, 1);
                var muted = Watch(Open(WaitForCamera(TestCameraName).Link));
                var cameraOff = AverageLuma(CameraOff.Frame(PInvoke.MFVideoFormat_NV12, muted.Width, muted.Height, 0)!, muted.Width * muted.Height);
                Report($"Through Shybun, muted (the camera-off image is {cameraOff:F0})", muted);
                Assert.True(muted.Frames >= 10, $"The Shybun camera gave {muted.Frames} frames while muted");
                Assert.InRange(muted.Brightness, cameraOff - 6, cameraOff + 6);
            }
            finally
            {
                shybun.Remove();
                shybun.Shutdown();
            }
        }
        finally
        {
            if (!hadKey)
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Shybun", throwOnMissingSubKey: false);
            }

            PInvoke.MFShutdown();
        }
    }

    private static void Report(string what, (int Frames, double Brightness, int Width, int Height) seen) =>
        TestContext.Current.SendDiagnosticMessage($"{what}: {seen.Frames} frames at {seen.Width}x{seen.Height}, brightness {seen.Brightness:F0}");

    // The key the tray app writes Mute to. Local Service, which the Frame Server runs as, may read it.
    private static RegistryKey MuteKey()
    {
        var key = Registry.CurrentUser.CreateSubKey(MuteSwitch.KeyPath);
        using var permissions = Registry.CurrentUser.OpenSubKey(
            MuteSwitch.KeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ReadPermissions | RegistryRights.ChangePermissions)!;
        var acl = permissions.GetAccessControl();
        acl.AddAccessRule(new RegistryAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null), RegistryRights.QueryValues | RegistryRights.Notify, AccessControlType.Allow));
        permissions.SetAccessControl(acl);
        return key;
    }

    // Session lifetime: Windows removes the camera when the test lets go of it, even if the test dies.
    private static IMFVirtualCamera CreateTestCamera(string realLink)
    {
        PInvoke.MFCreateVirtualCamera(
            MFVirtualCameraType.MFVirtualCameraType_SoftwareCameraSource,
            MFVirtualCameraLifetime.MFVirtualCameraLifetime_Session,
            MFVirtualCameraAccess.MFVirtualCameraAccess_CurrentUser,
            TestCameraName,
            $"{{{Exports.ClassId}}}",
            default,
            out var camera).ThrowOnFailure();
        camera.SetString(MuteSwitch.UserAttribute, WindowsIdentity.GetCurrent().User!.Value);
        camera.AddDeviceSourceInfo(realLink);
        camera.Start(null);
        return camera;
    }

    private static (string Name, string Link) WaitForCamera(string name)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 5000)
        {
            if (Cameras().FirstOrDefault(camera => camera.Name == name) is { Link: not null } found)
            {
                return found;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException($"{name} never appeared among the cameras. Is the media source installed?");
    }

    private static unsafe List<(string Name, string Link)> Cameras()
    {
        PInvoke.MFCreateAttributes(out var attributes, 1).ThrowOnFailure();
        attributes.SetGUID(PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
        PInvoke.MFEnumDeviceSources(attributes, out var activates, out var count).ThrowOnFailure();
        var cameras = new List<(string, string)>();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var activate = ComInterfaceMarshaller<IMFActivate>.ConvertToManaged(activates[i])!;
                Marshal.Release((nint)activates[i]);
                cameras.Add((Text(activate, PInvoke.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME), Text(activate, PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK)));
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)activates);
        }

        return cameras;
    }

    private static unsafe string Text(IMFAttributes attributes, Guid key)
    {
        Windows.Win32.Foundation.PWSTR value;
        attributes.GetAllocatedString(&key, &value, out _);
        try
        {
            return value.ToString();
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)value.Value);
        }
    }

    private static IMFMediaSource Open(string link)
    {
        PInvoke.MFCreateAttributes(out var attributes, 2).ThrowOnFailure();
        attributes.SetGUID(PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
        attributes.SetString(PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK, link);
        PInvoke.MFCreateDeviceSource(attributes, out var source).ThrowOnFailure();
        return source;
    }

    // Three seconds of frames, as NV12. Only the count and the last second's average brightness are kept: the
    // camera's auto-exposure settles in the first.
    private static unsafe (int Frames, double Brightness, int Width, int Height) Watch(IMFMediaSource source)
    {
        const uint FirstVideoStream = 0xFFFFFFFC;
        try
        {
            PInvoke.MFCreateAttributes(out var attributes, 1).ThrowOnFailure();
            attributes.SetUINT32(PInvoke.MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING, 1);
            PInvoke.MFCreateSourceReaderFromMediaSource(source, attributes, out var reader).ThrowOnFailure();
            PInvoke.MFCreateMediaType(out var nv12).ThrowOnFailure();
            nv12.SetGUID(PInvoke.MF_MT_MAJOR_TYPE, PInvoke.MFMediaType_Video);
            nv12.SetGUID(PInvoke.MF_MT_SUBTYPE, PInvoke.MFVideoFormat_NV12);
            reader.SetCurrentMediaType(FirstVideoStream, null, nv12);
            reader.GetCurrentMediaType(FirstVideoStream, out var current);
            current.GetUINT64(PInvoke.MF_MT_FRAME_SIZE, out var size);
            var (width, height) = ((int)(size >> 32), (int)(size & 0xFFFFFFFF));

            var frames = 0;
            var settled = new List<double>();
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 3000)
            {
                IMFSample_unmanaged* raw;
                uint flags;
                reader.ReadSample(FirstVideoStream, 0, null, &flags, null, &raw);
                Assert.True((flags & 1) == 0, "The camera reported an error");
                if (raw is null)
                {
                    continue;
                }

                var sample = ComInterfaceMarshaller<IMFSample>.ConvertToManaged(raw)!;
                Marshal.Release((nint)raw);
                frames++;
                if (clock.ElapsedMilliseconds > 2000)
                {
                    settled.Add(AverageLuma(sample, width * height));
                }
            }

            return (frames, settled.Count == 0 ? 0 : settled.Average(), width, height);
        }
        finally
        {
            source.Shutdown();
        }
    }

    private static unsafe double AverageLuma(IMFSample sample, int lumaBytes)
    {
        sample.ConvertToContiguousBuffer(out var buffer);
        byte* data;
        uint length;
        buffer.Lock(&data, null, &length);
        try
        {
            return AverageLuma(new ReadOnlySpan<byte>(data, (int)length), lumaBytes);
        }
        finally
        {
            buffer.Unlock();
        }
    }

    // NV12 starts with one luma byte per pixel.
    private static double AverageLuma(ReadOnlySpan<byte> nv12, int lumaBytes)
    {
        var count = Math.Min(lumaBytes, nv12.Length);
        long sum = 0;
        foreach (var luma in nv12[..count])
        {
            sum += luma;
        }

        return count == 0 ? 0 : (double)sum / count;
    }
}
