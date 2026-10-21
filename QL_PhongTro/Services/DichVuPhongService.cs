using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed class DichVuPhongService(AppDbContext db, DichVuService prices, ITimeProvider? time = null)
{
    private DateTime UtcNow => time?.UtcNow ?? DateTime.UtcNow;
    private DateOnly HomNay => DateOnly.FromDateTime(UtcNow.AddHours(7));
    // PO confirmed: charge the whole current Vietnam calendar month; stop next month.
    public static DateOnly KyNgung(DateTime utc) => DauKy(DateOnly.FromDateTime(utc.AddHours(7))).AddMonths(1);
    private static DateOnly DauKy(DateOnly date) => new(date.Year, date.Month, 1);

    // TODO(PO S2-01): Confirm snapshot-at-creation policy. Changing defaults never
    // backfills existing rooms. Keep this decision here for single and bulk creation.
    // Caller saves the room graph and audit in one transaction; no price is copied.
    public async Task GanMacDinhChoPhongMoiAsync(IReadOnlyCollection<PhongTro> rooms)
    {
        if (rooms.Any(x => db.Entry(x).State != EntityState.Added))
            throw new InvalidOperationException("Chỉ gán mặc định khi tạo phòng mới.");
        var buildingIds = rooms.Select(x => x.ToaNhaId).Distinct().ToArray();
        var defaults = await db.DichVuToaNhas.Where(x => buildingIds.Contains(x.ToaNhaId) && x.ApDungMacDinh).ToListAsync();
        foreach (var room in rooms)
            foreach (var service in defaults.Where(x => x.ToaNhaId == room.ToaNhaId))
                db.DichVuPhongs.Add(new DichVuPhong { Phong = room, DichVuToaNhaId = service.Id });
    }

    public async Task DatMacDinhAsync(int accountId, int buildingId, int serviceId, bool enabled)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!await prices.SoHuuToaNhaAsync(accountId, buildingId)) throw new UnauthorizedAccessException();
        var catalog = await db.DichVuToaNhas.SingleOrDefaultAsync(x => x.ToaNhaId == buildingId && x.DichVuId == serviceId)
            ?? throw new InvalidOperationException("Không tìm thấy dịch vụ trong tòa nhà.");
        catalog.ApDungMacDinh = enabled;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<PhongTro> PhongAsync(int accountId, int roomId)
    {
        var room = await db.PhongTros.AsNoTracking().SingleOrDefaultAsync(x => x.Id == roomId);
        if (room is null || !await prices.SoHuuToaNhaAsync(accountId, room.ToaNhaId)) throw new UnauthorizedAccessException();
        return room;
    }

    public async Task DatDichVuAsync(int accountId, int roomId, int catalogId, bool enabled, long? donGiaRieng = null)
    {
        if (donGiaRieng < 0) throw new ArgumentOutOfRangeException(nameof(donGiaRieng));
        await using var tx = await db.Database.BeginTransactionAsync();
        var room = await PhongAsync(accountId, roomId);
        if (!await db.DichVuToaNhas.AnyAsync(x => x.Id == catalogId && x.ToaNhaId == room.ToaNhaId))
            throw new UnauthorizedAccessException();
        var existing = await db.DichVuPhongs.SingleOrDefaultAsync(x => x.PhongId == roomId && x.DichVuToaNhaId == catalogId);
        if (enabled && existing is null)
            db.DichVuPhongs.Add(new DichVuPhong { PhongId = roomId, DichVuToaNhaId = catalogId, DonGiaRieng = donGiaRieng });
        else if (enabled && existing is not null && donGiaRieng.HasValue)
            existing.DonGiaRieng = donGiaRieng;
        if (existing is not null)
        {
            var removal = await db.NgungDichVuPhongs.SingleOrDefaultAsync(x => x.DichVuPhongId == existing.Id && x.ApDungLaiTuKy == null);
            if (!enabled && removal is null)
                db.NgungDichVuPhongs.Add(new NgungDichVuPhong
                { DichVuPhongId = existing.Id, YeuCauLucUtc = UtcNow, NgungTuKy = KyNgung(UtcNow) });
            else if (enabled && removal is not null)
            {
                // Cancelling a pending stop leaves an empty interval. Re-enabling later
                // closes it at the current month, preserving every previously stopped month.
                removal.ApDungLaiTuKy = DauKy(HomNay) < removal.NgungTuKy ? removal.NgungTuKy : DauKy(HomNay);
                removal.ApDungLaiLucUtc = UtcNow;
            }
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DatDonGiaRiengAsync(int accountId, int roomId, int catalogId, long? donGiaRieng)
    {
        if (donGiaRieng < 0) throw new ArgumentOutOfRangeException(nameof(donGiaRieng));
        await using var tx = await db.Database.BeginTransactionAsync();
        var room = await PhongAsync(accountId, roomId);
        if (!await db.DichVuToaNhas.AnyAsync(x => x.Id == catalogId && x.ToaNhaId == room.ToaNhaId))
            throw new UnauthorizedAccessException();
        var selected = await db.DichVuPhongs.SingleOrDefaultAsync(x => x.PhongId == roomId && x.DichVuToaNhaId == catalogId)
            ?? throw new InvalidOperationException("Dịch vụ không được gán cho phòng này.");
        selected.DonGiaRieng = donGiaRieng;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<DichVuPhongViewModel> XemAsync(int accountId, int roomId)
    {
        var room = await PhongAsync(accountId, roomId);
        var catalog = await db.DichVuToaNhas.AsNoTracking().Include(x => x.DichVu)
            .Where(x => x.ToaNhaId == room.ToaNhaId).OrderBy(x => x.DichVu.TenDichVu).ToListAsync();
        var selected = await db.DichVuPhongs.AsNoTracking().Where(x => x.PhongId == roomId)
            .ToDictionaryAsync(x => x.DichVuToaNhaId, x => x.DonGiaRieng);
        var removals = await db.NgungDichVuPhongs.AsNoTracking().Include(x => x.DichVuPhong).ThenInclude(x => x.DichVuToaNha).ThenInclude(x => x.DichVu)
            .Where(x => x.DichVuPhong.PhongId == roomId).OrderByDescending(x => x.Id).ToListAsync();
        var model = new DichVuPhongViewModel { PhongId = room.Id, MaPhong = room.MaPhong, ToaNhaId = room.ToaNhaId };
        model.LichSuNgung = removals;
        foreach (var item in catalog)
        {
            var price = await prices.LayDonGiaAsync(accountId, room.ToaNhaId, item.DichVuId, HomNay);
            var isSelected = selected.TryGetValue(item.Id, out var roomPrice);
            var pending = removals.SingleOrDefault(x => x.DichVuPhong.DichVuToaNhaId == item.Id && x.ApDungLaiTuKy == null);
            isSelected &= pending is null || pending.NgungTuKy > HomNay;
            model.DichVus.Add(new(item.Id, item.DichVu.TenDichVu, isSelected, price, isSelected ? roomPrice : null, pending?.NgungTuKy));
        }
        return model;
    }

    // Both the invoice form and publication use this period-based rule. Never use
    // today's room selection to reconstruct an issued invoice's immutable lines.
    public async Task<DonGiaDichVu?> LayGiaHoaDonAsync(int accountId, int roomId, int serviceId, DateOnly date)
    {
        var room = await PhongAsync(accountId, roomId);
        var month = DauKy(date);
        var selection = await db.DichVuPhongs.AsNoTracking().SingleOrDefaultAsync(x => x.PhongId == roomId && x.DichVuToaNha.DichVuId == serviceId);
        if (selection is null) return null;
        if (selection is not null && await db.NgungDichVuPhongs.AnyAsync(x => x.DichVuPhongId == selection.Id && x.NgungTuKy <= month &&
            (x.ApDungLaiTuKy == null || x.ApDungLaiTuKy > month))) return null;
        var roomPrice = await db.CauHinhDichVus.AsNoTracking().Include(x => x.DichVu)
            .SingleOrDefaultAsync(x => x.PhongId == roomId && x.DichVuId == serviceId && x.TuNgay <= date
                && (x.DenNgay == null || x.DenNgay >= date));
        if (roomPrice is not null)
            return roomPrice.DangApDung && roomPrice.DaChotGia && roomPrice.DichVu.DangHoatDong
                ? new(roomPrice.Id, roomPrice.DichVuId, roomPrice.DichVu.TenDichVu, roomPrice.CachTinh, roomPrice.DonViTinh, roomPrice.DonGia)
                : null;
        var price = await prices.LayDonGiaAsync(accountId, room.ToaNhaId, serviceId, date);
        // Building prices apply only to services explicitly assigned to this room.
        return price is not null && selection?.DonGiaRieng is { } ownPrice ? price with { DonGia = ownPrice } : price;
    }
}
