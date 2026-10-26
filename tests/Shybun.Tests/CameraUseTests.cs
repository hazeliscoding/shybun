using Shybun.Core;

namespace Shybun.Tests;

public class CameraUseTests
{
    private const string Webcam = "HD Webcam";
    private const string Shybun = "Shybun camera (Windows Virtual Camera)";

    private static bool Bypassed(params (string Name, uint[] Streaming)[] cameras) =>
        CameraUse.Bypassed(cameras.Select(c => (c.Name, (IReadOnlyCollection<uint>)c.Streaming)), Shybun);

    [Fact]
    public void An_app_on_the_Shybun_camera_streams_the_webcam_too_and_is_fine() =>
        Assert.False(Bypassed((Webcam, [7]), (Shybun, [7])));

    [Fact]
    public void An_app_streaming_only_the_webcam_bypasses_the_Shybun_camera() =>
        Assert.True(Bypassed((Webcam, [7, 9]), (Shybun, [7])));

    [Fact]
    public void An_app_on_another_virtual_camera_around_the_webcam_bypasses_it() =>
        Assert.True(Bypassed((Webcam, [9]), ("Studio Effects (Windows Virtual Camera)", [9]), (Shybun, [])));

    [Fact]
    public void Nobody_streaming_is_no_bypass() =>
        Assert.False(Bypassed((Webcam, []), (Shybun, [])));
}
