using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed class CongNoService(AppDbContext db, ITimeProvider clock)
{
    public async Task<CongNoIndexViewModel> LayAsync(int actorId, bool admin, int? toaNhaId,
        int? quaHanTu, int? quaHanDen, long? tienTu, long? tienDen)
    {
        var actor = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.DangHoatDong && !x.IsDeleted);
        if (actor is null || actor.VaiTro is not ("CHU_NHA" or "ADMIN") || admin != (actor.VaiTro == "ADMIN"))
            throw new UnauthorizedAccessException();
        var visible = db.ToaNhas.AsNoTracking().Where(x => admin || x.ChuNhaId == actorId);
        if (toaNhaId.HasValue && !await visible.AnyAsync(x => x.Id == toaNhaId)) throw new UnauthorizedAccessException();
        var buildings = await visible.OrderBy(x => x.TenToaNha)
            .Select(x => new CongNoIndexViewModel.BuildingOption(x.Id, x.TenToaNha)).ToListAsync();

        // Keep the invoice/payment/offset queries in one read snapshot.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var invoices = await (from bill in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs.AsNoTracking() on bill.HopDongId equals contract.Id
            join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
            join building in visible on room.ToaNhaId equals building.Id
            join profile in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals profile.Id into profiles
            from profile in profiles.DefaultIfEmpty()
            where bill.TrangThai == "DA_PHAT_HANH" && (!toaNhaId.HasValue || building.Id == toaNhaId)
            select new { bill.Id, bill.TongTien, bill.HanThanhToan, PhongId = room.Id, room.MaPhong,
                TenToaNha = building.TenToaNha, Name = profile == null ? "Chưa có người đứng tên" : profile.HoTen })
            .ToListAsync();
        var ids = invoices.Select(x => x.Id).ToList();
        var paid = await db.ThanhToans.AsNoTracking().Where(x => ids.Contains(x.HoaDonId) && x.TrangThai == "DA_XAC_NHAN")
            .GroupBy(x => x.HoaDonId).Select(g => new { Id = g.Key, Total = g.Sum(x => x.SoTien) })
            .ToDictionaryAsync(x => x.Id, x => x.Total);
        var offsets = await CanTruHoaDonQueries.LayAsync(db, ids);
        await transaction.CommitAsync();
        var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        var rows = invoices.Select(x => new { Invoice = x,
                Remaining = Math.Max(0L, checked(x.TongTien - paid.GetValueOrDefault(x.Id) - offsets.GetValueOrDefault(x.Id))) })
            .Where(x => x.Remaining > 0)
            .GroupBy(x => new { x.Invoice.PhongId, x.Invoice.MaPhong, x.Invoice.TenToaNha })
            .Select(g => new CongNoRowViewModel(g.Key.PhongId, g.Key.MaPhong, g.Key.TenToaNha,
                string.Join(", ", g.Select(x => x.Invoice.Name).Distinct()), g.Count(), g.Sum(x => x.Remaining),
                g.Max(x => Math.Max(0, today.DayNumber - x.Invoice.HanThanhToan.DayNumber))))
            .Where(x => !quaHanTu.HasValue || x.SoNgayQuaHanDaiNhat >= Math.Max(0, quaHanTu.Value))
            .Where(x => !quaHanDen.HasValue || x.SoNgayQuaHanDaiNhat <= Math.Max(0, quaHanDen.Value))
            .Where(x => !tienTu.HasValue || x.TongConThieu >= Math.Max(0, tienTu.Value))
            .Where(x => !tienDen.HasValue || x.TongConThieu <= Math.Max(0, tienDen.Value))
            .OrderBy(x => x.TenToaNha).ThenBy(x => x.MaPhong).ToList();
        return new() { ToaNhaId = toaNhaId, QuaHanTu = quaHanTu, QuaHanDen = quaHanDen,
            TienTu = tienTu, TienDen = tienDen, Buildings = buildings, Rows = rows };
    }
}
