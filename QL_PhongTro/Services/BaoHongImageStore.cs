using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
namespace QL_PhongTro.Services;

public sealed record PreparedBaoHongImage(byte[] Bytes, string ContentType, string Hash);

// Private, database-scoped storage; never expose this directory as static files.
public sealed class BaoHongImageStore(string root)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxImages = 3;
    private readonly string directory = Path.GetFullPath(root);
    public async Task<List<PreparedBaoHongImage>> PrepareAsync(IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        if (files.Count > MaxImages) throw Error("Chỉ được đính kèm tối đa 3 ảnh.");
        var result = new List<PreparedBaoHongImage>();
        foreach (var file in files)
        {
            if (file.Length <= 0 || file.Length > MaxBytes) throw Error("Mỗi ảnh phải có nội dung và không vượt 5 MiB (5.242.880 byte).");
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png")) throw Error("Chỉ nhận ảnh JPG/JPEG hoặc PNG.");
            using var bytes = new MemoryStream();
            await using var source = file.OpenReadStream();
            var buffer = new byte[81920]; int read;
            while ((read = await source.ReadAsync(buffer, ct)) != 0)
            {
                if (bytes.Length + read > MaxBytes) throw Error("Ảnh vượt 5 MiB (5.242.880 byte).");
                bytes.Write(buffer, 0, read);
            }
            if (bytes.Length == 0) throw Error("Tệp ảnh rỗng.");
            bytes.Position = 0;
            try
            {
                var info = await Image.IdentifyAsync(bytes, ct);
                var format = info.Metadata.DecodedImageFormat?.Name;
                if (format is not ("JPEG" or "PNG") || (format == "PNG") != (extension == ".png")) throw Error("Nội dung ảnh không khớp định dạng JPG/PNG.");
                if ((long)info.Width * info.Height > 50_000_000) throw Error("Ảnh vượt giới hạn an toàn 50 megapixel. Hãy giảm độ phân giải.");
                bytes.Position = 0;
                using var decoded = await Image.LoadAsync(new DecoderOptions { MaxFrames = 1 }, bytes, ct);
                var data = bytes.ToArray();
                result.Add(new(data, format == "PNG" ? "image/png" : "image/jpeg", Convert.ToHexString(SHA256.HashData(data))));
            }
            catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
            { throw Error("Tệp bị hỏng hoặc không phải ảnh JPG/PNG hợp lệ."); }
        }
        return result;
    }
    public string FilePath(string key)
    {
        if (!Guid.TryParseExact(key, "N", out _)) throw new InvalidDataException("Invalid image key.");
        return Path.Combine(directory, key);
    }
    public async Task SaveAsync(string key, byte[] bytes, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        await using var stream = new FileStream(FilePath(key), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }
    public void Delete(string key) { try { File.Delete(FilePath(key)); } catch (DirectoryNotFoundException) { } }
    private static BaoHongValidationException Error(string message) => new("Images", message);
}
