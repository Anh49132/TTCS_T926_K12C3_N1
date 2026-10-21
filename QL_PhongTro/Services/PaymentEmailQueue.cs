using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
namespace QL_PhongTro.Services;

public static class PaymentEmailQueue
{
    public const string Prefix="THANH_TOAN:";
    public static string Key(int noticeId,string kind)=>$"{Prefix}{noticeId}:{kind}";
    // Uses the existing durable email log, inside the same payment transaction.
    public static async Task EnqueueAsync(AppDbContext db,ThongBaoThanhToan notice,int invoiceId,DateTime now,CancellationToken ct)
    {
        var recipient=await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==notice.NguoiNhanId
            && x.DangHoatDong && !x.IsDeleted && x.EmailConfirmed && !x.MustChangePassword,ct);
        if(recipient is null)return;
        db.EmailGuiLogs.Add(new(){HoaDonId=invoiceId,LoaiThongBao=Key(notice.Id,notice.Loai),NguoiNhanId=recipient.Id,
            EmailNguoiNhan=recipient.Email,TrangThai="CHO_GUI",SoLanThu=0,NgayGui=now});
        await db.SaveChangesAsync(ct);
    }
}
