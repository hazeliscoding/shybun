using Shybun.Camera;
using Windows.Win32;

namespace Shybun.Tests;

public class CameraOffTests
{
    // Shiori's heather grey, #BCB8B9, in BT.601 limited range.
    private const byte GreyY = 175, GreyU = 128, GreyV = 130;

    [Fact]
    public void An_NV12_frame_is_grey_at_the_edges_and_Shiori_in_the_middle()
    {
        var frame = CameraOff.Frame(PInvoke.MFVideoFormat_NV12, 1280, 720, 0)!;

        Assert.Equal(1280 * 720 * 3 / 2, frame.Length);
        Assert.Equal(GreyY, frame[0]);
        Assert.Equal(GreyU, frame[1280 * 720]);
        Assert.Equal(GreyV, frame[(1280 * 720) + 1]);
        Assert.NotEqual(GreyY, frame[(600 * 1280) + 640]);
    }

    [Fact]
    public void Padded_lines_start_at_the_stride()
    {
        var frame = CameraOff.Frame(PInvoke.MFVideoFormat_NV12, 640, 480, 704)!;

        Assert.Equal(704 * 480 * 3 / 2, frame.Length);
        Assert.Equal(GreyY, frame[704]);
        Assert.Equal(0, frame[640]);
    }

    [Fact]
    public void A_YUY2_frame_starts_grey()
    {
        var frame = CameraOff.Frame(PInvoke.MFVideoFormat_YUY2, 640, 480, 0)!;

        Assert.Equal(640 * 2 * 480, frame.Length);
        Assert.Equal([GreyY, GreyU, GreyY, GreyV], frame[..4]);
    }

    [Fact]
    public void An_RGB32_frame_starts_grey() =>
        Assert.Equal([0xB9, 0xB8, 0xBC, 0xFF], CameraOff.Frame(PInvoke.MFVideoFormat_RGB32, 640, 480, 0)![..4]);

    [Fact]
    public void An_MJPG_frame_is_a_whole_JPEG()
    {
        var frame = CameraOff.Frame(PInvoke.MFVideoFormat_MJPG, 1920, 1080, 0)!;

        Assert.Equal([0xFF, 0xD8], frame[..2]);
        Assert.Equal([0xFF, 0xD9], frame[^2..]);
    }

    [Fact]
    public void A_format_it_cant_draw_gives_no_frame()
    {
        Assert.Null(CameraOff.Frame(PInvoke.MFVideoFormat_H264, 1280, 720, 0));
        Assert.Null(CameraOff.Frame(PInvoke.MFVideoFormat_NV12, 641, 480, 0));
    }
}
