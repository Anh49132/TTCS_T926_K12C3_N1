using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;

namespace QL_PhongTro.Services;

internal static class KyChiSoLockService
{
    public static async Task<HashSet<int>> LayToaNhaDaKhoaAsync(
        AppDbContext db, IReadOnlyCollection<int> buildingIds, int year, int month, CancellationToken ct)
    {
        if (buildingIds.Count == 0 || !await CoBangHoaDonAsync(db, ct))
            return [];

        return await (from invoice in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs.AsNoTracking() on invoice.HopDongId equals contract.Id
            join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
            where buildingIds.Contains(room.ToaNhaId)
                && invoice.Nam == year && invoice.Thang == month
                && (invoice.NgayPhatHanh.HasValue || invoice.NguoiPhatHanhId.HasValue)
            select room.ToaNhaId)
            .Distinct()
            .ToHashSetAsync(ct);
    }

    private static async Task<bool> CoBangHoaDonAsync(AppDbContext db, CancellationToken ct) =>
        await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='hoa_don'")
            .AnyAsync(count => count == 1, ct);
}
