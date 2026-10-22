using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using SkiaSharp;
namespace QL_PhongTro.Services;

// Private files, isolated per database; only the current account's endpoint serves them.
public sealed class PersonalAvatarStore(AppDbContext db, GiayToImageStore validator, DurableFiles? durable = null)
{
    private string Root => Path.GetFullPath(db.FileStoragePath)+".avatars";
    private FileScope? Remote => durable is { Enabled: true } ? durable.Scope(Root, "avatars") : null;
    public string FilePath(int actor) => Remote?.Ensure(actor+".png", refresh: true) ?? Path.Combine(Root,actor+".png");
    public async Task SaveAsync(int actor,IFormFile file)
    {
        var prepared=await validator.PrepareAsync(file) ?? throw new InvalidDataException();
        using var source=RasterImage.Decode(prepared.Bytes,prepared.Extension,out _);
        using var image=RasterImage.Resize(source,512,512,crop:true);
        if (Remote is {} remote) { await remote.WriteAsync(actor+".png", RasterImage.Encode(image,SKEncodedImageFormat.Png)); return; }
        Directory.CreateDirectory(Root);
        var temporary=Path.Combine(Root,Guid.NewGuid().ToString("N")+".tmp");
        try { await File.WriteAllBytesAsync(temporary,RasterImage.Encode(image,SKEncodedImageFormat.Png));File.Move(temporary,FilePath(actor),true); }
        finally { if(File.Exists(temporary))File.Delete(temporary); }
    }
    public void Delete(int actor) { if (Remote is {} remote) remote.Delete(actor+".png"); else File.Delete(FilePath(actor)); }
}
