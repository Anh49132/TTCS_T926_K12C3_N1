using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

namespace QL_PhongTro.Services;

// Local directories are caches on a cloud host. A successful write includes the
// private object store, before the caller commits its database transaction.
public sealed class DurableFiles : IDisposable
{
    private readonly IAmazonS3? client;
    private readonly string bucket = "";
    public bool Enabled => client is not null;
    public DurableFiles(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ObjectStorage:Enabled")) return;
        var endpoint = new Uri(configuration["ObjectStorage:Endpoint"] ?? "");
        if (endpoint.Scheme != "https" || !endpoint.Host.EndsWith(".neon.tech", StringComparison.Ordinal)
            || endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0)
            throw new InvalidOperationException("Invalid private storage endpoint.");
        bucket = configuration["ObjectStorage:Bucket"] ?? throw new InvalidOperationException("Storage bucket missing.");
        var access = configuration["ObjectStorage:AccessKey"] ?? throw new InvalidOperationException("Storage credential missing.");
        var secret = configuration["ObjectStorage:SecretKey"] ?? throw new InvalidOperationException("Storage credential missing.");
        client = new AmazonS3Client(new BasicAWSCredentials(access, secret), new AmazonS3Config
        {
            ServiceURL = endpoint.AbsoluteUri, ForcePathStyle = true,
            AuthenticationRegion = configuration["ObjectStorage:Region"] ?? "ap-southeast-1",
            MaxErrorRetry = 0, Timeout = TimeSpan.FromSeconds(30)
        });
    }
    public DurableFiles(IAmazonS3 client, string bucket) { this.client = client; this.bucket = bucket; }
    public FileScope Scope(string root, string prefix) => new(this, root, prefix);
    public async Task<byte[]?> ReadAsync(string key, CancellationToken ct = default)
    {
        ValidateKey(key);
        if (client is null) throw new InvalidOperationException();
        try
        {
            using var response = await client.GetObjectAsync(bucket, key, ct);
            if (response.ContentLength > 12 * 1024 * 1024) throw new IOException("Tệp lưu trữ vượt giới hạn.");
            using var output = new MemoryStream();
            var buffer = new byte[81920]; int read;
            while ((read = await response.ResponseStream.ReadAsync(buffer, ct)) != 0)
            {
                if (output.Length + read > 12 * 1024 * 1024) throw new IOException("Tệp lưu trữ vượt giới hạn.");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException) { throw new IOException("Không đọc được kho tệp. Vui lòng thử lại."); }
    }
    public async Task WriteAsync(string key, byte[] bytes, CancellationToken ct = default)
    {
        ValidateKey(key);
        if (client is null) throw new InvalidOperationException();
        try
        {
            using var input = new MemoryStream(bytes, false);
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket, Key = key, InputStream = input,
                ContentType = "application/octet-stream", UseChunkEncoding = false,
                Headers = { CacheControl = "private, no-store" }
            }, ct);
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException) { throw new IOException("Không lưu được vào kho tệp. Vui lòng thử lại."); }
    }
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ValidateKey(key);
        if (client is null) throw new InvalidOperationException();
        try { await client.DeleteObjectAsync(bucket, key, ct); }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException) { throw new IOException("Không xóa được tệp lưu trữ. Vui lòng thử lại."); }
    }
    public async Task<List<string>> ListAsync(string prefix, CancellationToken ct = default)
    {
        ValidateKey(prefix.TrimEnd('/'));
        if (client is null) throw new InvalidOperationException();
        try
        {
            var keys = new List<string>(); string? token = null;
            do
            {
                var page = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, Prefix = prefix, ContinuationToken = token }, ct);
                keys.AddRange((page.S3Objects ?? []).Select(o => o.Key));
                token = page.IsTruncated == true ? page.NextContinuationToken : null;
            } while (token is not null);
            return keys;
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException) { throw new IOException("Không đọc được danh sách tệp lưu trữ."); }
    }
    internal static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith('/') || key.Contains('\\') || key.Contains(':')
            || key.Split('/').Any(s => s is "" or "." or "..") || key.Any(char.IsControl))
            throw new InvalidDataException("Invalid storage key.");
    }
    public void Dispose() => client?.Dispose();
}

public sealed class FileScope(DurableFiles files, string root, string prefix)
{
    private readonly string root = Path.GetFullPath(root);
    public bool Enabled => files.Enabled;
    private string Key(string relative) { DurableFiles.ValidateKey(relative); return prefix + "/" + relative; }
    private string Local(string relative)
    {
        Key(relative);
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException();
        return path;
    }
    public string Ensure(string relative, bool refresh = false)
    {
        var path = Local(relative);
        if (files.Enabled && (refresh || !File.Exists(path)))
        {
            var bytes = files.ReadAsync(Key(relative)).GetAwaiter().GetResult();
            if (bytes is null) { if (File.Exists(path)) File.Delete(path); return path; }
            AtomicWrite(path, bytes);
        }
        return path;
    }
    public async Task WriteAsync(string relative, byte[] bytes, CancellationToken ct = default)
    {
        var path = Local(relative);
        if (files.Enabled) await files.WriteAsync(Key(relative), bytes, ct);
        AtomicWrite(path, bytes);
    }
    public void Delete(string relative)
    {
        var path = Local(relative);
        if (files.Enabled) files.DeleteAsync(Key(relative)).GetAwaiter().GetResult();
        if (File.Exists(path)) File.Delete(path);
    }
    public IReadOnlyList<string> Enumerate()
        => files.Enabled ? files.ListAsync(prefix + "/").GetAwaiter().GetResult().Select(k => Local(k[(prefix.Length + 1)..])).ToList()
            : Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : [];
    private static void AtomicWrite(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
