using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed partial class HoaDonDichVuService
{
    private static string SnapshotNhap(HoaDon bill) => JsonSerializer.Serialize(new SnapshotHoaDonNhap(bill.TongTien,
        bill.ChiTiet.OrderBy(x => x.SoThuTu).Select(x => new DongLichSuHoaDon(x.SoThuTu, x.LoaiKhoan,
            x.TenKhoan, x.ChiSoDau, x.ChiSoCuoi, x.SoLuong, x.DonGia, x.ThanhTien, x.GhiChu)).ToList(), bill.HanThanhToan));

    private async Task GhiLichSuNhapAsync(int actorId, HoaDon bill, string before)
    {
        var actor = await db.TaiKhoans.AsNoTracking().SingleAsync(x => x.Id == actorId && x.DangHoatDong);
        var after = SnapshotNhap(bill);
        var now = DateTime.UtcNow;
        // Existing append-only audit table; same immediate transaction as the invoice save.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) VALUES ({actor.Id},{actor.HoTen},{actor.VaiTro},{"hoa_don"},{bill.Id},{"SUA_NHAP"},{before},{after},{now})");
    }

    public async Task<IReadOnlyList<LichSuHoaDonNhapViewModel>> LichSuNhapAsync(int actor, int invoiceId)
    {
        // Never expose financial edit notes through a tenant or cross-owner request.
        await OwnedInvoiceAsync(actor, invoiceId);
        var rows = await db.NhatKyHoatDongs.AsNoTracking()
            .Where(x => x.LoaiDoiTuong == "hoa_don" && x.DoiTuongId == invoiceId && x.HanhDong == "SUA_NHAP")
            .OrderByDescending(x => x.ThoiDiem).ThenByDescending(x => x.Id).ToListAsync();
        return rows.Select(x => new LichSuHoaDonNhapViewModel(x.Id, x.TenNguoiThucHien ?? "Tài khoản đã lưu trong nhật ký",
            x.ThoiDiem, Read(x.DuLieuTruoc), Read(x.DuLieuSau))).ToList();
    }

    private static SnapshotHoaDonNhap? Read(string? json)
    {
        try { return json is null ? null : JsonSerializer.Deserialize<SnapshotHoaDonNhap>(json); }
        catch (JsonException) { return null; }
    }
}
