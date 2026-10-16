using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed partial class GhiThuHoaDonService
{
    public async Task<ThanhToanHoaDonSummary> XacNhanBaoCaoAsync(int actorId, int paymentId, string? key, CancellationToken ct = default)
    {
        if (!Guid.TryParseExact(key, "N", out _))
            throw new GhiThuValidationException(new() { [""] = ["Phiên xác nhận không hợp lệ. Hãy tải lại hóa đơn."] });
        await db.Database.OpenConnectionAsync(ct);
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite, ct);
        var report = await db.ThanhToans.SingleOrDefaultAsync(x => x.Id == paymentId, ct)
            ?? throw new UnauthorizedAccessException();
        var invoice = await OwnedAsync(actorId, report.HoaDonId, ct);
        var actor = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.DangHoatDong && !x.IsDeleted && x.VaiTro == "CHU_NHA", ct)
            ?? throw new UnauthorizedAccessException();
        var existing = await db.LanThuHoaDons.AsNoTracking().SingleOrDefaultAsync(x => x.KhoaChongTrung == key || x.ThanhToanId == paymentId, ct);
        if (existing is not null)
        {
            if (existing.ThanhToanId != paymentId || report.TrangThai != "DA_XAC_NHAN")
                throw new InvalidOperationException("Báo cáo hoặc phiên xác nhận đã thay đổi. Hãy tải lại hóa đơn.");
            var replay = await SummaryAsync(invoice, ct);
            await tx!.CommitAsync(ct);
            return replay;
        }
        if (report.TrangThai != "CHO_XAC_NHAN" || report.HinhThucBaoCao is not ("TIEN_MAT" or "CHUYEN_KHOAN"))
            throw new InvalidOperationException("Báo cáo không còn chờ xác nhận. Hãy tải lại hóa đơn.");
        var before = await SummaryAsync(invoice, ct);
        if (!before.ChoPhepGhiThu || report.SoTien > before.ConPhaiTra)
            throw new GhiThuValidationException(new() { [""] = ["Không thể xác nhận: số tiền báo trả vượt dư nợ hiện tại hoặc hóa đơn không còn được thu."] });

        report.TrangThai = "DA_XAC_NHAN";
        db.LanThuHoaDons.Add(new() { ThanhToanId = report.Id, HinhThuc = report.HinhThucBaoCao,
            NguoiXacNhanId = actorId, TenNguoiXacNhan = actor.HoTen, ThoiDiemGhi = clock.UtcNow, KhoaChongTrung = key! });
        await db.SaveChangesAsync(ct);
        var prior = JsonSerializer.Serialize(new { trang_thai = "CHO_XAC_NHAN" });
        var after = JsonSerializer.Serialize(new { trang_thai = "DA_XAC_NHAN", so_tien = report.SoTien, hoa_don_id = invoice.Id });
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) VALUES({actorId},{actor.HoTen},{actor.VaiTro},{"thanh_toan"},{report.Id},{"DOI_TRANG_THAI"},{prior},{after},{clock.UtcNow})", ct);
        var result = await SummaryAsync(invoice, ct);
        await tx!.CommitAsync(ct);
        return result;
    }
}
