#:package Svg.Skia@5.2.3
#:property ManagePackageVersionsCentrally=false

// Renders docs/brand/app-icon.svg into the multi-size .ico used by the app and Setup, plus a
// 256 px PNG for the README, and the tray's state icons: the same icon with a dot in its corner.
// Run from the repo root: dotnet run scripts/make-icons.cs
using SkiaSharp;
using Svg.Skia;

var brand = Path.Combine("docs", "brand");
using var svg = new SKSvg();
svg.Load(Path.Combine(brand, "app-icon.svg"));
var picture = svg.Picture ?? throw new InvalidOperationException("app-icon.svg did not render");

int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
var pngs = sizes.Select(size => Render(picture, size, dot: null)).ToList();
File.WriteAllBytes(Path.Combine(brand, "app-icon-256.png"), pngs[^1]);
WriteIco(Path.Combine(brand, "shybun.ico"), sizes, pngs);

// Blush for muted, ink when something's off. The paper ring keeps either one visible on a dark taskbar.
WriteIco(Path.Combine(brand, "shybun-muted.ico"), sizes, [.. sizes.Select(size => Render(picture, size, SKColor.Parse("#c4567a")))]);
WriteIco(Path.Combine(brand, "shybun-alert.ico"), sizes, [.. sizes.Select(size => Render(picture, size, SKColor.Parse("#1d1a16")))]);

Console.WriteLine($"Wrote shybun.ico, shybun-muted.ico and shybun-alert.ico ({string.Join(", ", sizes)}) and app-icon-256.png");

static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
{
    using var ico = File.Create(path);
    using var writer = new BinaryWriter(ico);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        // A 256 px entry is stored as 0 in the one-byte width and height fields.
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(pngs[i].Length);
        writer.Write(offset);
        offset += pngs[i].Length;
    }
    foreach (var png in pngs)
    {
        writer.Write(png);
    }
}

static byte[] Render(SKPicture picture, int size, SKColor? dot)
{
    using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    var scale = size / picture.CullRect.Width;
    canvas.Scale(scale);
    canvas.DrawPicture(picture);
    if (dot is { } color)
    {
        // In the icon's own 64-unit space, over the bottom-right corner of the tile.
        using var ring = new SKPaint { Color = SKColor.Parse("#f2ede2"), IsAntialias = true };
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawCircle(50, 50, 14, ring);
        canvas.DrawCircle(50, 50, 10.5f, fill);
    }
    canvas.Flush();
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}
