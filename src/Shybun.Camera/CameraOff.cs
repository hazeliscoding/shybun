using System.Collections.Concurrent;
using Windows.Win32;
using Windows.Win32.Graphics.Imaging;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;

namespace Shybun.Camera;

/// <summary>
/// The camera-off image: Shiori's hidden-and-quiet sticker on a flat heather-grey frame, in the format and size of
/// the real camera's stream. The Shybun camera sends it in place of every frame while Mute is on.
/// </summary>
public static unsafe class CameraOff
{
    // Shiori's joggers, from docs/brand/README.md.
    private const byte GreyR = 0xBC, GreyG = 0xB8, GreyB = 0xB9;

    // She stands on the bottom edge and fills this much of the height, like someone sitting at a webcam.
    private const double StickerHeight = 0.9;

    private static readonly ConcurrentDictionary<(Guid, int, int, int), byte[]?> Frames = new();

    /// <summary>
    /// One frame's bytes, with <paramref name="stride"/> bytes per line (0 for the format's default), or null for a
    /// format it can't draw.
    /// </summary>
    public static byte[]? Frame(Guid subtype, int width, int height, int stride) =>
        Frames.GetOrAdd((subtype, width, height, stride), key => Draw(key.Item1, key.Item2, key.Item3, key.Item4));

    private static byte[]? Draw(Guid subtype, int width, int height, int stride)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        if (subtype == PInvoke.MFVideoFormat_NV12)
        {
            return width % 2 == 0 && height % 2 == 0 ? Nv12(Compose(width, height), width, height, Math.Max(stride, width)) : null;
        }

        if (subtype == PInvoke.MFVideoFormat_YUY2)
        {
            return width % 2 == 0 ? Yuy2(Compose(width, height), width, height, Math.Max(stride, width * 2)) : null;
        }

        if (subtype == PInvoke.MFVideoFormat_RGB32)
        {
            // A negative stride means bottom-up lines, which no webcam was seen to send.
            return stride >= 0 ? Rgb32(Compose(width, height), width, height, Math.Max(stride, width * 4)) : null;
        }

