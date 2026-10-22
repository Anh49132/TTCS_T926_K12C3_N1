using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed class ChiPhiConflictException() : Exception("Chi phí đã thay đổi hoặc đã gắn vào hóa đơn. Hãy tải lại trang.");

public sealed class BaoHongChiPhiService(AppDbContext db, ITimeProvider clock)
{
    public static long ParseAmount(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Any(c => c < '0' || c > '9') ||
            !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
            throw new BaoHongValidationException("SoTien", "Số tiền phải là số nguyên VND từ 1 đến 9.223.372.036.854.775.807 đồng.");
        return value;
    }

    private async Task<bool> HasInvoiceModuleAsync(CancellationToken ct)
    {
        var count = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','chi_tiet_hoa_don')").SingleAsync(ct);
        // A partial installation is invalid; never treat it as an absent module.
        if (count == 1) throw new InvalidOperationException("Schema hóa đơn chưa đầy đủ. Cần kiểm tra database trước khi lưu chi phí.");
        return count == 2;
    }

    private async Task<DateOnly?> NextPeriodAsync(int contractId, DateTime recorded, bool hasInvoices, CancellationToken ct)
    {
        var contract = await db.HopDongs.AsNoTracking().SingleAsync(x => x.Id == contractId, ct);
        if (contract.TrangThai is not ("DANG_HIEU_LUC" or "CHO_HIEU_LUC")) return null;
        var day = DateOnly.FromDateTime(recorded.AddHours(7));
        var start = new DateOnly(day.Year, day.Month, 1).AddMonths(1);
        var periods = await db.KyHopDongs.AsNoTracking().Where(x => x.HopDongId == contractId).OrderBy(x => x.NgayBatDau).ToListAsync(ct);
        var issued = hasInvoices
            ? await db.HoaDons.AsNoTracking().Where(x => x.HopDongId == contractId && x.TrangThai == "DA_PHAT_HANH")
                .Select(x => x.Nam * 12 + x.Thang).ToListAsync(ct)
            : [];
        foreach (var period in periods)
        {
            var first = new DateOnly(period.NgayBatDau.Year, period.NgayBatDau.Month, 1);
            var candidate = first > start ? first : start;
            var end = contract.NgayTraPhong is DateOnly returned && returned < period.NgayKetThuc ? returned : period.NgayKetThuc;
            while (candidate <= end)
            {
                if (!issued.Contains(candidate.Year * 12 + candidate.Month)) return candidate;
                if (candidate.Year == 9999 && candidate.Month == 12) break;
                candidate = candidate.AddMonths(1);
            }
        }
        return null;
    }

