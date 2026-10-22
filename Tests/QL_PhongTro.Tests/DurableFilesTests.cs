using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using QL_PhongTro.Services;
using System.Net;
using System.Security.Cryptography;
using System.Xml.Linq;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class DurableFilesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nhatot-storage-" + Guid.NewGuid().ToString("N"));
    private sealed class FakeS3 : AmazonS3Client
    {
        public readonly Dictionary<string, byte[]> Objects = [];
        public bool FailWrite;
        public FakeS3() : base(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "https://storage.example.test", ForcePathStyle = true }) { }
        public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken ct = default)
        {
            if (FailWrite) throw new AmazonS3Exception("Secret provider detail");
            using var memory = new MemoryStream(); request.InputStream.CopyTo(memory); Objects[request.Key] = memory.ToArray();
            return Task.FromResult(new PutObjectResponse());
        }
        public override Task<GetObjectResponse> GetObjectAsync(string bucket, string key, CancellationToken ct = default)
        {
            if (!Objects.TryGetValue(key, out var bytes)) throw new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound };
            return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(bytes), ContentLength = bytes.Length });
        }
        public override Task<DeleteObjectResponse> DeleteObjectAsync(string bucket, string key, CancellationToken ct = default)
        { Objects.Remove(key); return Task.FromResult(new DeleteObjectResponse()); }
        public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken ct = default)
            => Task.FromResult(new ListObjectsV2Response { IsTruncated = false, S3Objects = Objects.Keys.Where(k => k.StartsWith(request.Prefix)).Select(k => new S3Object { Key = k }).ToList() });
    }
    [Theory]
    [InlineData("../secret")]
    [InlineData("/secret")]
    [InlineData("a\\secret")]
    [InlineData("a//secret")]
    public async Task CacheRejectsTraversal(string key)
    {
        using var files = new DurableFiles(new FakeS3(), "private");
        await Assert.ThrowsAsync<InvalidDataException>(() => files.Scope(root, "identity").WriteAsync(key, [1]));
    }
    [Fact]
    public async Task NewCacheRestoresPrivateBytesAndDeletionDoesNotReturnStaleCache()
    {
        using var files = new DurableFiles(new FakeS3(), "private");
        await files.Scope(root + "/a", "avatars").WriteAsync("1.png", [1,2,3]);
        var restarted = files.Scope(root + "/b", "avatars");
        Assert.Equal(new byte[] {1,2,3}, File.ReadAllBytes(restarted.Ensure("1.png")));
        files.Scope(root + "/a", "avatars").Delete("1.png");
        Assert.False(File.Exists(restarted.Ensure("1.png", refresh: true)));
    }
    [Fact]
    public async Task FailedRemoteWriteDoesNotReplaceLocalBytesOrLeakProviderDetail()
    {
        using var client = new FakeS3(); using var files = new DurableFiles(client, "private");
        var cache = files.Scope(root, "identity");
        await cache.WriteAsync("image", [1]); client.FailWrite = true;
        var error = await Assert.ThrowsAsync<IOException>(() => cache.WriteAsync("image", [2]));
        Assert.DoesNotContain("Secret", error.ToString());
        Assert.Equal(new byte[] {1}, File.ReadAllBytes(cache.Ensure("image")));
    }
    [Fact]
    public void KeyRingIsEncryptedAndRequiresTheSameMasterSecret()
    {
        using var client = new FakeS3(); using var files = new DurableFiles(client, "private");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ObjectStorage:KeyEncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }).Build();
        var repository = new DurableKeyRepository(files, config);
        repository.StoreElement(XElement.Parse("<key id='test'><secret>key-material</secret></key>"), "key-test");
        Assert.DoesNotContain("key-material", System.Text.Encoding.UTF8.GetString(Assert.Single(client.Objects.Values)));
        Assert.Equal("key-material", Assert.Single(new DurableKeyRepository(files, config).GetAllElements()).Element("secret")!.Value);
        var blob = Assert.Single(client.Objects.Values); blob[^1] ^= 1;
        Assert.Throws<AuthenticationTagMismatchException>(() => repository.GetAllElements());
    }
    [Fact]
    public async Task UncommittedContractEditRestoresRemoteManifestAcrossRestart()
    {
        using var files = new DurableFiles(new FakeS3(), "private");
        var store = new ContractDocumentStore(root + "/first/db", files);
        var bytes = "%PDF-1.7 synthetic"u8.ToArray();
        IFormFile upload = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", "contract.pdf");
        await store.WriteAsync(1, "old terms", [upload]);
        using (await store.PrepareWriteAsync(1, "uncommitted terms", [], true)) { }
        var restarted = new ContractDocumentStore(root + "/second/db", files);
        var read = await restarted.ReadAsync(1);
        Assert.Equal("old terms", read.Terms);
        Assert.Equal(bytes, File.ReadAllBytes(restarted.FilePath(1, Assert.Single(read.Files).Id)));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
