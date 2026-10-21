using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
namespace QL_PhongTro.Services;

// Private files, isolated per database; only the current account's endpoint serves them.
public sealed class PersonalAvatarStore(AppDbContext db, GiayToImageStore validator)
{
    private string Root => Path.GetFullPath(db.Database.GetDbConnection().DataSource)+".avatars";
    public string FilePath(int actor) => Path.Combine(Root,actor+".png");
    public async Task SaveAsync(int actor,IFormFile file)
    {
        var prepared=await validator.PrepareAsync(file) ?? throw new InvalidDataException();
        using var image=Image.Load(prepared.Bytes);
        image.Mutate(x=>x.Resize(new ResizeOptions{Size=new Size(512,512),Mode=ResizeMode.Crop}));
        Directory.CreateDirectory(Root);
        var temporary=Path.Combine(Root,Guid.NewGuid().ToString("N")+".tmp");
        try { await image.SaveAsPngAsync(temporary);File.Move(temporary,FilePath(actor),true); }
        finally { if(File.Exists(temporary))File.Delete(temporary); }
    }
    public void Delete(int actor)=>File.Delete(FilePath(actor));
}
