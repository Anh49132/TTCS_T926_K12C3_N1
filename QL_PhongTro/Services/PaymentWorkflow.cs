using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public sealed record PaymentReportRow(ThanhToanHoaDon Payment, BaoCaoThanhToan? Report, string? ReceiptMethod, DateTime? ReceiptAt);

public static class PaymentWorkflow
{
    // Call only after invoice ownership has been checked. Includes canceled/rejected history.
    public static async Task<List<PaymentReportRow>> HistoryAsync(AppDbContext db, int invoiceId, CancellationToken ct = default) =>
        await (from p in db.ThanhToans.AsNoTracking()
            join m in db.BaoCaoThanhToans.AsNoTracking() on p.Id equals m.ThanhToanId into metadata
            from m in metadata.DefaultIfEmpty()
            join r in db.LanThuHoaDons.AsNoTracking() on p.Id equals r.ThanhToanId into receipts
            from r in receipts.DefaultIfEmpty()
            where p.HoaDonId == invoiceId
            orderby p.Id descending
            select new PaymentReportRow(p, m, r == null ? null : r.HinhThuc, r == null ? null : r.ThoiDiemGhi)).ToListAsync(ct);

    // Participates in the caller's immediate transaction; financial state and notices commit together.
    public static async Task NotifyAsync(AppDbContext db, int paymentId, string kind, DateTime now, CancellationToken ct)
    {
        var context = await (from p in db.ThanhToans
            join i in db.HoaDons on p.HoaDonId equals i.Id
            join h in db.HopDongs on i.HopDongId equals h.Id
            join k in db.KhachThues on h.KhachDungTenId equals k.Id
            join r in db.PhongTros on h.PhongId equals r.Id
            join b in db.ToaNhas on r.ToaNhaId equals b.Id
            where p.Id == paymentId
            select new { InvoiceId = i.Id, i.MaHoaDon, p.SoTien, r.MaPhong, k.HoTen, TenantId = k.TaiKhoanId, b.ChuNhaId }).SingleAsync(ct);
        var metadata = await db.BaoCaoThanhToans.SingleOrDefaultAsync(x => x.ThanhToanId == paymentId, ct);
        var recipient = kind is "BAO_MOI" or "DA_HUY" ? context.ChuNhaId : metadata?.NguoiGuiId ?? context.TenantId;
        if (!recipient.HasValue) return;
        if (await db.ThongBaoThanhToans.AnyAsync(x => x.ThanhToanId == paymentId && x.NguoiNhanId == recipient && x.Loai == kind, ct)) return;
        var amount = context.SoTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
        var ownerEvent = kind is "BAO_MOI" or "DA_HUY";
        var title = kind switch { "BAO_MOI" => "Báo thanh toán mới", "DA_HUY" => "Khách đã hủy báo thanh toán", "DA_XAC_NHAN" => "Thanh toán đã được xác nhận", _ => "Báo thanh toán bị từ chối" };
        var body = kind switch
        {
            "BAO_MOI" => $"Khách {context.HoTen} · Phòng {context.MaPhong} vừa báo thanh toán {amount}.",
            "DA_HUY" => $"Khách {context.HoTen} đã hủy báo trả {amount} của hóa đơn {context.MaHoaDon}.",
            "DA_XAC_NHAN" => $"Chủ nhà đã xác nhận {amount} cho hóa đơn {context.MaHoaDon}. Xem số còn phải trả trong hóa đơn.",
            _ => $"Báo trả {amount} của hóa đơn {context.MaHoaDon} bị từ chối. Xem lý do trong hóa đơn và gửi lại nếu cần."
        };
        var notice = new ThongBaoThanhToan { ThanhToanId = paymentId, NguoiNhanId = recipient.Value, Loai = kind,
            TieuDe = title, NoiDung = body, NgayTao = now,
            DuongDan = (ownerEvent ? $"/HoaDonDichVu/Details/{context.InvoiceId}" : $"/ThongBao/HoaDon/{context.InvoiceId}") + $"#payment-report-{paymentId}" };
        db.ThongBaoThanhToans.Add(notice);
        if (kind != "BAO_MOI")
            await db.ThongBaoThanhToans.Where(x => x.ThanhToanId == paymentId && x.Loai == "BAO_MOI" && x.NgayDoc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.NgayDoc, now), ct);
        await db.SaveChangesAsync(ct);
        await PaymentEmailQueue.EnqueueAsync(db,notice,context.InvoiceId,now,ct);
    }
}
