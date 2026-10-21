using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public class HopDongService(AppDbContext db)
{
    public Task<List<HopDongChongLan>> ChongLanAsync(int room, DateOnly start, DateOnly end, CancellationToken ct) =>
        (from h in db.HopDongs.AsNoTracking()
         join k in db.KyHopDongs on h.Id equals k.HopDongId
         where h.PhongId == room && (h.TrangThai == "DANG_HIEU_LUC" || h.TrangThai == "CHO_HIEU_LUC")
             && k.NgayBatDau <= end && k.NgayKetThuc >= start
         orderby k.NgayBatDau
         select new HopDongChongLan(h.Id, h.MaHopDong, k.NgayBatDau, k.NgayKetThuc)).ToListAsync(ct);

    // Called only within the immediate write transaction: allocation and contract save are atomic.
    public async Task<string> SinhMaAsync(int year, CancellationToken ct)
    {
        var prefix = $"HD-{year:0000}-";
        var codes = await db.HopDongs.Where(h => h.MaHopDong.StartsWith(prefix)).Select(h => h.MaHopDong).ToListAsync(ct);
        var max = codes.Where(c => c.Length == prefix.Length + 4 && c[prefix.Length..].All(char.IsAsciiDigit))
            .Select(c => int.Parse(c[prefix.Length..])).DefaultIfEmpty(0).Max();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hop_dong_so_ma(nam,so_cuoi) VALUES({year},{max}) ON CONFLICT(nam) DO UPDATE SET so_cuoi=MAX(so_cuoi,{max})", ct);
        if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hop_dong_so_ma SET so_cuoi=so_cuoi+1 WHERE nam={year} AND so_cuoi<9999", ct) != 1)
            throw new ArgumentException("Đã hết dải mã hợp đồng trong năm. Không thể lưu thêm hợp đồng.");
        var connection = db.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT so_cuoi FROM hop_dong_so_ma WHERE nam=$year";
        var parameter = command.CreateParameter(); parameter.ParameterName = "$year"; parameter.Value = year;
        command.Parameters.Add(parameter);
        var next = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if (max >= 9999 || codes.Contains(prefix + next.ToString("0000")))
            throw new ArgumentException("Đã hết dải mã hợp đồng trong năm. Không thể lưu thêm hợp đồng.");
        return prefix + next.ToString("0000");
    }
}
