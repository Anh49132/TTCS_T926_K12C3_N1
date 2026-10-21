using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

internal static class HistoryVerification
{
    public static async Task Run(Func<AppDbContext> context, Action<bool,string> check)
    {
        int id;
        using(var db=context()) {
            var bill=new HoaDon { MaHoaDon="HD-HISTORY",HopDongId=1,Nam=2028,Thang=10,TuNgay=new(2028,10,1),DenNgay=new(2028,10,31),
                NgayChot=new(2028,10,31),HanThanhToan=new(2028,11,7),NguoiLapId=1,NgayLap=DateTime.UtcNow,TongTien=100,SoNguoiTinhPhi=1,
                ChiTiet=[new ChiTietHoaDon { SoThuTu=1,TenKhoan="Điện test",CachTinhApDung="THEO_CHI_SO",ChiSoDau=10,ChiSoCuoi=20,SoLuong=10,DonGia=10,ThanhTien=100 }] };
            db.HoaDons.Add(bill);await db.SaveChangesAsync();id=bill.Id;
        }
        async Task<SuaHoaDonNhapViewModel> Input() {
            using var db=context();var b=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==id);
            var m=b.ChiTiet.Single(x=>x.CachTinhApDung=="THEO_CHI_SO");
            return new(){Id=id,PhienBan=b.PhienBan,ChiSo=[new(){Id=m.Id,ChiSoDau=m.ChiSoDau!.Value,ChiSoCuoi=m.ChiSoCuoi!.Value}]};
        }
        async Task Save(SuaHoaDonNhapViewModel input) { using var db=context();await new HoaDonDichVuService(db,new DichVuService(db)).LuuNhapAsync(1,input); }
        async Task<IReadOnlyList<LichSuHoaDonNhapViewModel>> History() { using var db=context();return await new HoaDonDichVuService(db,new DichVuService(db)).LichSuNhapAsync(1,id); }
        await Save(await Input());check((await History()).Count==0,"history unchanged save creates no entry");
        var edit=await Input();edit.ChiSo[0].ChiSoCuoi=30;await Save(edit);
        var rows=await History();check(rows.Count==1 && rows[0].Truoc!.Dong[0].ChiSoCuoi==20 && rows[0].Sau!.Dong[0].ChiSoCuoi==30
            && rows[0].Truoc!.TongTien==100 && rows[0].Sau!.TongTien==200,"history before after readings and totals");
        check(rows[0].NguoiSua=="Owner" && Math.Abs((DateTime.UtcNow-rows[0].ThoiDiem).TotalSeconds)<20,"history saved actor name and UTC time");
        edit=await Input();edit.Khoan=[new(){LoaiKhoan="PHAT_SINH",TenKhoan="Phí test",SoTien=50,GhiChu="<script>test</script>"},new(){LoaiKhoan="GIAM_TRU",TenKhoan="Giảm test",SoTien=20,GhiChu="Lý do giảm"}];await Save(edit);
        rows=await History();check(rows.Count==2 && rows[0].Id>rows[1].Id && rows[0].Truoc!.Dong.Count==1 && rows[0].Sau!.Dong.Count==3
            && rows[0].Sau!.TongTien==230 && rows[0].Sau!.Dong[1].GhiChu=="<script>test</script>" && rows[0].Sau!.Dong[2].GhiChu=="Lý do giảm","history additions reductions notes one entry per save newest first");
        using(var db=context())foreach(var actor in new[]{2,3,4,5}) {
            try { await new HoaDonDichVuService(db,new DichVuService(db)).LichSuNhapAsync(actor,id);throw new Exception("History permission bypass"); }
            catch(UnauthorizedAccessException){check(true,"history denies other owner manager tenant admin "+actor);}
        }
        var before=JsonSerializer.Serialize(rows);
        edit=await Input();edit.PhienBan--;try{await Save(edit);throw new Exception("stale saved");}catch(InvalidOperationException){}
        check(before==JsonSerializer.Serialize(await History()),"history stale rejected save creates no entry");
        using(var db=context()) await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER history_test_failure BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.hanh_dong='SUA_NHAP' BEGIN SELECT RAISE(ABORT,'Synthetic history failure'); END");
        edit=await Input();var version=edit.PhienBan;edit.ChiSo[0].ChiSoCuoi=40;
        try{await Save(edit);throw new Exception("history failure ignored");}catch(Microsoft.Data.Sqlite.SqliteException){}
        using(var db=context()) {check((await db.HoaDons.FindAsync(id))!.TongTien==230 && (await db.HoaDons.FindAsync(id))!.PhienBan==version
            && before==JsonSerializer.Serialize(await History()),"history insert failure rolls back amounts version and audit");
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER history_test_failure");}
        var same=await Input();same.ChiSo[0].ChiSoCuoi=40;
        async Task<bool> Race(){try{await Save(same);return true;}catch(InvalidOperationException){return false;}}
        var races=await Task.WhenAll(Race(),Race());check(races.Count(x=>x)==1 && (await History()).Count==3,"history concurrent saves one winner one record");
        using(var db=context()) {
            try{await db.Database.ExecuteSqlRawAsync("UPDATE nhat_ky_hoat_dong SET du_lieu_sau='tampered' WHERE hanh_dong='SUA_NHAP'");throw new Exception("audit editable");}catch(Microsoft.Data.Sqlite.SqliteException){check(true,"history SQL update blocked");}
            try{await db.Database.ExecuteSqlRawAsync("DELETE FROM nhat_ky_hoat_dong WHERE hanh_dong='SUA_NHAP'");throw new Exception("audit deletable");}catch(Microsoft.Data.Sqlite.SqliteException){check(true,"history SQL delete blocked");}
        }
    }
}
