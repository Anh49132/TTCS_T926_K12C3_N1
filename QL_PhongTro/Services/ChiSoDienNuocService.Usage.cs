using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QL_PhongTro.ViewModels;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QL_PhongTro.Services;

public sealed partial class ChiSoDienNuocService
{
    private sealed record UsagePeriod(DateOnly TuNgay, DateOnly DenNgay, decimal Dau, decimal Cuoi, string Source, int Id, int Version);
    private sealed record UsageConfirmation(string Fingerprint, DateTime ExpiresUtc);

    private async Task<HashSet<string>> CheckUsageAsync(int actor, LuuChiSoInput input, PhongGhiChiSo room, DateOnly first, CancellationToken ct)
    {
        input.CanhBaos.Clear();
        var meter = await db.ChiSoDienNuocs.AsNoTracking().Where(x => x.HopDongId == room.HopDongId && x.DenNgay < first).ToListAsync(ct);
        var connection = db.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','chi_tiet_hoa_don')";
        var hasInvoices = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 2;
        var legacy = hasInvoices ? await (from invoice in db.HoaDons.AsNoTracking()
            join line in db.ChiTietHoaDons on invoice.Id equals line.HoaDonId
            where invoice.HopDongId == room.HopDongId && invoice.TrangThai == "DA_PHAT_HANH" && invoice.DenNgay < first
                && line.LoaiKhoan == "DICH_VU" && line.CachTinhApDung == "THEO_CHI_SO"
                && line.ChiSoDau != null && line.ChiSoCuoi != null
            select new { invoice.TuNgay, invoice.DenNgay, line.DichVuId, Dau = line.ChiSoDau!.Value, Cuoi = line.ChiSoCuoi!.Value, line.Id }).ToListAsync(ct) : [];
        var snapshots = new List<object>();
        var warnings = new List<MeterUsageWarning>();
        foreach (var service in room.DichVu.OrderBy(x => x.Id))
        {
            var history = meter.Where(x => x.DichVuId == service.Id)
                .Select(x => new UsagePeriod(x.TuNgay, x.DenNgay, x.ChiSoDau, x.ChiSoCuoi, "meter", x.Id, x.PhienBan))
                .Concat(legacy.Where(x => x.DichVuId == service.Id)
                    .Select(x => new UsagePeriod(x.TuNgay, x.DenNgay, x.Dau, x.Cuoi, "invoice", x.Id, 0)))
                .Where(x => x.TuNgay <= x.DenNgay && x.Dau >= 0 && x.Cuoi >= x.Dau
                    && x.Cuoi <= 99999999999.999m && decimal.Round(x.Dau, 3) == x.Dau && decimal.Round(x.Cuoi, 3) == x.Cuoi)
                .GroupBy(x => new { x.TuNgay, x.DenNgay })
                .Select(g => g.OrderBy(x => x.Source == "meter" ? 0 : 1).ThenByDescending(x => x.Id).First())
                .OrderByDescending(x => x.DenNgay).ThenByDescending(x => x.TuNgay).Take(3).ToList();
            var value = (service.Ma == "DIEN" ? input.DienMoi : input.NuocMoi)!.Value;
            var consumption = value - service.Truoc.GiaTri!.Value;
            snapshots.Add(new { service.Id, service.Ma, Previous = service.Truoc.GiaTri, Value = value, service.PhienBan, service.DaKhoa, History = history });
            // Compare sums without rounding the repeating average at the threshold.
            if (history.Count == 3 && consumption * 3 > 2 * history.Sum(x => x.Cuoi - x.Dau))
            {
                var average = history.Sum(x => x.Cuoi - x.Dau) / 3;
                warnings.Add(new(service.Ma, service.Truoc.GiaTri.Value, value, consumption, average, 2 * average, history[0].Cuoi - history[0].Dau));
            }
        }
        var abnormal = warnings.Select(x => x.Ma).ToHashSet();
        if (warnings.Count == 0) return abnormal;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { Actor = actor, input.ToaNhaId, room.PhongId, room.HopDongId, Period = first, room.PhienBanPhong, Services = snapshots }))));
        var protector = protection.CreateProtector("S3-05:4.UsageConfirmation.v1");
        bool confirmed = false;
        if (input.XacNhanBatThuong && !string.IsNullOrEmpty(input.MaXacNhan))
        {
            try
            {
                var token = JsonSerializer.Deserialize<UsageConfirmation>(protector.Unprotect(input.MaXacNhan));
                confirmed = token != null && token.Fingerprint == fingerprint && token.ExpiresUtc > clock.UtcNow;
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { }
        }
        if (!confirmed)
        {
            input.CanhBaos = warnings;
            input.XacNhanBatThuong = false;
            input.MaXacNhan = protector.Protect(JsonSerializer.Serialize(new UsageConfirmation(fingerprint, clock.UtcNow.AddMinutes(15))));
        }
        return abnormal;
    }
}
