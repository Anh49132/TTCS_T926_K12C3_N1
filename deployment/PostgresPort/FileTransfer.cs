using Microsoft.Extensions.Configuration;
using QL_PhongTro.Services;
using System.Security.Cryptography;
using System.Text.Json;

internal static class FileTransfer
{
    private sealed record Entry(string Source,string Key,string Sha256,long Bytes);
    public static async Task RunAsync(string configPath,string manifestPath)
    {
        var config=new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(configPath)).Build();
        if(config["ObjectStorage:Endpoint"]!="https://br-bitter-voice-b360ggd5.storage.c-4.ap-southeast-1.aws.neon.tech" || config["ObjectStorage:Bucket"]!="nhatot-private") throw new InvalidOperationException("Unexpected target.");
        var entries=JsonSerializer.Deserialize<Entry[]>(await File.ReadAllTextAsync(manifestPath))!;
        if(entries.Length!=2889 || entries.Select(x=>x.Key).Distinct().Count()!=entries.Length || entries.Sum(x=>x.Bytes)>300*1024*1024) throw new InvalidOperationException("Storage inventory changed.");
        using var files=new DurableFiles(config); var complete=0;
        var workspace=Path.GetFullPath(Directory.GetCurrentDirectory())+Path.DirectorySeparatorChar;
        await Parallel.ForEachAsync(entries,new ParallelOptions{MaxDegreeOfParallelism=6},async(entry,ct)=>
        {
            if(!Path.GetFullPath(entry.Source).StartsWith(workspace,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected source root.");
            var bytes=await File.ReadAllBytesAsync(entry.Source,ct);
            if(bytes.LongLength!=entry.Bytes || Convert.ToHexString(SHA256.HashData(bytes))!=entry.Sha256)throw new InvalidOperationException("Source file changed.");
            var existing=await files.ReadAsync(entry.Key,ct);
            if(existing is null) await files.WriteAsync(entry.Key,bytes,ct);
            else if(Convert.ToHexString(SHA256.HashData(existing))!=entry.Sha256) throw new InvalidOperationException("Existing object differs; will not overwrite.");
            var restored=await files.ReadAsync(entry.Key,ct)??throw new IOException("Missing uploaded object.");
            if(Convert.ToHexString(SHA256.HashData(restored))!=entry.Sha256)throw new IOException("Uploaded file mismatch.");
            var count=Interlocked.Increment(ref complete);
            if(count%250==0)Console.WriteLine(JsonSerializer.Serialize(new{verifiedFiles=count,total=entries.Length}));
        });
        Console.WriteLine(JsonSerializer.Serialize(new{verifiedFiles=complete,totalBytes=entries.Sum(x=>x.Bytes),allHashesMatched=true,privateBucket=true,unreferencedFilesCopied=false}));
    }
}
