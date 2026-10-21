using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed partial class DichVuService
{
    private Task<List<CauHinhDichVu>> CauHinhDienNuocAsync(int buildingId) =>
        db.CauHinhDichVus.Include(x => x.DichVu)
            .Where(x => x.ToaNhaId == buildingId && x.PhongId == null &&
                (x.DichVu.MaDichVu == "DIEN" || x.DichVu.MaDichVu == "NUOC"))
            .OrderBy(x => x.TuNgay).ToListAsync();

    private static CauHinhDichVu? HienTai(List<CauHinhDichVu> rows, string code, DateOnly today) =>
        rows.SingleOrDefault(x => x.DichVu.MaDichVu == code && x.TuNgay <= today &&
            (x.DenNgay == null || x.DenNgay >= today));

    // A snapshot detects stale forms, including edits that retain the pending row's ID.
    private static string TrangThai(List<CauHinhDichVu> rows) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(rows.OrderBy(x => x.Id).Select(x => new
        { x.Id, x.ToaNhaId, x.DichVuId, x.CachTinh, x.DonViTinh, x.DonGia, x.TuNgay, x.DenNgay,
          x.DangApDung, x.DaChotGia, CatalogActive = x.DichVu.DangHoatDong })))));

    private static CauHinhTienDichVuViewModel Input(CauHinhDichVu? row) => new()
    {
        CachTinh = row?.CachTinh ?? CachTinhDichVu.TheoChiSo,
        DonGiaChiSo = row is { DaChotGia: true, CachTinh: CachTinhDichVu.TheoChiSo } ? row.DonGia : null,
        TienMotNguoi = row is { DaChotGia: true, CachTinh: CachTinhDichVu.TheoNguoi } ? row.DonGia : null
    };

    public async Task<CauHinhDienNuocViewModel> LayCauHinhDienNuocAsync(int accountId, int buildingId)
    {
        if (!await SoHuuToaNhaAsync(accountId, buildingId)) throw new UnauthorizedAccessException();
        var today = DateOnly.FromDateTime((time?.UtcNow ?? DateTime.UtcNow).AddHours(7));
        var next = new DateOnly(today.Year, today.Month, 1).AddMonths(1);
        var rows = await CauHinhDienNuocAsync(buildingId);
        var electricity = HienTai(rows, "DIEN", today);
        var water = HienTai(rows, "NUOC", today);
        var pendingElectricity = rows.SingleOrDefault(x => x.DichVu.MaDichVu == "DIEN" && x.TuNgay == next);
        var pendingWater = rows.SingleOrDefault(x => x.DichVu.MaDichVu == "NUOC" && x.TuNgay == next);
        return new CauHinhDienNuocViewModel
        {
            ToaNhaId = buildingId,
            TenToaNha = await db.ToaNhas.Where(x => x.Id == buildingId).Select(x => x.TenToaNha).SingleAsync(),
            Dien = Input(pendingElectricity ?? electricity), Nuoc = Input(pendingWater ?? water),
            DienDaLuu = Input(pendingElectricity ?? electricity), NuocDaLuu = Input(pendingWater ?? water),
            DienHienTai = electricity is null ? null : Input(electricity),
            NuocHienTai = water is null ? null : Input(water),
            DienCho = pendingElectricity is null ? null : Input(pendingElectricity),
            NuocCho = pendingWater is null ? null : Input(pendingWater),
            KyHienTai = next.AddMonths(-1), KyKeTiep = next, KyDaXem = next, TrangThaiDaXem = TrangThai(rows)
        };
    }

    public async Task LuuCauHinhDienNuocAsync(int accountId, CauHinhDienNuocViewModel input)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!await SoHuuToaNhaAsync(accountId, input.ToaNhaId)) throw new UnauthorizedAccessException();
        var utcNow = time?.UtcNow ?? DateTime.UtcNow;
        var today = DateOnly.FromDateTime(utcNow.AddHours(7));
        var next = new DateOnly(today.Year, today.Month, 1).AddMonths(1);
        if (input.KyDaXem != next)
            throw new InvalidOperationException("Kỳ áp dụng đã thay đổi. Hãy tải lại trang và kiểm tra kỳ mới trước khi lưu.");
        Validator.ValidateObject(input.Dien, new ValidationContext(input.Dien), true);
        Validator.ValidateObject(input.Nuoc, new ValidationContext(input.Nuoc), true);
        // Reload tracked entities as well: a context may have served an earlier preview.
        foreach (var entry in db.ChangeTracker.Entries<CauHinhDichVu>().ToList()) await entry.ReloadAsync();
        var rows = await CauHinhDienNuocAsync(input.ToaNhaId);
        if (input.TrangThaiDaXem != TrangThai(rows))
            throw new InvalidOperationException("Cấu hình vừa thay đổi. Hãy tải lại trang trước khi lưu.");
        await Save("DIEN", "Điện", "kWh", input.Dien);
        await Save("NUOC", "Nước", "m³", input.Nuoc);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        async Task Save(string code, string name, string unit, CauHinhTienDichVuViewModel value)
        {
            var current = HienTai(rows, code, today);
            var pending = rows.SingleOrDefault(x => x.DichVu.MaDichVu == code && x.TuNgay == next);
            if (!value.ThayDoiSoVoi(Input(pending ?? current))) return;
            if (rows.Any(x => x.DichVu.MaDichVu == code && x.TuNgay > next))
                throw new InvalidOperationException("Có phiên bản tương lai sau kỳ kế tiếp của " + code + ". Cần chốt cách xử lý trước khi lưu.");
            if (pending is not null && current is { DaChotGia: true } && !value.ThayDoiSoVoi(Input(current)))
                throw new InvalidOperationException("Chưa chốt nghiệp vụ đổi cấu hình chờ về cấu hình hiện tại. Chưa lưu thay đổi.");
            if (current is { DangApDung: false } || (current is not null && !current.DichVu.DangHoatDong) ||
                pending is { DangApDung: false } || (pending is not null && !pending.DichVu.DangHoatDong))
                throw new InvalidOperationException("Dịch vụ đang ngừng áp dụng. Cần kiểm tra lịch dịch vụ trước khi lưu.");
            // Snapshot every room override before changing the shared utility method/unit/price.
            // Those rooms continue using their own complete configuration.
            if (current is not null) await BaoToanCauHinhRiengAsync(current, next, accountId);
            if (pending is not null && await DaThamChieuAsync(pending.Id))
                throw new InvalidOperationException("Cấu hình chờ đã được chứng từ hoặc hợp đồng tham chiếu. Chưa chốt nghiệp vụ sửa cấu hình này.");
            var row = pending;
            if (row is null)
            {
                // Both saves stay in this transaction; failure restores the old interval.
                if (current is not null)
                {
                    if (current.DenNgay == null || current.DenNgay >= next) current.DenNgay = next.AddDays(-1);
                    await db.SaveChangesAsync();
                }
                var catalog = current?.DichVu ?? await db.DichVus.SingleOrDefaultAsync(x => x.MaDichVu == code);
                if (catalog is null)
                {
                    catalog = new DichVu { MaDichVu = code, TenDichVu = name };
                    db.DichVus.Add(catalog);
                }
                if (catalog.Id == 0 || !await db.DichVuToaNhas.AnyAsync(x =>
                    x.ToaNhaId == input.ToaNhaId && x.DichVuId == catalog.Id))
                    db.DichVuToaNhas.Add(new DichVuToaNha
                    {
                        ToaNhaId = input.ToaNhaId, DichVu = catalog, ApDungMacDinh = false
                    });
                row = new CauHinhDichVu
                {
                    ToaNhaId = input.ToaNhaId, DichVu = catalog, TuNgay = next,
                    DangApDung = current?.DangApDung ?? true, NguoiTaoId = accountId, NgayTao = utcNow
                };
                db.CauHinhDichVus.Add(row);
            }
            row.CachTinh = value.CachTinh;
            row.DonViTinh = value.CachTinh == CachTinhDichVu.TheoChiSo ? unit : "người/tháng";
            row.DonGia = value.GiaApDung!.Value;
            row.DaChotGia = true;
        }
    }

    private async Task<bool> DaThamChieuAsync(int configId)
    {
        // These optional tables need not exist in a services-only database.
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('chi_tiet_hoa_don','hop_dong_dich_vu')";
        var tables = new List<string>();
        using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        foreach (var table in tables)
        {
            command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE cau_hinh_dich_vu_id=$id";
            command.Parameters.Clear();
            var parameter = command.CreateParameter(); parameter.ParameterName = "$id"; parameter.Value = configId;
            command.Parameters.Add(parameter);
            if (Convert.ToInt64(await command.ExecuteScalarAsync()) > 0) return true;
        }
        return false;
    }
}
