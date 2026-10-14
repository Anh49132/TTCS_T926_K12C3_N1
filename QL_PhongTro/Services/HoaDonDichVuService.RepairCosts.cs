using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
namespace QL_PhongTro.Services;

public sealed partial class HoaDonDichVuService
{
    // Validate existing snapshots only. Never select/add pending fees automatically.
    private async Task ValidateRepairSnapshotsAsync(HoaDon invoice)
    {
        var lines = invoice.ChiTiet.Where(x => x.BaoHongId.HasValue).ToList();
        if (lines.Count == 0) return;
        if (lines.Select(x => x.BaoHongId).Distinct().Count() != lines.Count)
            throw new InvalidOperationException("Một báo hỏng chỉ có một khoản thu hiệu lực.");
        var month = new DateOnly(invoice.Nam, invoice.Thang, 1);
        foreach (var line in lines)
        {
            var cost = await db.ChiPhiBaoHongs.AsNoTracking().SingleOrDefaultAsync(x => x.BaoHongId == line.BaoHongId);
            if (cost is null || cost.HopDongId != invoice.HopDongId || cost.BenChiuPhi != "KHACH_THUE" ||
                cost.KyDuKien is null || month < cost.KyDuKien || line.LoaiKhoan != "PHAT_SINH" ||
                line.SoLuong != 1 || line.DonGia != cost.SoTien || line.ThanhTien != cost.SoTien)
                throw new InvalidOperationException("Khoản sửa chữa không khớp chi phí, hợp đồng gốc hoặc kỳ dự kiến.");
            if (await (from d in db.ChiTietHoaDons join h in db.HoaDons on d.HoaDonId equals h.Id
                where d.BaoHongId == line.BaoHongId && h.Id != invoice.Id && h.TrangThai != "DA_HUY" select d.Id).AnyAsync())
                throw new InvalidOperationException("Chi phí sửa chữa đã nằm trong hóa đơn khác đang hiệu lực.");
        }
    }
}
