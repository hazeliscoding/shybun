using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
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
    public void The_Shybun_camera_passes_the_real_cameras_frames_through()
    {
        PInvoke.MFStartup(PInvoke.MF_VERSION, 0).ThrowOnFailure();
        try
        {
            var real = Cameras().First(camera => camera.Name != TestCameraName);
            var direct = Watch(Open(real.Link));
            Assert.True(direct.Frames >= 10, $"{real.Name} gave {direct.Frames} frames on its own");
            Assert.True(direct.Brightness > 20, $"{real.Name} is too dark to compare ({direct.Brightness:F0}); light the room");

            var shybun = CreateTestCamera(real.Link);
            try
            {
                var wrapped = Watch(Open(WaitForCamera(TestCameraName).Link));
                TestContext.Current.SendDiagnosticMessage(
                    $"{real.Name}: {direct.Frames} frames, brightness {direct.Brightness:F0}; through Shybun: {wrapped.Frames}, {wrapped.Brightness:F0}");
                Assert.True(wrapped.Frames >= 10, $"The Shybun camera gave {wrapped.Frames} frames");
                Assert.InRange(wrapped.Brightness, direct.Brightness * 0.7, direct.Brightness * 1.3);
            }
            finally
            {
                shybun.Remove();
                shybun.Shutdown();
            }
        }
        finally
        {
            PInvoke.MFShutdown();
        }
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
    private static unsafe (int Frames, double Brightness) Watch(IMFMediaSource source)
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
            var lumaBytes = (int)(size >> 32) * (int)(size & 0xFFFFFFFF);

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
                    settled.Add(AverageLuma(sample, lumaBytes));
                }
            }

            return (frames, settled.Count == 0 ? 0 : settled.Average());
        }
        finally
        {
            source.Shutdown();
        }
    }

    // NV12 starts with one luma byte per pixel.
    private static unsafe double AverageLuma(IMFSample sample, int lumaBytes)
    {
        sample.ConvertToContiguousBuffer(out var buffer);
        byte* data;
        uint length;
        buffer.Lock(&data, null, &length);
        try
        {
            var count = Math.Min(lumaBytes, (int)length);
            long sum = 0;
            for (var i = 0; i < count; i++)
            {
                sum += data[i];
            }

            return count == 0 ? 0 : (double)sum / count;
        }
        finally
        {
            buffer.Unlock();
        }
    }
}
