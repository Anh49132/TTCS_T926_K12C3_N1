using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;
public sealed class LichSuNguoiOService(AppDbContext db)
{
    public async Task<(string Phong, string ToaNha)?> PhongAsync(int actor, int room, CancellationToken ct)
    {
        var context = await (from p in db.PhongTros.AsNoTracking() join b in db.ToaNhas on p.ToaNhaId equals b.Id
            join a in db.TaiKhoans on b.ChuNhaId equals a.Id
            where p.Id == room && a.Id == actor && a.VaiTro == "CHU_NHA" && a.DangHoatDong && !a.IsDeleted
            select new { p.MaPhong, b.TenToaNha }).SingleOrDefaultAsync(ct);
        return context == null ? null : (context.MaPhong, context.TenToaNha);
    }

    public async Task<(List<LichSuNguoiORow> Rows, int Missing)> LayAsync(int actor, int room,
        DateOnly from, DateOnly to, DateOnly today, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var transaction = await db.Database.UseTransactionAsync(sqlite, ct);
        if (await PhongAsync(actor, room, ct) == null) throw new UnauthorizedAccessException();
        if (to < from) throw new ArgumentException("Đến ngày không được trước từ ngày.");
        var contracts = await (from h in db.HopDongs.AsNoTracking()
            join k in db.KhachThues on h.KhachDungTenId equals k.Id into signers from k in signers.DefaultIfEmpty()
            where h.PhongId == room && (h.TrangThai == "CHO_HIEU_LUC" || h.TrangThai == "DANG_HIEU_LUC" || h.TrangThai == "DA_KET_THUC")
            select new { h.Id, h.MaHopDong, h.TrangThai, h.NgayTraPhong, Name = k == null ? null : k.HoTen }).ToListAsync(ct);
        var ids = contracts.Select(x => x.Id).ToList();
        var periods = await db.KyHopDongs.AsNoTracking().Where(k => ids.Contains(k.HopDongId)).OrderBy(k => k.NgayBatDau).ToListAsync(ct);
        var roommates = await (from g in db.NguoiOGheps.AsNoTracking() join k in db.KhachThues on g.KhachThueId equals k.Id
            where ids.Contains(g.HopDongId) select new { g.HopDongId, g.NgayVao, g.NgayRa, k.HoTen }).ToListAsync(ct);
        var rows = new List<LichSuNguoiORow>();
        var missing = 0;
        foreach (var h in contracts)
        {
            var spans = new List<(DateOnly Start, DateOnly End)>();
            foreach (var period in periods.Where(k => k.HopDongId == h.Id))
            {
                var end = h.NgayTraPhong is { } actual && actual < period.NgayKetThuc ? actual : period.NgayKetThuc;
                if (end < period.NgayBatDau) continue;
                // Merge consecutive renewals; retain real gaps in legacy period data.
                if (spans.Count > 0 && period.NgayBatDau.DayNumber <= spans[^1].End.DayNumber + 1)
                {
                    var previous = spans[^1]; spans[^1] = (previous.Start, end > previous.End ? end : previous.End);
                }
                else spans.Add((period.NgayBatDau, end));
            }
            if (h.Name == null || spans.Count == 0) missing++;
            void Add(string name, string role, DateOnly start, DateOnly end, bool scheduled)
            {
                if (start > end || start > to || end < from) return;
                rows.Add(new(name, role, h.Id, h.MaHopDong, start, end,
                    h.TrangThai == "DANG_HIEU_LUC" && start <= today && end >= today, scheduled));
            }
            foreach (var span in spans)
            {
                if (h.Name != null) Add(h.Name, "Người đứng tên", span.Start, span.End, h.NgayTraPhong == null || h.NgayTraPhong > span.End);
                foreach (var person in roommates.Where(g => g.HopDongId == h.Id))
                {
                    var start = person.NgayVao > span.Start ? person.NgayVao : span.Start;
                    var end = person.NgayRa is { } left && left < span.End ? left : span.End;
                    var knownEnd = person.NgayRa ?? h.NgayTraPhong;
                    if (h.NgayTraPhong is { } roomEnd && (knownEnd == null || roomEnd < knownEnd)) knownEnd = roomEnd;
                    // Even when a roommate's date is absent, the stay cannot extend beyond the contract.
                    Add(person.HoTen, "Người ở ghép", start, end, knownEnd == null || knownEnd > span.End);
                }
            }
        }
        return (rows.OrderByDescending(x => x.NgayBatDau).ThenBy(x => x.HopDongId).ThenBy(x => x.VaiTro).ThenBy(x => x.HoTen).ToList(), missing);
    }
}
