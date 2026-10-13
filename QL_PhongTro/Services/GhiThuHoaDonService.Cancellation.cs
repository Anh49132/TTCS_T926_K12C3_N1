using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed partial class GhiThuHoaDonService
{
    public async Task<bool> HuySanSangAsync(CancellationToken ct = default) =>
        await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='huy_lan_thu_hoa_don'").SingleAsync(ct) == 1;

    public async Task<ThanhToanHoaDonSummary> HuyAsync(int actorId, HuyLanThuHoaDonInput input, CancellationToken ct = default)
    {
        if (!await HuySanSangAsync(ct)) throw new InvalidOperationException("Chưa cài chức năng hủy lần thu. Cần nâng cấp CSDL bằng bước v27 trên bản sao trước.");
        await db.Database.OpenConnectionAsync(ct);
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite, ct);
        var receipt = await db.ThanhToans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ThanhToanId, ct)
            ?? throw new UnauthorizedAccessException();
        var invoice = await OwnedAsync(actorId, receipt.HoaDonId, ct);
        var actor = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.DangHoatDong && !x.IsDeleted && x.VaiTro == "CHU_NHA", ct)
            ?? throw new UnauthorizedAccessException();
        if (!await db.RolePermissions.AsNoTracking().AnyAsync(x => x.RoleCode == "CHU_NHA" && x.ModuleCode == "TAI_CHINH"
            && (x.AccessLevel == "WRITE" || x.AccessLevel == "FULL"), ct)) throw new UnauthorizedAccessException();
        // Ownership is checked now, not against the original recorder; there is no age limit.
        if (receipt.TrangThai == "DA_HUY" || await db.HuyLanThuHoaDons.AnyAsync(x => x.ThanhToanId == receipt.Id, ct))
            throw new InvalidOperationException("Lần thu đã hủy. Không thể hủy lần nữa hoặc khôi phục; muốn thu lại hãy ghi lần thu mới.");
        if (receipt.TrangThai != "DA_XAC_NHAN") throw new InvalidOperationException("Chỉ hủy lần thu đã xác nhận.");
        if (input.PhienBan != 0) throw new InvalidOperationException("Lần thu đã thay đổi hoặc thiếu phiên bản. Hãy tải lại hóa đơn trước khi hủy.");
        var reason = input.LyDo?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5 || reason.Length > 500)
            throw new GhiThuValidationException(new() { ["LyDo"] = ["Lý do phải có từ 5 đến 500 ký tự, không chỉ gồm khoảng trắng."] });
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE thanh_toan SET trang_thai='DA_HUY' WHERE id={receipt.Id} AND trang_thai='DA_XAC_NHAN'", ct);
        if (count != 1) throw new InvalidOperationException("Lần thu đã thay đổi. Hãy tải lại hóa đơn.");
        db.HuyLanThuHoaDons.Add(new() { ThanhToanId = receipt.Id, LyDoHuy = reason, NguoiHuyId = actorId,
            TenNguoiHuy = actor.HoTen, ThoiDiemHuy = clock.UtcNow, PhienBan = 1 });
        await db.SaveChangesAsync(ct);
        // Business-only allowlist; the free-text reason is stored in the private receipt history, never in audit.
        var before = JsonSerializer.Serialize(new { trang_thai = "DA_XAC_NHAN", phien_ban = 0 });
        var after = JsonSerializer.Serialize(new { trang_thai = "DA_HUY", phien_ban = 1, nguoi_huy_id = actorId, thoi_diem_huy = clock.UtcNow });
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) VALUES({actorId},{actor.HoTen},{actor.VaiTro},{"thanh_toan"},{receipt.Id},{"HUY"},{before},{after},{clock.UtcNow})", ct);
        var summary = await SummaryAsync(invoice, ct);
        await tx!.CommitAsync(ct);
        return summary;
    }
}
