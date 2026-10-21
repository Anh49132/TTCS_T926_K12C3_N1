using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
namespace QL_PhongTro.Services;
public sealed class PaymentEmailDispatcher(AppDbContext db,IPaymentEmailSender sender,ITimeProvider clock)
{
    private async Task<(ThongBaoThanhToan Notice,string Invoice)?> Allowed(EmailGuiLog row,CancellationToken ct)
    {
        var parts=row.LoaiThongBao.Split(':');
        if(parts.Length!=3 || !int.TryParse(parts[1],out var noticeId))return null;
        var notice=await db.ThongBaoThanhToans.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==noticeId && x.NguoiNhanId==row.NguoiNhanId,ct);
        if(notice is null || row.LoaiThongBao!=PaymentEmailQueue.Key(notice.Id,notice.Loai))return null;
        var context=await (from payment in db.ThanhToans.AsNoTracking()
            join invoice in db.HoaDons on payment.HoaDonId equals invoice.Id
            join contract in db.HopDongs on invoice.HopDongId equals contract.Id
            join room in db.PhongTros on contract.PhongId equals room.Id
            join building in db.ToaNhas on room.ToaNhaId equals building.Id
            where payment.Id==notice.ThanhToanId && invoice.Id==row.HoaDonId && invoice.TrangThai=="DA_PHAT_HANH"
            select new {invoice.MaHoaDon,building.ChuNhaId,State=payment.TrangThai,
                TenantId=db.KhachThues.Where(x=>x.Id==contract.KhachDungTenId).Select(x=>x.TaiKhoanId).FirstOrDefault()}).SingleOrDefaultAsync(ct);
        if(context is null)return null;
        var account=await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==row.NguoiNhanId && x.Email==row.EmailNguoiNhan
            && x.DangHoatDong && !x.IsDeleted && x.EmailConfirmed && !x.MustChangePassword,ct);
        if(account is null || !await db.RolePermissions.AnyAsync(x=>x.RoleCode==account.VaiTro && x.ModuleCode=="TAI_CHINH"
            && (x.AccessLevel=="READ" || x.AccessLevel=="WRITE" || x.AccessLevel=="FULL"),ct))return null;
        var ownerEvent=notice.Loai is "BAO_MOI" or "DA_HUY";
        if(ownerEvent ? account.VaiTro!="CHU_NHA" || context.ChuNhaId!=account.Id : account.VaiTro!="KHACH_THUE")return null;
        if(!ownerEvent && context.TenantId!=account.Id)return null;
        var expected=notice.Loai switch {"BAO_MOI"=>"CHO_XAC_NHAN","DA_HUY"=>"DA_HUY","DA_XAC_NHAN"=>"DA_XAC_NHAN","TU_CHOI"=>"TU_CHOI",_=>null};
        if(expected is null || expected!=context.State)return null;
        return (notice,context.MaHoaDon);
    }
    public async Task<int> DispatchAsync(CancellationToken ct=default)
    {
        var exists=await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('email_gui_log','thong_bao_thanh_toan')").SingleAsync(ct);
        if(exists!=2)return 0;
        var now=clock.UtcNow;
        var scoped=db.EmailGuiLogs.Where(x=>x.LoaiThongBao.StartsWith(PaymentEmailQueue.Prefix));
        await scoped.Where(x=>x.TrangThai=="DANG_GUI" && x.SoLanThu>=4 && x.NgayThuTiep<=now).ExecuteUpdateAsync(s=>s
            .SetProperty(x=>x.TrangThai,"THAT_BAI").SetProperty(x=>x.NgayThuTiep,(DateTime?)null).SetProperty(x=>x.LoiGanNhat,"Hết số lần thử gửi email thanh toán."),ct);
        var pending=scoped.Where(x=>x.SoLanThu<4 && ((x.TrangThai=="CHO_GUI" && (x.NgayThuTiep==null || x.NgayThuTiep<=now)) || (x.TrangThai=="DANG_GUI" && x.NgayThuTiep<=now)));
        var rows=await pending.AsNoTracking().OrderBy(x=>x.Id).Take(10).ToListAsync(ct);var sent=0;
        foreach(var row in rows)
        {
            ct.ThrowIfCancellationRequested();var attempt=row.SoLanThu+1;
            var claimed=await pending.Where(x=>x.Id==row.Id && x.SoLanThu==row.SoLanThu).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.TrangThai,"DANG_GUI")
                .SetProperty(x=>x.SoLanThu,attempt).SetProperty(x=>x.NgayGui,now).SetProperty(x=>x.NgayThuTiep,now.AddMinutes(2)),ct);
            if(claimed!=1)continue;
            var owned=scoped.Where(x=>x.Id==row.Id && x.SoLanThu==attempt && x.TrangThai=="DANG_GUI");
            var data=await Allowed(row,ct);
            if(data is null){await owned.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.TrangThai,"DA_HUY").SetProperty(x=>x.NgayThuTiep,(DateTime?)null)
                .SetProperty(x=>x.LoiGanNhat,"Thông báo không còn phù hợp hoặc người nhận không còn quyền truy cập."),ct);continue;}
            try
            {
                await sender.SendPaymentAsync(row.EmailNguoiNhan,data.Value.Notice.TieuDe+" · "+data.Value.Invoice,
                    data.Value.Notice.NoiDung+"\nThời điểm: "+data.Value.Notice.NgayTao.AddHours(7).ToString("dd/MM/yyyy HH:mm")+" (giờ Việt Nam)\nĐăng nhập để xem chi tiết và chứng từ.",data.Value.Notice.DuongDan);
                await owned.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.TrangThai,"DA_GUI").SetProperty(x=>x.NgayThuTiep,(DateTime?)null).SetProperty(x=>x.LoiGanNhat,(string?)null),ct);sent++;
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                var next=InvoiceNotificationDispatcher.GetNextRetry(attempt,clock.UtcNow);var state=attempt>=4?"THAT_BAI":"CHO_GUI";
                await owned.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.TrangThai,state).SetProperty(x=>x.NgayThuTiep,next)
                    .SetProperty(x=>x.LoiGanNhat,"Không gửi được email thanh toán. Kiểm tra cấu hình hoặc máy chủ gửi thư."),ct);
            }
        }
        return sent;
    }
}
