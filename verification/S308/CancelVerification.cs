using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

internal static class CancelVerification
{
    private sealed class Clock : ITimeProvider { public DateTime UtcNow { get; } = new(2026,10,9,2,0,0,DateTimeKind.Utc); }
    private static string Contents(HoaDon bill) => JsonSerializer.Serialize(new {
        bill.HopDongId,bill.Nam,bill.Thang,bill.TuNgay,bill.DenNgay,bill.NgayChot,bill.SoNguoiTinhPhi,bill.LoaiHoaDon,bill.TongTien,bill.GhiChu,
        Lines=bill.ChiTiet.OrderBy(x=>x.SoThuTu).Select(x=>new {x.SoThuTu,x.DichVuId,x.CauHinhDichVuId,x.KyHopDongId,
            x.ChiSoId,x.BaoHongId,x.SoNgayTinhTien,x.SoNgayTrongThang,x.LoaiKhoan,x.TenKhoan,x.CachTinhApDung,x.DonViTinh,
            x.SoLuong,x.DonGia,x.ChiSoDau,x.ChiSoCuoi,x.ThanhTien,x.GhiChu}) });

    public static async Task Run(Func<AppDbContext> context,string database,string root,Action<bool,string> check)
    {
        var clock=new Clock(); int id;
        using(var db=context()) id=await db.HoaDons.Where(x=>x.Thang==12 && x.Nam==2026).Select(x=>x.Id).SingleAsync();
        HoaDonDichVuService Service(AppDbContext db) => new(db,new(db),clock);
        async Task<HuyHoaDonViewModel> Input() {
            using var db=context();var invoice=await db.HoaDons.FindAsync(id);
            return new(){Id=id,PhienBan=invoice!.PhienBan,LyDo="Điều chỉnh chỉ số điện ghi sai",XacNhan=true};
        }
        async Task<string> Snapshot() {
            using var db=context();
            return JsonSerializer.Serialize(new {Bills=await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).OrderBy(x=>x.Id).ToListAsync(),
                Notices=await db.ThongBaoHoaDons.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
                Meters=await db.ChiSoDienNuocs.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
                AuditCount=await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM nhat_ky_hoat_dong").SingleAsync()});
        }
        async Task Reject(HuyHoaDonViewModel input,string name,int actor=1) {
            var before=await Snapshot();
            try {using var db=context();await Service(db).HuyAsync(actor,input);throw new Exception("Unexpected cancellation: "+name);}
            catch(Exception ex) when(ex is InvalidOperationException or UnauthorizedAccessException or DbUpdateException or SqliteException) { }
            check(before==await Snapshot(),"cancel rejects and preserves data: "+name);
        }
        var input=await Input();input.LyDo=" \r\n ";await Reject(input,"blank reason");
        input=await Input();input.LyDo=new string('a',1001);await Reject(input,"reason too long");
        input=await Input();input.XacNhan=false;await Reject(input,"missing confirmation");
        input=await Input();input.PhienBan--;await Reject(input,"stale version");
        await Reject(await Input(),"other owner",2);await Reject(await Input(),"manager",3);await Reject(await Input(),"tenant",4);
        input=await Input();using(var db=context())input.Id=await db.HoaDons.Where(x=>x.TrangThai=="NHAP").Select(x=>x.Id).FirstAsync();
        await Reject(input,"draft cannot cancel");
        var issuedSnapshot=await Snapshot();
        try {using var db=context();await db.Database.ExecuteSqlRawAsync("UPDATE hoa_don SET trang_thai='DA_HUY',ly_do_huy='Bypass',nguoi_huy_id=1,ngay_huy='2026-10-09',phien_ban=phien_ban+1,tong_tien=tong_tien+1 WHERE id={0}",id);throw new Exception("Cancellation gate allowed amount change");}
        catch(SqliteException){check(issuedSnapshot==await Snapshot(),"database cancellation gate forbids changing published amount");}
        try {using var db=context();await Service(db).TaoNhapThayTheAsync(1,id,(await Input()).PhienBan);throw new Exception("Replacement of issued invoice");}
        catch(InvalidOperationException){check(issuedSnapshot==await Snapshot(),"replacement requires original already cancelled");}

        using(var db=context())await db.Database.ExecuteSqlRawAsync("CREATE TABLE giao_dich_coc(id INTEGER PRIMARY KEY,hoa_don_id INTEGER,trang_thai TEXT)");
        foreach(var status in new[]{"DA_XAC_NHAN","CHO_XAC_NHAN"}) {
            using(var db=context())await db.Database.ExecuteSqlRawAsync("INSERT INTO thanh_toan(hoa_don_id,trang_thai,so_tien,ngay_thanh_toan) VALUES({0},{1},1,'2026-10-09')",id,status);
            await Reject(await Input(),"payment "+status);
            using(var db=context())await db.Database.ExecuteSqlRawAsync("UPDATE thanh_toan SET trang_thai='DA_HUY'");
        }
        using(var db=context())await db.Database.ExecuteSqlRawAsync("INSERT INTO giao_dich_coc(hoa_don_id,trang_thai) VALUES({0},'DA_XAC_NHAN')",id);
        await Reject(await Input(),"confirmed deposit offset");
        using(var db=context())await db.Database.ExecuteSqlRawAsync("UPDATE giao_dich_coc SET trang_thai='DA_HUY'");
        using(var db=context())await db.Database.ExecuteSqlRawAsync("UPDATE thong_bao SET trang_thai_email='DANG_GUI',khoa_xu_ly_den='2020-01-01' WHERE hoa_don_id={0}",id);
        await Reject(await Input(),"in-flight email even with expired lease");
        using(var db=context())await db.Database.ExecuteSqlRawAsync("UPDATE thong_bao SET trang_thai_email='CHO_GUI',khoa_xu_ly_den=NULL WHERE hoa_don_id={0}",id);
        using(var db=context())await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER test_cancel_audit_failure BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='hoa_don' BEGIN SELECT RAISE(ABORT,'Synthetic audit failure'); END");
        await Reject(await Input(),"audit failure rolls back cancelled notice");
        using(var db=context())await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_cancel_audit_failure");

        string oldContents,meterSnapshot;DateTime? issuedAt;string code;
        using(var db=context()) {
            var bill=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==id);oldContents=Contents(bill);issuedAt=bill.NgayPhatHanh;code=bill.MaHoaDon;
            meterSnapshot=JsonSerializer.Serialize(await db.ChiSoDienNuocs.AsNoTracking().OrderBy(x=>x.Id).ToListAsync());
        }
        input=await Input();using(var db=context())await Service(db).HuyAsync(1,input);
        int cancelledVersion;
        using(var db=context()) {
            var old=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==id);cancelledVersion=old.PhienBan;
            check(old.TrangThai=="DA_HUY" && old.LyDoHuy==input.LyDo && old.NgayHuy==clock.UtcNow && old.NguoiHuyId==1,"cancellation stores state, reason, actor and UTC time");
            check(oldContents==Contents(old) && old.NgayPhatHanh==issuedAt && old.MaHoaDon==code,"cancel keeps every original financial line and publication field");
            check((await db.ThongBaoHoaDons.SingleAsync(x=>x.HoaDonId==id)).TrangThaiEmail=="DA_HUY","queued email suppressed after cancellation");
            check(meterSnapshot==JsonSerializer.Serialize(await db.ChiSoDienNuocs.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()),"cancel leaves source meter rows unchanged and locked");
        }
        await Reject(await Input(),"repeated cancellation");
        foreach(var sql in new[]{"UPDATE hoa_don SET tong_tien=tong_tien+1 WHERE id={0}","UPDATE hoa_don SET ly_do_huy='Overwritten' WHERE id={0}",
            "UPDATE chi_tiet_hoa_don SET don_gia=1 WHERE hoa_don_id={0}","DELETE FROM chi_tiet_hoa_don WHERE hoa_don_id={0}","DELETE FROM hoa_don WHERE id={0}"}) {
            try {using var db=context();await db.Database.ExecuteSqlRawAsync(sql,id);throw new Exception("Mutable cancelled invoice");}
            catch(SqliteException){check(true,"database rejects cancelled content change: "+sql.Split(' ')[0]);}
        }
        var unchanged=await Snapshot();
        using(var db=context())await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER test_replacement_failure BEFORE INSERT ON chi_tiet_hoa_don BEGIN SELECT RAISE(ABORT,'Synthetic line failure'); END");
        try {using var db=context();await Service(db).TaoNhapThayTheAsync(1,id,cancelledVersion);throw new Exception("Unexpected replacement");}
        catch(DbUpdateException){check(unchanged==await Snapshot(),"replacement line failure rolls back invoice and audit");}
        using(var db=context())await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_replacement_failure");
        var ids=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(async()=>{using var db=context();return await Service(db).TaoNhapThayTheAsync(1,id,cancelledVersion);})));var replacementId=ids[0];
        check(ids[0]==ids[1],"concurrent replacement requests return same draft");
        using(var db=context()) {
            var bill=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==replacementId);
            check(bill.TrangThai=="NHAP" && bill.ThayTheHoaDonId==id && bill.MaHoaDon!=code && bill.NgayPhatHanh==null,"replacement starts draft with new code and original link");
            check(oldContents==Contents(bill),"replacement clones snapshots, notes and reserved reference fields");
            check(!await db.ThongBaoHoaDons.AnyAsync(x=>x.HoaDonId==replacementId),"replacement draft creates no notification");
            var beforeOld=JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==id));
            var expectedTotal=bill.TongTien+1000+bill.ChiTiet.Where(x=>x.ChiSoDau.HasValue).Sum(x=>x.DonGia);
            await Service(db).LuuNhapAsync(1,new(){Id=replacementId,PhienBan=bill.PhienBan,
                ChiSo=bill.ChiTiet.Where(x=>x.ChiSoDau.HasValue).Select(x=>new ChiSoNhapInput{Id=x.Id,ChiSoDau=x.ChiSoDau!.Value,ChiSoCuoi=x.ChiSoCuoi!.Value+1}).ToList(),
                Khoan=[new(){LoaiKhoan="PHAT_SINH",TenKhoan="Replacement adjustment",SoTien=1000,GhiChu="Correction"}]});
            check((await db.HoaDons.AsNoTracking().SingleAsync(x=>x.Id==replacementId)).TongTien==expectedTotal,"replacement draft recalculates exact meter and adjustment total");
            check(beforeOld==JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==id)),"editing replacement preserves cancelled original");
        }
        using(var db=context()) {
            var bill=await db.HoaDons.FindAsync(replacementId);
            await Service(db).PhatHanhNhapAsync(1,new(){Id=replacementId,PhienBan=bill!.PhienBan,XacNhan=true,NgayPhatHanh=new(2026,10,9),HanThanhToan=new(2026,10,16)});
            check(bill.TrangThai=="DA_PHAT_HANH" && await db.ThongBaoHoaDons.CountAsync(x=>x.HoaDonId==replacementId)==1,"replacement publishes with locked source meters and new notification");
            check(meterSnapshot==JsonSerializer.Serialize(await db.ChiSoDienNuocs.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()),"replacement publication never rewrites source meters");
            check(await db.HoaDons.CountAsync(x=>x.HopDongId==bill.HopDongId && x.Nam==bill.Nam && x.Thang==bill.Thang && x.TrangThai!="DA_HUY")==1,"only one active invoice remains for contract period");
        }
        using(var db=context())await db.Database.ExecuteSqlRawAsync("DELETE FROM thanh_toan;DROP TABLE giao_dich_coc");
        // Simulate v21 on an isolated copy, restoring the exact old immutable trigger.
        var copy=database+".v21-copy.sqlite";
        using(var source=new SqliteConnection("Data Source="+database)){source.Open();using var target=new SqliteConnection("Data Source="+copy);target.Open();source.BackupDatabase(target);}
        QL_PhongTro.Verification.LegacySchemaFixture.RemoveRoomExtensions(copy);
        using(var c=new SqliteConnection("Data Source="+copy)){c.Open();using var cmd=c.CreateCommand();cmd.CommandText="DELETE FROM app_schema_version WHERE version>=22;DROP TRIGGER khoa_hoa_don_update;DROP TRIGGER hoa_don_thay_the_insert;DROP TRIGGER hoa_don_thay_the_update;CREATE TRIGGER khoa_hoa_don_update BEFORE UPDATE ON hoa_don WHEN OLD.trang_thai<>'NHAP' BEGIN SELECT RAISE(ABORT,'Issued invoice is immutable'); END";cmd.ExecuteNonQuery();}
        async Task<string> CopySnapshot(){using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source="+copy).Options);return JsonSerializer.Serialize(new {Bills=await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).OrderBy(x=>x.Id).ToListAsync(),Notices=await db.ThongBaoHoaDons.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()});}
        var beforeCopy=await CopySnapshot();var seed=Path.Combine(root,"QL_PhongTro/Data/permissions.seed.json");
        DatabaseUpdates.Update(copy,seed);DatabaseUpdates.Check(copy);check(beforeCopy==await CopySnapshot(),"v21 to v22 preserves all invoice and notification rows");
        DatabaseUpdates.Update(copy,seed);check(beforeCopy==await CopySnapshot(),"v22 rerun preserves data");
        using(var db=context()) {
            check(await db.Database.SqlQueryRaw<string>("SELECT integrity_check AS Value FROM pragma_integrity_check").SingleAsync()=="ok","cancel/reissue database integrity");
            check(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_foreign_key_check").SingleAsync()==0,"cancel/reissue foreign keys");
        }
    }
}
