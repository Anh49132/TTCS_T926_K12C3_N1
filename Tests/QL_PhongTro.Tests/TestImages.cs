using SkiaSharp;

namespace QL_PhongTro.Tests;

internal static class TestImages
{
    public static byte[] Encode(int width, int height, SKEncodedImageFormat format, SKColor? color = null)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color ?? SKColors.CornflowerBlue);
        using var data = bitmap.Encode(format, 90);
        return data.ToArray();
    }

    // A minimal independent EXIF APP1 segment; encoding with Skia does not write EXIF.
    public static byte[] WithOrientation(byte[] jpeg, ushort orientation)
    {
        using var result = new MemoryStream();
        result.Write(jpeg, 0, 2);
        result.Write([0xff, 0xe1, 0, 34]);
        result.Write("Exif\0\0"u8);
        using var writer = new BinaryWriter(result, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x4949);
        writer.Write((ushort)42);
        writer.Write(8u);
        writer.Write((ushort)1);
        writer.Write((ushort)0x112);
        writer.Write((ushort)3);
        writer.Write(1u);
        writer.Write(orientation);
        writer.Write((ushort)0);
        writer.Write(0u);
        result.Write(jpeg, 2, jpeg.Length - 2);
        return result.ToArray();
    }
}