        return subtype == PInvoke.MFVideoFormat_MJPG ? Jpeg(Compose(width, height), width, height) : null;
    }

    // Opaque BGRA, top-down, four bytes a pixel.
    private static byte[] Compose(int width, int height)
    {
        var size = Math.Min((int)Math.Round(height * StickerHeight), width);
        var sticker = Sticker(size);
        var left = (width - size) / 2;
        var top = height - size;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int b = GreyB, g = GreyG, r = GreyR;
                var sx = x - left;
                var sy = y - top;
                if (sx >= 0 && sx < size && sy >= 0)
                {
                    // Premultiplied alpha: her colour plus whatever grey shows through.
                    var s = ((sy * size) + sx) * 4;
                    var through = 255 - sticker[s + 3];
                    b = sticker[s] + (b * through / 255);
                    g = sticker[s + 1] + (g * through / 255);
                    r = sticker[s + 2] + (r * through / 255);
                }

                var p = ((y * width) + x) * 4;
                pixels[p] = (byte)b;
                pixels[p + 1] = (byte)g;
                pixels[p + 2] = (byte)r;
                pixels[p + 3] = 255;
            }
        }

        return pixels;
    }

    // The sticker as premultiplied BGRA, scaled to size x size.
    private static byte[] Sticker(int size)
    {
        using var resource = typeof(CameraOff).Assembly.GetManifestResourceStream("hidden-and-quiet.png")!;
        // Pinned for good: WIC reads the PNG through this memory after InitializeFromMemory returns.
        var png = GC.AllocateUninitializedArray<byte>((int)resource.Length, pinned: true);
        resource.ReadExactly(png);

        var wic = Factory();
        wic.CreateStream(out var stream);
        stream.InitializeFromMemory(png, (uint)png.Length);
        var decoder = wic.CreateDecoderFromStream(stream, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand);
        decoder.GetFrame(0, out var frame);
        wic.CreateFormatConverter(out var premultiplied);
        var format = PInvoke.GUID_WICPixelFormat32bppPBGRA;
        premultiplied.Initialize(frame, &format, WICBitmapDitherType.WICBitmapDitherTypeNone, null!, 0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
        wic.CreateBitmapScaler(out var scaler);
        scaler.Initialize(premultiplied, (uint)size, (uint)size, WICBitmapInterpolationMode.WICBitmapInterpolationModeHighQualityCubic);
        var pixels = new byte[size * size * 4];
        scaler.CopyPixels(null, (uint)size * 4, (uint)pixels.Length, pixels);
        GC.KeepAlive(png);
        return pixels;
    }

    // BT.601 with limited range, which webcams use. Each U and V sample covers a 2x2 block.
    private static byte[] Nv12(byte[] bgra, int width, int height, int stride)
    {
        var frame = new byte[stride * height * 3 / 2];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                frame[(y * stride) + x] = Luma(bgra, ((y * width) + x) * 4);
            }
        }

        var chroma = stride * height;
        for (var y = 0; y < height; y += 2)
        {
            for (var x = 0; x < width; x += 2)
            {
                var (u, v) = Chroma(bgra, width, x, y, 2);
                var c = chroma + ((y / 2) * stride) + x;
                frame[c] = u;
                frame[c + 1] = v;
            }
        }

        return frame;
    }

    private static byte[] Yuy2(byte[] bgra, int width, int height, int stride)
    {
        var frame = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x += 2)
            {
                var (u, v) = Chroma(bgra, width, x, y, 1);
                var p = (y * stride) + (x * 2);
                frame[p] = Luma(bgra, ((y * width) + x) * 4);
                frame[p + 1] = u;
                frame[p + 2] = Luma(bgra, ((y * width) + x + 1) * 4);
                frame[p + 3] = v;
            }
        }

        return frame;
    }

    private static byte[] Rgb32(byte[] bgra, int width, int height, int stride)
    {
        var frame = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            Array.Copy(bgra, y * width * 4, frame, y * stride, width * 4);
        }

        return frame;
    }

    private static byte Luma(byte[] bgra, int p) =>
        (byte)((((66 * bgra[p + 2]) + (129 * bgra[p + 1]) + (25 * bgra[p]) + 128) >> 8) + 16);

    // The average U and V of two pixels across and rows pixels down.
    private static (byte U, byte V) Chroma(byte[] bgra, int width, int x, int y, int rows)
    {
        int r = 0, g = 0, b = 0;
        for (var dy = 0; dy < rows; dy++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                var p = (((y + dy) * width) + x + dx) * 4;
                b += bgra[p];
                g += bgra[p + 1];
                r += bgra[p + 2];
            }
        }

        var n = rows * 2;
        r /= n;
        g /= n;
        b /= n;
        return ((byte)((((-38 * r) - (74 * g) + (112 * b) + 128) >> 8) + 128), (byte)((((112 * r) - (94 * g) - (18 * b) + 128) >> 8) + 128));
    }

    private static byte[] Jpeg(byte[] bgra, int width, int height)
    {
        var wic = Factory();
        var bgraFormat = PInvoke.GUID_WICPixelFormat32bppBGRA;
        wic.CreateBitmapFromMemory((uint)width, (uint)height, &bgraFormat, (uint)width * 4, (uint)bgra.Length, bgra, out var bitmap);
        wic.CreateFormatConverter(out var bgr);
        var bgrFormat = PInvoke.GUID_WICPixelFormat24bppBGR;
        bgr.Initialize(bitmap, &bgrFormat, WICBitmapDitherType.WICBitmapDitherTypeNone, null!, 0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);

        // Pinned for good: WIC writes through this memory until the encoder commits. A flat frame compresses far
        // below its raw size.
        var output = GC.AllocateUninitializedArray<byte>((width * height * 4) + 65536, pinned: true);
        wic.CreateStream(out var stream);
        stream.InitializeFromMemory(output, (uint)output.Length);
        var container = PInvoke.GUID_ContainerFormatJpeg;
        var encoder = wic.CreateEncoder(&container, null);
        encoder.Initialize(stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);
        IPropertyBag2 options = null!;
        encoder.CreateNewFrame(out var frame, ref options);
        frame.Initialize(options);
        frame.SetSize((uint)width, (uint)height);
        frame.SetPixelFormat(&bgrFormat);
        frame.WriteSource(bgr, null);
        frame.Commit();
        encoder.Commit();
        ulong length;
        stream.Seek(0, SeekOrigin.Current, &length);
        return output[..(int)length];
    }

    private static IWICImagingFactory Factory()
    {
        PInvoke.CoCreateInstance(PInvoke.CLSID_WICImagingFactory, null, CLSCTX.CLSCTX_INPROC_SERVER, out IWICImagingFactory factory).ThrowOnFailure();
        return factory;
    }
}
