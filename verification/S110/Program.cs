using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;

var root = Path.GetFullPath(args[0]);
var source = Path.GetFullPath(args[1]);
var allowed = Path.Combine(root, "data", "S1-10-verification") + Path.DirectorySeparatorChar;
if (!source.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new Exception("Use an S1-10 disposable HTTP fixture only.");
var path = Path.Combine(allowed, "ef-" + Guid.NewGuid().ToString("N") + ".sqlite");
using (var src = new SqliteConnection($"Data Source={source};Mode=ReadOnly"))
using (var dst = new SqliteConnection($"Data Source={path}")) { src.Open(); dst.Open(); src.BackupDatabase(dst); }
var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options;
using var check = new AppDbContext(options);
var actor = await check.TaiKhoans.AsNoTracking().SingleAsync(x => x.Email == "audit-owner@example.test");
var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
    new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim(ClaimTypes.Name, "FORGED"), new Claim(ClaimTypes.Role, "ADMIN") }, "test")) } };
void Assert(bool condition, string name) { if (!condition) throw new Exception(name); }
foreach (var operation in new[] { "add", "update", "delete" })
{
    await using var db = new AppDbContext(options, accessor);
    var log = await db.NhatKyHoatDongs.FirstAsync();
    if (operation == "add") db.NhatKyHoatDongs.Add(new NhatKyHoatDong());
    else if (operation == "update") log.HanhDong = "FORGED";
    else db.NhatKyHoatDongs.Remove(log);
    try { await db.SaveChangesAsync(); throw new Exception("EF allowed audit " + operation); }
    catch (InvalidOperationException) { }
}
using (var db = new AppDbContext(options, accessor))
{
    var building = new ToaNha { ChuNhaId = actor.Id, TenToaNha = "EF sync", DiaChi = "Test", NgayChotHangThang = 1, DangHoatDong = true };
    db.ToaNhas.Add(building); db.SaveChanges();
    var log = await db.NhatKyHoatDongs.AsNoTracking().SingleAsync(x => x.LoaiDoiTuong == "toa_nha" && x.DoiTuongId == building.Id);
    Assert(log.TenNguoiThucHien == actor.HoTen && log.VaiTroLucThucHien == actor.VaiTro, "Do not trust role/name claims");
    var count = await db.NhatKyHoatDongs.CountAsync();
    building.TenToaNha = "EF sync"; await db.SaveChangesAsync();
    Assert(count == await db.NhatKyHoatDongs.CountAsync(), "No-op audit");
    db.ToaNhas.Remove(building); await db.SaveChangesAsync();
    var deleted = await db.NhatKyHoatDongs.OrderByDescending(x => x.Id).FirstAsync();
    Assert(deleted.HanhDong == "XOA" && deleted.DoiTuongId == building.Id && deleted.DuLieuSau == null && deleted.DuLieuTruoc!.Contains("EF sync"), "Deletion snapshot");
}
using (var db = new AppDbContext(options))
{
    db.ToaNhas.Add(new ToaNha { ChuNhaId = actor.Id, TenToaNha = "No actor", DiaChi = "Test" });
    try { await db.SaveChangesAsync(); throw new Exception("Allowed missing actor"); }
    catch (InvalidOperationException) { }
    Assert(!await check.ToaNhas.AnyAsync(x => x.TenToaNha == "No actor"), "Missing actor rollback");
}
// Even a caller that catches a failed save and commits its outer transaction cannot retain the business insert.
using (var db = new AppDbContext(options, accessor))
{
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER test_audit_failure BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test'); END");
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
        db.ToaNhas.Add(new ToaNha { ChuNhaId = actor.Id, TenToaNha = "Rollback savepoint", DiaChi = "Test", NgayChotHangThang = 1 });
        try { await db.SaveChangesAsync(); throw new Exception("Expected failure"); }
        catch (SqliteException) { }
        await tx.CommitAsync();
    }
    Assert(!await check.ToaNhas.AnyAsync(x => x.TenToaNha == "Rollback savepoint"), "Savepoint rollback");
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_audit_failure");
}
Console.WriteLine("PASS S1-10 EF: append-only guard; sync save; DB actor snapshot rejects forged claims; no-op; deletion; missing actor; savepoint rollback survives caller commit");
