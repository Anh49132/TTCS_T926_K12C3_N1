using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
namespace QL_PhongTro.Services;

public sealed class RepairEmailRow
{
    public int Id {get;set;}
    public int ReportId {get;set;}
    public string Event {get;set;}="";
    public int Recipient {get;set;}
    public string Role {get;set;}="";
    public string Email {get;set;}="";
    public string Subject {get;set;}="";
    public string Body {get;set;}="";
    public string Path {get;set;}="";
    public string Status {get;set;}="";
    public int Attempts {get;set;}
    public string Created {get;set;}="";
    public string? Next {get;set;}
    public string? Error {get;set;}
}

public sealed class RepairEmailDispatcher(AppDbContext db,IRepairEmailSender sender,ITimeProvider clock)
{
    public const string SelectSql="""
        SELECT id AS Id,bao_hong_id AS ReportId,su_kien AS Event,nguoi_nhan_id AS Recipient,vai_tro_nhan AS Role,
        email_nhan AS Email,tieu_de AS Subject,noi_dung AS Body,duong_dan AS Path,trang_thai AS Status,
        so_lan_gui AS Attempts,ngay_tao AS Created,lan_gui_tiep AS Next,loi AS Error FROM email_bao_hong
        """;
    private async Task<bool> StillAllowed(RepairEmailRow row,CancellationToken ct)=>
        await (from a in db.TaiKhoans.AsNoTracking()
               join r in db.BaoHongs on row.ReportId equals r.Id
               join p in db.PhongTros on r.PhongId equals p.Id
               join b in db.ToaNhas on p.ToaNhaId equals b.Id
               where a.Id==row.Recipient && a.Email==row.Email && a.VaiTro==row.Role && a.DangHoatDong
                   && a.EmailConfirmed && !a.IsDeleted && !a.MustChangePassword
                   && db.RolePermissions.Any(x=>x.RoleCode==a.VaiTro && x.ModuleCode=="BAO_HONG" && (x.AccessLevel=="READ" || x.AccessLevel=="WRITE" || x.AccessLevel=="FULL"))
                   && (row.Role=="CHU_NHA" && b.ChuNhaId==a.Id || row.Role=="QUAN_LY" && b.QuanLyId==a.Id || row.Role=="KHACH_THUE" && r.NguoiBaoId==a.Id)
               select a.Id).AnyAsync(ct);
    public async Task<int> DispatchAsync(CancellationToken ct=default)
    {
        if(!await RepairEmailQueue.InstalledAsync(db,ct))return 0;
        var now=clock.UtcNow.ToString("O");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE email_bao_hong SET trang_thai='THAT_BAI',khoa_xu_ly=NULL,khoa_den=NULL,loi='Hết số lần thử gửi email báo hỏng.' WHERE trang_thai='DANG_GUI' AND khoa_den<={now} AND so_lan_gui>=4",ct);
        var rows=await db.Database.SqlQueryRaw<RepairEmailRow>(SelectSql+" WHERE so_lan_gui<4 AND ((trang_thai='CHO_GUI' AND (lan_gui_tiep IS NULL OR lan_gui_tiep<={0})) OR (trang_thai='DANG_GUI' AND khoa_den<={0})) ORDER BY id LIMIT 10",now).ToListAsync(ct);
        var sent=0;
        foreach(var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var lease=Guid.NewGuid().ToString("N");var until=clock.UtcNow.AddMinutes(2).ToString("O");
            var claimed=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE email_bao_hong SET trang_thai='DANG_GUI',so_lan_gui=so_lan_gui+1,khoa_xu_ly={lease},khoa_den={until} WHERE id={row.Id} AND so_lan_gui<4 AND ((trang_thai='CHO_GUI' AND (lan_gui_tiep IS NULL OR lan_gui_tiep<={now})) OR (trang_thai='DANG_GUI' AND khoa_den<={now}))",ct);
            if(claimed!=1)continue;
            if(!await StillAllowed(row,ct))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE email_bao_hong SET trang_thai='DA_HUY',khoa_xu_ly=NULL,khoa_den=NULL,loi='Người nhận không còn đủ điều kiện nhận thông báo.' WHERE id={row.Id} AND khoa_xu_ly={lease}",ct);
                continue;
            }
            try
            {
                await sender.SendRepairAsync(row.Email,row.Subject,row.Body,row.Path);
                var done=clock.UtcNow.ToString("O");
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE email_bao_hong SET trang_thai='DA_GUI',ngay_gui={done},lan_gui_tiep=NULL,khoa_xu_ly=NULL,khoa_den=NULL,loi=NULL WHERE id={row.Id} AND khoa_xu_ly={lease}",ct);sent++;
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                var attempts=row.Attempts+1;var state=attempts>=4?"THAT_BAI":"CHO_GUI";
                var next=InvoiceNotificationDispatcher.GetNextRetry(attempts,clock.UtcNow)?.ToString("O");
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE email_bao_hong SET trang_thai={state},lan_gui_tiep={next},khoa_xu_ly=NULL,khoa_den=NULL,loi='Không gửi được email báo hỏng. Kiểm tra cấu hình hoặc máy chủ gửi thư.' WHERE id={row.Id} AND khoa_xu_ly={lease}",ct);
            }
        }
        return sent;
    }
}
