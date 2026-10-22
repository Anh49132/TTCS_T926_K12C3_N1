using SkiaSharp;

namespace QL_PhongTro.Services;

// Decode only the first frame. Check dimensions before allocating native pixel memory,
// and require a complete decode: Skia can otherwise return partial, corrupt images.
internal static class RasterImage
{
    public static async Task<byte[]> ReadAsync(IFormFile file, long maxBytes, CancellationToken ct = default)
    {
        await using var source = file.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(buffer, ct)) != 0)
        {
            if (output.Length + count > maxBytes)
                throw new InvalidDataException("Ảnh vượt quá giới hạn 5 MB (5.242.880 byte).");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    public static SKBitmap Decode(byte[] bytes, string extension, out SKEncodedImageFormat format,
        long maxPixels = 50_000_000, int maxDimension = int.MaxValue, bool autoOrient = true)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) throw InvalidImage();
        format = codec.EncodedFormat;
        if (format is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png)
            || (format == SKEncodedImageFormat.Png) != (extension == ".png"))
            throw new InvalidDataException("Nội dung tệp không khớp ảnh JPG hoặc PNG hợp lệ.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0) throw InvalidImage();
        if ((long)info.Width * info.Height > maxPixels || info.Width > maxDimension || info.Height > maxDimension)
            throw new InvalidDataException($"Ảnh có độ phân giải quá lớn (tối đa {maxPixels / 1_000_000} megapixel).");

        var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        try
        {
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
                throw InvalidImage();
            if (!autoOrient || codec.EncodedOrigin == SKEncodedOrigin.TopLeft) return bitmap;
            var oriented = Orient(bitmap, codec.EncodedOrigin);
            bitmap.Dispose();
            return oriented;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 85)
    {
        // Encoding a fresh pixel image drops EXIF/XMP/IPTC and any embedded location.
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, quality);
        if (encoded is null) throw new InvalidDataException("Không thể xử lý ảnh JPG/PNG.");
        return encoded.ToArray();
    }

    public static SKBitmap Resize(SKBitmap source, int width, int height, bool crop = false)
    {
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(source);
        var sourceRect = new SKRect(0, 0, source.Width, source.Height);
        if (crop)
        {
            var side = Math.Min(source.Width, source.Height);
            var left = (source.Width - side) / 2f;
            var top = (source.Height - side) / 2f;
            sourceRect = new SKRect(left, top, left + side, top + side);
        }
        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(image, sourceRect, new SKRect(0, 0, width, height),
            new SKSamplingOptions(SKCubicResampler.Mitchell));
        return result;
    }

    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        var w = source.Width;
        var h = source.Height;
        var swapped = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(swapped ? h : w, swapped ? w : h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(source);
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        canvas.SetMatrix(matrix);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return result;
    }

    private static InvalidDataException InvalidImage() => new("Tệp ảnh bị hỏng hoặc không phải ảnh JPG/PNG hợp lệ.");
}
