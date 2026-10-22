using SkiaSharp;

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
    private readonly FileScope? remote;

    public RoomImageStore(IWebHostEnvironment environment, IConfiguration configuration, DurableFiles? durable = null)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        root = Path.GetFullPath(configuration["RoomImagesPath"]
            ?? Path.Combine(webRoot, "uploads", "rooms"));
        remote = durable is { Enabled: true } ? durable.Scope(root, "rooms") : null;
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

        var originalBytes = await RasterImage.ReadAsync(file, MaxBytes, cancellationToken);
        using var image = RasterImage.Decode(originalBytes, extension, out var format, MaxPixels);
        cancellationToken.ThrowIfCancellationRequested();
        var width = Math.Min(image.Width, ThumbnailWidth);
        var height = Math.Max(1, (int)Math.Round((decimal)image.Height * width / image.Width));
        using var thumbnail = RasterImage.Resize(image, width, height);
        return new PreparedRoomImage(originalBytes, RasterImage.Encode(thumbnail, format, 82),
            format == SKEncodedImageFormat.Jpeg ? ".jpg" : ".png");
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
            if (remote is not null)
            {
                await remote.WriteAsync($"{roomId}/{originalName}", image.OriginalBytes, cancellationToken);
                await remote.WriteAsync($"{roomId}/{thumbnailName}", image.ThumbnailBytes, cancellationToken);
            }
            else
            {
                await File.WriteAllBytesAsync(originalFile, image.OriginalBytes, cancellationToken);
                await File.WriteAllBytesAsync(thumbnailFile, image.ThumbnailBytes, cancellationToken);
            }
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

    public string? FetchPublicImage(string publicPath)
    {
        if (!TryGetFullPath(publicPath, out var path)) return null;
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        var parts = relative.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var id) || id <= 0) return null;
        var extension = Path.GetExtension(parts[1]);
        var stem = Path.GetFileNameWithoutExtension(parts[1]);
        if (stem.EndsWith("-thumb", StringComparison.Ordinal)) stem = stem[..^6];
        if (extension is not (".jpg" or ".png") || !Guid.TryParseExact(stem, "N", out _)) return null;
        return remote?.Ensure(relative) ?? path;
    }

    public IReadOnlyList<string> EnumerateStoredFiles()
    {
        if (remote is not null) return remote.Enumerate();
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
        try { DeleteFileStrict(fullPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errors.Add($"{path}: {exception.Message}");
        }
    }

    private void DeleteFileStrict(string path)
    {
        if (remote is not null) remote.Delete(Path.GetRelativePath(root, path).Replace('\\', '/'));
        else File.Delete(path);
    }

    private void DeleteFile(string path)
    {
        try { DeleteFileStrict(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
