using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

// Read-only: live ownership applies even when the originating contract has ended.
public sealed class ChuNhaBaoHongService(AppDbContext db, BaoHongService tenantService)
{
    private async Task CheckActorAsync(int id, CancellationToken ct)
    {
        var active = await db.TaiKhoans.AsNoTracking().AnyAsync(x => x.Id == id && x.VaiTro == "CHU_NHA"
            && x.DangHoatDong && !x.IsDeleted && x.EmailConfirmed && !x.MustChangePassword, ct);
        var level = await db.RolePermissions.AsNoTracking().Where(x => x.RoleCode == "CHU_NHA" && x.ModuleCode == "BAO_HONG")
            .Select(x => x.AccessLevel).SingleOrDefaultAsync(ct);
        if (!active || level is not ("READ" or "WRITE" or "FULL")) throw new UnauthorizedAccessException();
    }

    private IQueryable<BaoHongChiTiet> Rows(int ownerId) =>
        from b in db.BaoHongs.AsNoTracking()
        join p in db.PhongTros on b.PhongId equals p.Id
        join t in db.ToaNhas on p.ToaNhaId equals t.Id where t.ChuNhaId == ownerId
        join h in db.HopDongs on b.HopDongId equals h.Id
        join a in db.TaiKhoans on b.NguoiBaoId equals a.Id
        select new BaoHongChiTiet { BaoHong = b, MaPhong = p.MaPhong, TenToaNha = t.TenToaNha,
            MaHopDong = h.MaHopDong, TenNguoiBao = a.HoTen, LaChuNha = true };

    public async Task<List<BaoHongChiTiet>> DanhSachAsync(int ownerId, int page, CancellationToken ct = default)
    {
        await CheckActorAsync(ownerId, ct);
        return await Rows(ownerId).OrderByDescending(x => x.BaoHong.Id)
            .Skip((Math.Clamp(page, 1, 100000) - 1) * 20).Take(21).ToListAsync(ct);
    }

    public async Task<BaoHongChiTiet?> ChiTietAsync(int ownerId, int id, CancellationToken ct = default)
    {
        await CheckActorAsync(ownerId, ct);
        var result = await Rows(ownerId).SingleOrDefaultAsync(x => x.BaoHong.Id == id, ct);
        if (result is null && await db.BaoHongs.AnyAsync(x => x.Id == id, ct)) throw new UnauthorizedAccessException();
        if (result is not null) await tenantService.LoadExtrasAsync(result, ct);
        return result;
    }
}
