using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Shybun.Camera;
using Shybun.Core;
using Windows.Win32;
using Windows.Win32.Media.MediaFoundation;
using Windows.Win32.System.Com;

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
    public void The_Shybun_camera_passes_frames_through_and_sends_the_camera_off_image_while_muted() => Run(() =>
    {
        var real = RealCamera();
        var direct = ReadDirectly(real);
        using var mute = MuteKey();
        var shybun = Within(() => CreateTestCamera(real.Link));
        try
        {
            CheckLiveThenMuted(mute, () => Open(WaitForCamera(TestCameraName).Link), direct);
        }
        finally
        {
            shybun.Remove();
            shybun.Shutdown();
        }
    });

    // Loads the media source into this process, as Microsoft's HWMediaSourceTest does, so a fault in it shows here
    // rather than inside the Frame Server. It needs no install.
    [Fact(Explicit = true)]
    public void The_media_source_passes_frames_through_and_sends_the_camera_off_image_in_this_process() => Run(() =>
    {
        var real = RealCamera();
        var direct = ReadDirectly(real);
        using var mute = MuteKey();
        CheckLiveThenMuted(mute, () => ActivateHere(real.Activate), direct);
    });

    // The whole chain: Shybun makes its camera, and the key switches it with the mics. It presses Win+Shift+Q and
    // mutes every mic, so keep off the keyboard and off calls. The Shybun camera stays afterwards, as it would for
    // anyone who installed it.
    [Fact(Explicit = true)]
    public void The_key_switches_the_Shybun_camera_to_the_camera_off_image_and_back() => Run(() =>
    {
        var mics = LiveTests.Mics();
        Assert.All(mics, mic => Assert.False(LiveTests.IsMuted(mic.Volume), $"Unmute {mic.Id} before running this test."));
        var direct = ReadDirectly(RealCamera());

        using var shybun = LiveTests.StartShybun();
        try
        {
            // Windows' name for the camera Shybun calls "Shybun camera".
            var camera = WaitForCamera("Shybun camera (Windows Virtual Camera)");
            var live = Within(() => Watch(Open(camera.Link)));
            Report("Shybun camera, live", live);
            Assert.InRange(live.Brightness, direct.Brightness * 0.7, direct.Brightness * 1.3);

            LiveTests.PressMuteKey();
            Thread.Sleep(1000);
            var muted = Within(() => Watch(Open(camera.Link)));
            var cameraOff = AverageLuma(CameraOff.Frame(PInvoke.MFVideoFormat_NV12, muted.Width, muted.Height, 0)!, muted.Width * muted.Height);
            Report($"Shybun camera after the key (the camera-off image is {cameraOff:F0})", muted);
            Assert.InRange(muted.Brightness, cameraOff - 6, cameraOff + 6);
            Assert.All(mics, mic => Assert.True(LiveTests.IsMuted(mic.Volume), $"{mic.Id} isn't muted"));

            LiveTests.PressMuteKey();
            Thread.Sleep(1000);
            var back = Within(() => Watch(Open(camera.Link)));
            Report("Shybun camera after the second press", back);
            Assert.InRange(back.Brightness, direct.Brightness * 0.7, direct.Brightness * 1.3);
        }
        finally
        {
            shybun.Kill();
            LiveTests.Unmute(mics);
        }
    });

    // Leaves Mute as the test found it: a Shybun running here keeps its own, and a key the test made goes.
    private static void Run(Action test)
    {
        PInvoke.MFStartup(PInvoke.MF_VERSION, 0).ThrowOnFailure();
        var hadKey = Registry.CurrentUser.OpenSubKey(@"Software\Shybun") is not null;
        var before = Registry.CurrentUser.OpenSubKey(CameraContract.MuteKeyPath)?.GetValue(CameraContract.MuteValueName);
        try
        {
            test();
        }
        finally
        {
            if (!hadKey)
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Shybun", throwOnMissingSubKey: false);
            }
            else if (Registry.CurrentUser.OpenSubKey(CameraContract.MuteKeyPath, writable: true) is { } key)
            {
                using (key)
                {
                    if (before is null)
                    {
                        key.DeleteValue(CameraContract.MuteValueName, throwOnMissingValue: false);
                    }
                    else
                    {
                        key.SetValue(CameraContract.MuteValueName, before);
                    }
                }
            }

            PInvoke.MFShutdown();
        }
    }

    private static (string Name, string Link, IMFActivate Activate) RealCamera() =>
        Cameras().First(camera => !camera.Name.StartsWith(TestCameraName, StringComparison.Ordinal));

    private static (int Frames, double Brightness, int Width, int Height) ReadDirectly((string Name, string Link, IMFActivate Activate) real)
    {
        var direct = Within(() => Watch(Open(real.Link)));
        Report($"{real.Name} on its own", direct);
        Assert.True(direct.Frames >= 10, $"{real.Name} gave {direct.Frames} frames on its own");
        Assert.True(direct.Brightness > 20, $"{real.Name} is too dark to compare ({direct.Brightness:F0}); light the room");
        return direct;
    }

    // With Mute off the Shybun camera must look like the real one; with it on, like the camera-off image.
    private static void CheckLiveThenMuted(RegistryKey mute, Func<IMFMediaSource> open, (int Frames, double Brightness, int Width, int Height) direct)
    {
        mute.SetValue(CameraContract.MuteValueName, 0);
        var live = Within(() => Watch(open()));
        Report("Through Shybun, live", live);
        Assert.True(live.Frames >= 10, $"The Shybun camera gave {live.Frames} frames while live");
        Assert.InRange(live.Brightness, direct.Brightness * 0.7, direct.Brightness * 1.3);

        mute.SetValue(CameraContract.MuteValueName, 1);
        var muted = Within(() => Watch(open()));
        var cameraOff = AverageLuma(CameraOff.Frame(PInvoke.MFVideoFormat_NV12, muted.Width, muted.Height, 0)!, muted.Width * muted.Height);
        Report($"Through Shybun, muted (the camera-off image is {cameraOff:F0})", muted);
        Assert.True(muted.Frames >= 10, $"The Shybun camera gave {muted.Frames} frames while muted");
        Assert.InRange(muted.Brightness, cameraOff - 6, cameraOff + 6);
    }

    // What the Frame Server does: ask the class for an activator, hand it the real camera, and activate it.
    private static unsafe IMFMediaSource ActivateHere(IMFActivate realCamera)
    {
        var clsid = new Guid(CameraContract.ClassId);
        var iid = typeof(IClassFactory).GUID;
        void* factory;
        delegate* unmanaged<Guid*, Guid*, void**, int> getClassObject = &Exports.DllGetClassObject;
        Assert.Equal(0, getClassObject(&clsid, &iid, &factory));
        iid = typeof(IMFActivate).GUID;
        Wrap<IClassFactory>(factory).CreateInstance(null, &iid, out var instance);
        var activator = Wrap<IMFActivate>(instance);

        PInvoke.MFCreateCollection(out var cameras).ThrowOnFailure();
        cameras.AddElement(realCamera);
        activator.SetUnknown(PInvoke.MF_VIRTUALCAMERA_ASSOCIATED_CAMERA_SOURCES, cameras);
        activator.SetString(CameraContract.UserAttribute, WindowsIdentity.GetCurrent().User!.Value);
        iid = typeof(IMFMediaSource).GUID;
        activator.ActivateObject(&iid, out var source);
        return Wrap<IMFMediaSource>(source);
    }

    // The media source's objects are managed in this process too. Wrapping them as COM objects, without unwrapping,
    // reaches them through their COM interfaces as the Frame Server does, not through their .NET types.
    private static unsafe T Wrap<T>(void* pointer)
        where T : class
    {
        var wrapper = (T)new StrategyBasedComWrappers().GetOrCreateObjectForComInstance((nint)pointer, CreateObjectFlags.None);
        Marshal.Release((nint)pointer);
        return wrapper;
    }

    private static void Report(string what, (int Frames, double Brightness, int Width, int Height) seen) =>
        TestContext.Current.SendDiagnosticMessage($"{what}: {seen.Frames} frames at {seen.Width}x{seen.Height}, brightness {seen.Brightness:F0}");

    // A camera that sends nothing blocks ReadSample for good, so each step gets a deadline.
    private static T Within<T>(Func<T> step)
    {
        var task = Task.Run(step);
        return task.Wait(TimeSpan.FromSeconds(20)) ? task.Result : throw new TimeoutException("The camera step didn't finish within 20 s.");
    }

    // The key the tray app writes Mute to. Local Service, which the Frame Server runs as, may read it.
    private static RegistryKey MuteKey()
    {
        var key = Registry.CurrentUser.CreateSubKey(CameraContract.MuteKeyPath);
        using var permissions = Registry.CurrentUser.OpenSubKey(
            CameraContract.MuteKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ReadPermissions | RegistryRights.ChangePermissions)!;
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
            $"{{{CameraContract.ClassId}}}",
            default,
            out var camera).ThrowOnFailure();
        camera.SetString(CameraContract.UserAttribute, WindowsIdentity.GetCurrent().User!.Value);
        camera.AddDeviceSourceInfo(realLink);
        camera.Start(null);
        return camera;
    }

    private static (string Name, string Link, IMFActivate Activate) WaitForCamera(string name)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 5000)
        {
            // Windows lists a virtual camera as "<name> (Windows Virtual Camera)".
            if (Cameras().FirstOrDefault(camera => camera.Name.StartsWith(name, StringComparison.Ordinal)) is { Link: not null } found)
            {
                return found;
            }

            Thread.Sleep(250);
        }

        throw new TimeoutException(
            $"{name} never appeared among the cameras ({string.Join(", ", Cameras().Select(camera => camera.Name))}). Is the media source installed?");
    }

    private static unsafe List<(string Name, string Link, IMFActivate Activate)> Cameras()
    {
        PInvoke.MFCreateAttributes(out var attributes, 1).ThrowOnFailure();
        attributes.SetGUID(PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
        PInvoke.MFEnumDeviceSources(attributes, out var activates, out var count).ThrowOnFailure();
        var cameras = new List<(string, string, IMFActivate)>();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var activate = ComInterfaceMarshaller<IMFActivate>.ConvertToManaged(activates[i])!;
                Marshal.Release((nint)activates[i]);
                cameras.Add((Text(activate, PInvoke.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME), Text(activate, PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK), activate));
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

                frames++;
                if (clock.ElapsedMilliseconds > 2000)
                {
                    settled.Add(AverageLuma(raw, width * height));
                }

                Marshal.Release((nint)raw);
                // A frame's wrappers hold one of the camera's few buffers until they're collected, and the camera
                // stops sending when it has none left.
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            return (frames, settled.Count == 0 ? 0 : settled.Average(), width, height);
        }
        finally
        {
            source.Shutdown();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe double AverageLuma(IMFSample_unmanaged* raw, int lumaBytes)
    {
        var sample = ComInterfaceMarshaller<IMFSample>.ConvertToManaged(raw)!;
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
