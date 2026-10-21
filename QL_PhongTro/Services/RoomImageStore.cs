using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace QL_PhongTro.Services;

public sealed record PreparedRoomImage(byte[] OriginalBytes, byte[] ThumbnailBytes, string Extension);
public sealed record StoredRoomImage(string OriginalPath, string ThumbnailPath);
public sealed record RoomImageDeleteResult(bool Success, string? ErrorMessage);

public sealed class RoomImageStore
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const int MaxImagesPerRoom = 8;
    public const int ThumbnailWidth = 400;
    private const long MaxPixels = 50_000_000;
    private readonly string root;

    public RoomImageStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        root = Path.GetFullPath(configuration["RoomImagesPath"]
            ?? Path.Combine(webRoot, "uploads", "rooms"));
    }

    public RoomImageStore(string webRootPath) => root = Path.GetFullPath(Path.Combine(webRootPath, "uploads", "rooms"));

    public async Task<PreparedRoomImage> PrepareAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            throw new InvalidDataException("Tệp ảnh rỗng. Vui lòng chọn lại ảnh.");
        if (file.Length > MaxBytes)
            throw new InvalidDataException("Ảnh vượt quá giới hạn 5 MB (5.242.880 byte).");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png"))
            throw new InvalidDataException("Chỉ chấp nhận ảnh JPG hoặc PNG.");

        await using var input = new MemoryStream();
        await file.CopyToAsync(input, cancellationToken);
        var originalBytes = input.ToArray();
        input.Position = 0;

        try
        {
            var info = await Image.IdentifyAsync(input, cancellationToken);
            var format = info.Metadata.DecodedImageFormat?.Name;
            if (format is not ("JPEG" or "PNG") || (format == "PNG") != (extension == ".png"))
                throw new InvalidDataException("Nội dung tệp không khớp ảnh JPG hoặc PNG hợp lệ.");
            if ((long)info.Width * info.Height > MaxPixels)
                throw new InvalidDataException("Ảnh có độ phân giải quá lớn (tối đa 50 megapixel).");

            input.Position = 0;
            using var image = await Image.LoadAsync(new DecoderOptions { MaxFrames = 1 }, input, cancellationToken);
            image.Mutate(context => context.AutoOrient());
            using var thumbnail = image.Clone(context =>
            {
                if (image.Width > ThumbnailWidth)
                    context.Resize(ThumbnailWidth, 0);
            });
            thumbnail.Metadata.ExifProfile = null;
            thumbnail.Metadata.XmpProfile = null;
            thumbnail.Metadata.IptcProfile = null;

            await using var output = new MemoryStream();
            if (format == "JPEG")
                await thumbnail.SaveAsync(output, new JpegEncoder { Quality = 82 }, cancellationToken);
            else
                await thumbnail.SaveAsync(output, new PngEncoder(), cancellationToken);

            return new PreparedRoomImage(originalBytes, output.ToArray(), format == "JPEG" ? ".jpg" : ".png");
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new InvalidDataException("Tệp ảnh bị hỏng hoặc không phải ảnh JPG/PNG hợp lệ.", exception);
        }
    }

    public async Task<StoredRoomImage> SaveAsync(int roomId, PreparedRoomImage image, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(root, roomId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid().ToString("N");
        var originalName = id + image.Extension;
        var thumbnailName = id + "-thumb" + image.Extension;
        var originalFile = Path.Combine(directory, originalName);
        var thumbnailFile = Path.Combine(directory, thumbnailName);

        try
        {
            await File.WriteAllBytesAsync(originalFile, image.OriginalBytes, cancellationToken);
            await File.WriteAllBytesAsync(thumbnailFile, image.ThumbnailBytes, cancellationToken);
            return new StoredRoomImage(
                $"/uploads/rooms/{roomId}/{originalName}",
                $"/uploads/rooms/{roomId}/{thumbnailName}");
        }
        catch
        {
            DeleteFile(originalFile);
            DeleteFile(thumbnailFile);
            throw;
        }
    }

    public void Delete(StoredRoomImage? image)
    {
        if (image is null) return;
        DeletePublicPath(image.OriginalPath);
        DeletePublicPath(image.ThumbnailPath);
    }

    public RoomImageDeleteResult DeletePermanent(StoredRoomImage? image)
    {
        if (image is null) return new(true, null);
        var errors = new List<string>();
        DeletePublicPathStrict(image.OriginalPath, errors);
        DeletePublicPathStrict(image.ThumbnailPath, errors);
        return errors.Count == 0
            ? new RoomImageDeleteResult(true, null)
            : new RoomImageDeleteResult(false, string.Join("; ", errors));
    }

    public bool TryGetFullPath(string? publicPath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(publicPath)) return false;
        var prefix = "/uploads/rooms/";
        if (!publicPath.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var relativePath = publicPath[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        fullPath = candidate;
        return true;
    }

    public IReadOnlyList<string> EnumerateStoredFiles()
    {
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void DeletePublicPath(string path)
    {
        if (!TryGetFullPath(path, out var fullPath)) return;
        DeleteFile(fullPath);
    }

    private void DeletePublicPathStrict(string? path, List<string> errors)
    {
        if (!TryGetFullPath(path, out var fullPath)) return;
        try { File.Delete(fullPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errors.Add($"{path}: {exception.Message}");
        }
    }

    private static void DeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
