using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
namespace QL_PhongTro.Services;

public static class RepairEmailQueue
{
    public static async Task<bool> InstalledAsync(AppDbContext db,CancellationToken ct=default)=>
        await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='email_bao_hong'").SingleAsync(ct)==1;

    // Called inside the business transaction; never sends SMTP while holding a database writer.
    public static async Task EnqueueAsync(AppDbContext db,BaoHong report,string eventKey,int actor,
        string? status,DateTime now,CancellationToken ct)
    {
        if(!await InstalledAsync(db,ct))throw new InvalidOperationException("Cần nâng schema v41 trước khi gửi thông báo báo hỏng.");
        var room=await db.PhongTros.AsNoTracking().SingleAsync(x=>x.Id==report.PhongId,ct);
        var building=await db.ToaNhas.AsNoTracking().SingleAsync(x=>x.Id==room.ToaNhaId,ct);
        var fresh=eventKey.StartsWith("MOI:",StringComparison.Ordinal);
        var ids=fresh ? new[]{building.ChuNhaId,building.QuanLyId??0} : new[]{report.NguoiBaoId,building.ChuNhaId};
        var recipients=await db.TaiKhoans.AsNoTracking().Where(x=>ids.Contains(x.Id) && x.Id!=actor && x.DangHoatDong
            && !x.IsDeleted && x.EmailConfirmed && !x.MustChangePassword).ToListAsync(ct);
        foreach(var recipient in recipients)
        {
            var valid=recipient.VaiTro switch {
                "CHU_NHA"=>recipient.Id==building.ChuNhaId,
                "QUAN_LY"=>fresh && recipient.Id==building.QuanLyId,
                "KHACH_THUE"=>!fresh && recipient.Id==report.NguoiBaoId,
                _=>false };
            if(!valid)continue;
            var controller=recipient.VaiTro=="KHACH_THUE"?"BaoHong":recipient.VaiTro=="QUAN_LY"?"QuanLyBaoHong":"ChuNhaBaoHong";
            var path=$"/{controller}/Details/{report.Id}";
            var subject=$"{(report.MucDo=="GAP"?"[Gấp] ":"")}{(fresh?"Báo hỏng mới":"Cập nhật báo hỏng")} {report.MaBaoHong} · Phòng {room.MaPhong}";
            var content=$"Xin chào {recipient.HoTen},\n\n{(fresh?"Có báo hỏng mới cần kiểm tra.":status is null?"Có phản hồi mới từ quản lý.":"Trạng thái báo hỏng đã thay đổi.")}\nMã báo hỏng: {report.MaBaoHong}\nPhòng: {room.MaPhong} · {building.TenToaNha}\nThiết bị: {report.LoaiThietBi}\nMức độ: {(report.MucDo=="GAP"?"Gấp":"Thường")}\nTrạng thái: {BaoHongService.TenTrangThai(status??report.TrangThai)}\nThời điểm: {now.AddHours(7):dd/MM/yyyy HH:mm} (giờ Việt Nam)\n\nĐăng nhập để xem nội dung, phản hồi và ảnh đính kèm.";
            var created=now.ToString("O");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO email_bao_hong(bao_hong_id,su_kien,nguoi_nhan_id,vai_tro_nhan,email_nhan,tieu_de,noi_dung,duong_dan,ngay_tao) VALUES({report.Id},{eventKey},{recipient.Id},{recipient.VaiTro},{recipient.Email},{subject},{content},{path},{created}) ON CONFLICT(su_kien,nguoi_nhan_id) DO NOTHING",ct);
        }
    }
}