    public async Task<ChiPhiBaoHong> LuuAsync(int managerId, int reportId, BaoHongChiPhiInput input, CancellationToken ct = default)
    {
        var amount = ParseAmount(input.SoTien);
        if (input.BenChiuPhi is not ("CHU_NHA" or "KHACH_THUE"))
            throw new BaoHongValidationException("BenChiuPhi", "Chọn Chủ nhà hoặc Khách thuê chịu chi phí.");
        if (!Guid.TryParseExact(input.KhoaChongTrung, "N", out _) || input.KhoaChongTrung != input.KhoaChongTrung.ToLowerInvariant())
            throw new BaoHongValidationException("", "Phiên gửi không hợp lệ. Hãy tải lại trang.");
        await db.Database.OpenConnectionAsync(ct);
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite, ct);
        var actor = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == managerId && x.VaiTro == "QUAN_LY" &&
            x.DangHoatDong && !x.IsDeleted && x.EmailConfirmed && !x.MustChangePassword, ct);
        var level = await db.RolePermissions.AsNoTracking().Where(x => x.RoleCode == "QUAN_LY" && x.ModuleCode == "BAO_HONG")
            .Select(x => x.AccessLevel).SingleOrDefaultAsync(ct);
        if (actor is null || level is not ("WRITE" or "FULL")) throw new UnauthorizedAccessException();
        var report = await db.BaoHongs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reportId, ct) ?? throw new KeyNotFoundException();
        if (!await (from p in db.PhongTros join t in db.ToaNhas on p.ToaNhaId equals t.Id
            where p.Id == report.PhongId && t.QuanLyId == managerId select p.Id).AnyAsync(ct)) throw new UnauthorizedAccessException();
        var replay = await db.LichSuChiPhiBaoHongs.AsNoTracking().SingleOrDefaultAsync(x => x.KhoaChongTrung == input.KhoaChongTrung, ct);
        if (replay is not null)
        {
            var saved = await db.ChiPhiBaoHongs.AsNoTracking().SingleAsync(x => x.Id == replay.ChiPhiId, ct);
            if (saved.BaoHongId != reportId || replay.NguoiCapNhatId != managerId) throw new UnauthorizedAccessException();
            if (replay.SoTienSau != amount || replay.BenChiuPhiSau != input.BenChiuPhi || replay.PhienBanTruoc != input.PhienBan)
                throw new BaoHongValidationException("", "Phiên gửi đã dùng cho nội dung khác. Hãy tải lại trang.");
            await tx!.CommitAsync(ct); return saved;
        }
        if (report.TrangThai != "HOAN_THANH") throw new BaoHongValidationException("", "Chỉ ghi nhận chi phí cho báo hỏng Hoàn thành.");
        var hasInvoices = await HasInvoiceModuleAsync(ct);
        var cost = await db.ChiPhiBaoHongs.SingleOrDefaultAsync(x => x.BaoHongId == reportId, ct);
        if (input.PhienBan != (cost?.PhienBan ?? 0) ||
            (hasInvoices && await db.ChiTietHoaDons.AnyAsync(x => x.BaoHongId == reportId, ct))) throw new ChiPhiConflictException();
        var now = clock.UtcNow;
        var history = new LichSuChiPhiBaoHong { NguoiCapNhatId = managerId, ThoiDiem = now, Loai = cost is null ? "TAO" : "SUA",
            SoTienTruoc = cost?.SoTien, BenChiuPhiTruoc = cost?.BenChiuPhi, KyDuKienTruoc = cost?.KyDuKien,
            PhienBanTruoc = cost?.PhienBan ?? 0, KhoaChongTrung = input.KhoaChongTrung };
        if (cost is null)
        {
            cost = new ChiPhiBaoHong { BaoHongId = reportId, HopDongId = report.HopDongId, NguoiTaoId = managerId, NgayTao = now };
            db.ChiPhiBaoHongs.Add(cost);
        }
        cost.SoTien = amount; cost.BenChiuPhi = input.BenChiuPhi; cost.ChoThuKhach = input.BenChiuPhi == "KHACH_THUE";
        cost.KyDuKien = cost.ChoThuKhach ? await NextPeriodAsync(cost.HopDongId, cost.NgayTao, hasInvoices, ct) : null;
        cost.NguoiCapNhatId = managerId; cost.NgayCapNhat = now; cost.PhienBan++;
        await db.SaveChangesAsync(ct);
        history.ChiPhiId = cost.Id; history.SoTienSau = amount; history.BenChiuPhiSau = cost.BenChiuPhi;
        history.KyDuKienSau = cost.KyDuKien; history.PhienBanSau = cost.PhienBan;
        db.LichSuChiPhiBaoHongs.Add(history); await db.SaveChangesAsync(ct);
        var metadata = JsonSerializer.Serialize(new { cost.BaoHongId, cost.HopDongId, cost.SoTien, cost.BenChiuPhi, cost.KyDuKien, cost.PhienBan });
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_sau,thoi_diem) VALUES({actor.Id},{actor.HoTen},{actor.VaiTro},{"chi_phi_bao_hong"},{cost.Id},{history.Loai},{metadata},{now})", ct);
        await tx!.CommitAsync(ct); return cost;
    }
}
