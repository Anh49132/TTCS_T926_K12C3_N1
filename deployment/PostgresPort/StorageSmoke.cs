using Microsoft.Extensions.Configuration;
using QL_PhongTro.Services;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

internal static class StorageSmoke
{
    public static async Task RunAsync(string configPath)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(configPath)).Build();
        using var files = new DurableFiles(configuration);
        var name = Guid.NewGuid().ToString("N");
        var root = Path.GetFullPath(Path.Combine("data", "postgres-port", "storage-smoke-" + name));
        var original = RandomNumberGenerator.GetBytes(257);
        await files.Scope(root + "-first", "verification/" + name).WriteAsync("image", original);
        var restored = files.Scope(root + "-restart", "verification/" + name).Ensure("image");
        if (!File.ReadAllBytes(restored).SequenceEqual(original)) throw new InvalidOperationException("Storage round-trip mismatch.");
        if (await files.ReadAsync("verification/" + name + "/missing") is not null) throw new InvalidOperationException("Missing object handling failed.");
        Console.WriteLine(JsonSerializer.Serialize(new { storageRoundTrip = true, restartCacheRestored = true, missingHandled = true, syntheticBytes = original.Length }));
    }
}
