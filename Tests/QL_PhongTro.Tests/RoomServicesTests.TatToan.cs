using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private static MockTimeProvider TatToanClock()=>new() { UtcNow=new(2026,10,14,2,0,0,DateTimeKind.Utc) };
    private async Task<(int Contract,int Final,int Previous)> TatToanFixture(AppDbContext db,long deposit=5000000)
    {
        var f=await BillingAsync(db);
        await db.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1; UPDATE phong_tro SET trang_thai='DANG_THUE'");
        async Task<HoaDon> Bill(int month,long amount,bool final)
        {
            var bill=new HoaDon {MaHoaDon="TT-TEST-"+month,HopDongId=f.A,Nam=2026,Thang=month,TuNgay=new(2026,month,1),DenNgay=new(2026,month,final?13:30),NgayChot=new(2026,month,final?13:30),LoaiHoaDon=final?"KY_CUOI":"DINH_KY",SoNguoiTinhPhi=1,TongTien=amount,HanThanhToan=new(2026,10,20),NgayLap=TatToanClock().UtcNow,NguoiLapId=1,
                ChiTiet=[new() {SoThuTu=1,LoaiKhoan="TIEN_PHONG",TenKhoan="Tiền phòng",DonGia=amount,SoLuong=1,ThanhTien=amount}]};
            db.HoaDons.Add(bill);await db.SaveChangesAsync();
            if(!final){bill.TrangThai="DA_PHAT_HANH";bill.NgayPhatHanh=TatToanClock().UtcNow;await db.SaveChangesAsync();}return bill;
        }
        var old=await Bill(9,1500000,false);var final=await Bill(10,2000000,true);
        db.ThanhToans.Add(new() {HoaDonId=old.Id,SoTien=500000,NgayThanhToan=new(2026,10,10),TrangThai="DA_XAC_NHAN"});
        if(deposit>0)db.GiaoDichCocs.Add(new() {Ma="COC-TEST",KhoaChongTrung=Guid.NewGuid().ToString("N"),HopDongId=f.A,SoTien=deposit,HinhThuc="TIEN_MAT",NguoiThucHienId=1,NgayGiaoDich=TatToanClock().UtcNow});
        await db.SaveChangesAsync();return(f.A,final.Id,old.Id);
    }
    private static TatToanInput TatToanFields(TatToanViewModel model)=>new() {HopDongId=model.Input.HopDongId,PhienBan=model.Input.PhienBan,DauVanTayNguon=model.Input.DauVanTayNguon,LyDo="KHACH_CHUYEN_DI",
        KhauTru=[new() {TenKhoan="Thay khóa cửa",SoTien=250000,GhiChu="Mất chìa khóa"},new() {TenKhoan="Sơn tường",SoTien=200000}]};

    [Theory]
    [InlineData(7000000,2000000,1000000,450000,3550000,0)]
    [InlineData(3000000,2000000,1000000,0,0,0)]
    [InlineData(2000000,2000000,1000000,0,0,1000000)]
    [InlineData(0,2000000,0,0,0,2000000)]
    [InlineData(5000000,0,0,0,5000000,0)]
    [InlineData(100,10,20,70,0,0)]
    [InlineData(100,10,20,69,1,0)]
    [InlineData(100,10,20,71,0,1)]
    [InlineData(0,0,0,0,0,0)]
    [InlineData(2000000,1500000,400000,50000,50000,0)]
    public void TatToanMoneyExamples(long coc,long last,long old,long damage,long refund,long extra)
    {
        var bang=new BangTatToan {CocConGiu=coc,NoHoaDonCuoi=last,CongNoCu=old};
        TatToanService.ApDungKhauTru(bang,new() {KhauTru=damage==0?[]:[new() {TenKhoan="Hư hỏng",SoTien=damage}]});
        Assert.Equal(refund,bang.TienPhaiHoan);Assert.Equal(extra,bang.TienKhachTraThem);
    }
    [Theory]
    [InlineData("",1)] [InlineData("   ",1)] [InlineData("Khóa cửa",-0.01)] [InlineData("Khóa cửa",-1)]
    public void TatToanRejectsInvalidDamage(string name,decimal amount)=>Assert.Throws<InvalidOperationException>(()=>TatToanService.ApDungKhauTru(new(),new() {KhauTru=[new() {TenKhoan=name,SoTien=amount}]}));
    [Fact]
    public void TatToanRoundsEachLineAndAllowsZero()
    {
        var b=new BangTatToan {CocConGiu=10};TatToanService.ApDungKhauTru(b,new() {KhauTru=[new() {TenKhoan="A",SoTien=0.5m},new() {TenKhoan="B",SoTien=0.5m},new() {TenKhoan="C",SoTien=0}]});
        Assert.Equal(2,b.TienHuHong);Assert.Equal(8,b.TienPhaiHoan);
    }
    [Fact]
    public async Task TatToanSavesEditsDeletesDeductionsAndSharesIdenticalSnapshot()
    {
        using var db=Context();var f=await TatToanFixture(db);var service=new TatToanService(db,TatToanClock());
        var model=await service.NhapAsync(1,f.Contract);var input=TatToanFields(model);
        await service.LuuAsync(1,input);
        var owner=await service.XemAsync(1,f.Contract);var tenant=await service.XemAsync(3,f.Contract);
        Assert.Equal(1550000,owner.Bang.TienPhaiHoan);Assert.Equal(owner.Bang.ChiTietChot,tenant.Bang.ChiTietChot);Assert.Equal(owner.KetQua,tenant.KetQua);Assert.False(tenant.DuocSua);
        Assert.Equal(2,owner.Bang.KhauTru.Count);Assert.Equal("DANG_HIEU_LUC",(await db.HopDongs.AsNoTracking().SingleAsync(x=>x.Id==f.Contract)).TrangThai);
        var edit=await service.NhapAsync(1,f.Contract);edit.Input.KhauTru.RemoveAt(0);edit.Input.KhauTru[0].TenKhoan="Sơn lại";edit.Input.KhauTru[0].SoTien=0;
        await service.LuuAsync(1,edit.Input);var updated=await service.XemAsync(3,f.Contract);
        Assert.Equal(2000000,updated.Bang.TienPhaiHoan);Assert.Equal("Sơn lại",Assert.Single(updated.Bang.KhauTru).TenKhoan);
        edit=await service.NhapAsync(1,f.Contract);edit.Input.KhauTru.Clear();await service.LuuAsync(1,edit.Input);
        Assert.Empty((await service.XemAsync(3,f.Contract)).Bang.KhauTru);
        Assert.Equal(2000000,(await db.HoaDons.AsNoTracking().SingleAsync(x=>x.Id==f.Final)).TongTien); // Draft damage is not charged twice.
    }
    [Fact]
    public async Task TatToanCountsConfirmedPaymentsOffsetsAndNetDepositOnly()
    {
        using var db=Context();var f=await TatToanFixture(db);
        db.ThanhToans.AddRange(new ThanhToanHoaDon {HoaDonId=f.Final,SoTien=100000,NgayThanhToan=new(2026,10,13),TrangThai="DA_XAC_NHAN"},new ThanhToanHoaDon {HoaDonId=f.Previous,SoTien=700000,NgayThanhToan=new(2026,10,13),TrangThai="CHO_XAC_NHAN"},new ThanhToanHoaDon {HoaDonId=f.Previous,SoTien=200000,NgayThanhToan=new(2026,10,13),TrangThai="DA_HUY"});
        db.GiaoDichCocs.AddRange(new GiaoDichCoc {Ma="REFUND",KhoaChongTrung="REFUND",HopDongId=f.Contract,Loai="HOAN_COC",SoTien=300000,HinhThuc="TIEN_MAT",NguoiThucHienId=1,NgayGiaoDich=TatToanClock().UtcNow},new GiaoDichCoc {Ma="OFFSET",KhoaChongTrung="OFFSET",HopDongId=f.Contract,HoaDonId=f.Previous,Loai="CAN_TRU",SoTien=200000,NguoiThucHienId=1,NgayGiaoDich=TatToanClock().UtcNow},new GiaoDichCoc {Ma="CANCELLED",KhoaChongTrung="CANCELLED",HopDongId=f.Contract,SoTien=900000,TrangThai="DA_HUY",HinhThuc="TIEN_MAT",NguoiThucHienId=1,NgayGiaoDich=TatToanClock().UtcNow});
        await db.SaveChangesAsync();var model=await new TatToanService(db,TatToanClock()).NhapAsync(1,f.Contract);
        Assert.Equal(4500000,model.Bang.CocConGiu);Assert.Equal(1900000,model.Bang.NoHoaDonCuoi);Assert.Equal(800000,model.Bang.CongNoCu);Assert.Equal(1800000,model.Bang.TienPhaiHoan);
    }
    [Fact]
    public async Task TatToanDoesNotTreatAgreedDepositAsReceivedOrDraftAsDebt()
    {
        using var db=Context();var f=await TatToanFixture(db,0);
        await db.Database.ExecuteSqlRawAsync("UPDATE hop_dong SET tien_coc_thoa_thuan=9000000");
        // A prior draft and a cancelled invoice are not debt.
        foreach(var month in new[] {7,8})db.HoaDons.Add(new() {MaHoaDon="EXCLUDE-"+month,HopDongId=f.Contract,Nam=2026,Thang=month,TuNgay=new(2026,month,1),DenNgay=new(2026,month,28),NgayChot=new(2026,month,28),SoNguoiTinhPhi=1,TongTien=999999,NguoiLapId=1,NgayLap=TatToanClock().UtcNow,HanThanhToan=new(2026,10,20),TrangThai=month==7?"NHAP":"DA_HUY"});
        await db.SaveChangesAsync();var model=await new TatToanService(db,TatToanClock()).NhapAsync(1,f.Contract);
        Assert.Equal(0,model.Bang.CocConGiu);Assert.Equal(1000000,model.Bang.CongNoCu);Assert.Equal(3000000,model.Bang.TienKhachTraThem);
    }
    [Fact]
    public async Task TatToanRejectsForeignViewTenantEditStaleSourceAndVersion()
    {
        using var db=Context();var f=await TatToanFixture(db);var service=new TatToanService(db,TatToanClock());var input=TatToanFields(await service.NhapAsync(1,f.Contract));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.NhapAsync(2,f.Contract));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.NhapAsync(3,f.Contract));
        db.ThanhToans.Add(new() {HoaDonId=f.Final,SoTien=1,NgayThanhToan=new(2026,10,13),TrangThai="DA_XAC_NHAN"});await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuAsync(1,input));
        input=TatToanFields(await service.NhapAsync(1,f.Contract));await service.LuuAsync(1,input);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuAsync(1,input));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.XemAsync(2,f.Contract));
        Assert.Single(await db.BangTatToans.ToListAsync());
    }
    [Fact]
    public async Task TatToanAuditFailureRollsBackCreationAndEdit()
    {
        using var db=Context();var f=await TatToanFixture(db);var service=new TatToanService(db,TatToanClock());var input=TatToanFields(await service.NhapAsync(1,f.Contract));
        await service.LuuAsync(1,input);var edit=await service.NhapAsync(1,f.Contract);edit.Input.KhauTru.Clear();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_settlement_audit BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END");
        await Assert.ThrowsAnyAsync<Exception>(()=>service.LuuAsync(1,edit.Input));
        using var check=Context();Assert.Equal(2,await check.KhauTruTatToans.CountAsync());Assert.Equal(1,(await check.BangTatToans.SingleAsync()).PhienBan);
    }
    [Fact]
    public async Task TatToanHttpSharedReadOnlyTenantCsrfAndCrossOwner()
    {
        using var db=Context();var f=await TatToanFixture(db);using var factory=BillingWeb(TatToanClock());using var client=factory.CreateClient(new() {AllowAutoRedirect=false});await Login(client,"owner");
        var html=await client.GetStringAsync($"/TatToan/Nhap/{f.Contract}");Assert.Contains("Khấu trừ hư hỏng",html);
        string Field(string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var form=new Dictionary<string,string> { ["Input.HopDongId"]=f.Contract.ToString(),["Input.PhienBan"]="0",["Input.LyDo"]="KHACH_CHUYEN_DI",["Input.DauVanTayNguon"]=Field("Input.DauVanTayNguon"),["Input.KhauTru[0].TenKhoan"]="Thay khóa",["Input.KhauTru[0].SoTien"]="250000" };
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/TatToan/Luu",new FormUrlEncodedContent(form))).StatusCode);
        form["__RequestVerificationToken"]=Field("__RequestVerificationToken");Assert.NotEmpty(form["Input.DauVanTayNguon"]);
        var estimate=await client.PostAsync("/TatToan/UocTinh",new FormUrlEncodedContent(form));Assert.Equal(HttpStatusCode.OK,estimate.StatusCode);
        using(var json=JsonDocument.Parse(await estimate.Content.ReadAsStringAsync()))Assert.Equal("1750000",json.RootElement.GetProperty("hoan").GetString());
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/TatToan/Luu",new FormUrlEncodedContent(form))).StatusCode);
        var owner=await client.GetStringAsync($"/TatToan/ChiTiet/{f.Contract}");Assert.Contains("1.750.000",owner);Assert.Contains("Sửa bảng nháp",owner);
        await Login(client,"tenant");var tenant=await client.GetStringAsync($"/TatToan/ChiTiet/{f.Contract}");Assert.Contains("1.750.000",tenant);Assert.DoesNotContain("Sửa bảng nháp",tenant);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/TatToan/Nhap/{f.Contract}")).StatusCode);
        await Login(client,"other");Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/TatToan/ChiTiet/{f.Contract}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/TatToan/Nhap/{f.Contract}")).StatusCode);
    }
    [Theory]
    [InlineData("HOP_DONG")][InlineData("TAI_CHINH")]
    public async Task TatToanRequiresBothModuleWritePermissions(string module)
    {
        using var db=Context();var f=await TatToanFixture(db);await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='CHU_NHA' AND ModuleCode={module}");
        Assert.Equal("READ",(await db.RolePermissions.AsNoTracking().SingleAsync(x=>x.RoleCode=="CHU_NHA" && x.ModuleCode==module)).AccessLevel);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new TatToanService(db,TatToanClock()).NhapAsync(1,f.Contract));
    }
    [Fact]
    public async Task TatToanRestoredTaskOneConnectsToSettlement()
    {
        using var db=Context();var f=await MonthlyFixture(db);await db.Database.ExecuteSqlRawAsync("UPDATE chi_so_dien_nuoc SET tu_ngay='2026-09-01',den_ngay='2026-09-30'; UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1");db.ChangeTracker.Clear();
        var service=new TraPhongService(db,TatToanClock());var input=new TraPhongInput {LyDo="KHACH_CHUYEN_DI",HopDongId=f.A,NgayTra=new(2026,10,13),ChiSoDien=224,ChiSoNuoc=30};
        input.DauVanTay=TraPhongService.DauVanTay(await service.NhapAsync(1,input,true));var bill=await service.LuuAsync(1,input);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuAsync(1,input));
        var settlement=new TatToanService(db,TatToanClock());var draft=await settlement.NhapAsync(1,f.A);Assert.Equal(795355,draft.Bang.NoHoaDonCuoi);await settlement.LuuAsync(1,draft.Input);
        Assert.Equal(795355,(await settlement.XemAsync(3,f.A)).Bang.TienKhachTraThem);
        using var factory=BillingWeb(TatToanClock());using var client=factory.CreateClient(new() {AllowAutoRedirect=false});await Login(client,"owner");Assert.Contains("Tiếp tục lập bảng tất toán",await client.GetStringAsync($"/TraPhong/ChiTiet/{bill}"));
    }
    [Theory]
    [InlineData(31,13,3500000,1467742)][InlineData(30,15,3500000,1750000)][InlineData(28,14,3500000,1750000)][InlineData(29,15,3500000,1810345)]
    [InlineData(31,1,500000,16129)][InlineData(30,1,500000,16667)][InlineData(30,1,500025,16668)][InlineData(28,28,500000,500000)]
    [InlineData(29,29,500000,500000)][InlineData(31,31,3500000,3500000)]
    public void TatToanRestoredRentExamples(int monthDays,int days,long price,long expected)=>Assert.Equal(expected,TraPhongService.TienPhongTheoNgay(price,days,monthDays));

    [Fact]
    public async Task TatToanV31UpgradesCopyPreservesAllOldRowsAndRerunsSafely()
    {
        using(var db=Context())await TatToanFixture(db);
        SchemaTestFixture.RemoveXacNhanExtensions(path);
        // A synthetic v30 source has no deposit/settlement tables. Never touch the user's DB.
        using(var c=Open())
        {
            using var cmd=c.CreateCommand();cmd.CommandText="DROP TABLE cham_dut_hop_dong; DROP TABLE chi_tiet_khau_tru; DROP TABLE thanh_ly_hop_dong; DROP TABLE giao_dich_coc; DELETE FROM app_schema_version WHERE version>=31";cmd.ExecuteNonQuery();
        }
        var target=Path.Combine(folder,"v31-upgrade-copy.sqlite");
        using(var source=Open())using(var copy=new SqliteConnection($"Data Source={target};Pooling=False")){copy.Open();source.BackupDatabase(copy);}
        List<string> Tables(string file)
        {
            using var c=new SqliteConnection($"Data Source={file};Pooling=False");c.Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name<>'app_schema_version' ORDER BY name";using var reader=cmd.ExecuteReader();var names=new List<string>();while(reader.Read())names.Add(reader.GetString(0));return names;
        }
        var tables=Tables(path);
        string Snapshot(string file)
        {
            using var c=new SqliteConnection($"Data Source={file};Pooling=False");c.Open();var text=new System.Text.StringBuilder();
            foreach(var table in tables){text.Append(table);using var cmd=c.CreateCommand();cmd.CommandText=$"SELECT * FROM \"{table}\" ORDER BY rowid";using var reader=cmd.ExecuteReader();while(reader.Read()){var values=new object[reader.FieldCount];reader.GetValues(values);text.Append(JsonSerializer.Serialize(values));}}
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
        }
        var before=Snapshot(path);DatabaseUpdates.Update(target,Path.Combine(app,"Data","permissions.seed.json"));
        Assert.Equal(before,Snapshot(target));Assert.Equal(before,Snapshot(path));
        var backups=Directory.GetFiles(folder,"v31-upgrade-copy.sqlite.before-update-*.bak");Assert.Single(backups);
        DatabaseUpdates.Check(target);DatabaseUpdates.Update(target,Path.Combine(app,"Data","permissions.seed.json"));Assert.Single(Directory.GetFiles(folder,"v31-upgrade-copy.sqlite.before-update-*.bak"));
        using var verify=new SqliteConnection($"Data Source={target};Pooling=False");verify.Open();using var check=verify.CreateCommand();check.CommandText="PRAGMA integrity_check";Assert.Equal("ok",check.ExecuteScalar());check.CommandText="PRAGMA foreign_key_check";using(var reader=check.ExecuteReader())Assert.False(reader.Read());
        check.CommandText="SELECT MAX(version) FROM app_schema_version";Assert.Equal(36L,check.ExecuteScalar());Assert.Equal(before,Snapshot(target));
    }
    [Fact]
    public async Task TatToanConcurrentDraftCreatesOnlyOneRowAndKeepsSavedSnapshotStable()
    {
        int contract;TatToanInput input;
        using(var setup=Context()){var f=await TatToanFixture(setup);contract=f.Contract;input=TatToanFields(await new TatToanService(setup,TatToanClock()).NhapAsync(1,contract));}
        async Task<bool> Submit(){using var db=Context();try{await new TatToanService(db,TatToanClock()).LuuAsync(1,input);return true;}catch(InvalidOperationException){return false;}}
        var results=await Task.WhenAll(Task.Run(Submit),Task.Run(Submit));Assert.Single(results,x=>x);
        using var verify=Context();Assert.Single(await verify.BangTatToans.ToListAsync());
        var service=new TatToanService(verify,TatToanClock());var before=await service.XemAsync(3,contract);
        var final=await verify.HoaDons.SingleAsync(x=>x.HopDongId==contract && x.LoaiHoaDon=="KY_CUOI");verify.ThanhToans.Add(new() {HoaDonId=final.Id,SoTien=100000,NgayThanhToan=new(2026,10,14),TrangThai="DA_XAC_NHAN"});await verify.SaveChangesAsync();
        Assert.Equal(before.KetQua,(await service.XemAsync(3,contract)).KetQua);
        var refreshed=await service.NhapAsync(1,contract);Assert.Equal(before.KetQua+100000,refreshed.KetQua);
    }
    [Theory]
    [InlineData("ADMIN")][InlineData("QUAN_LY")]
    public async Task TatToanAppliesStaffReadPermissionsAndDeniesUnrelatedTenant(string role)
    {
        using var db=Context();var f=await TatToanFixture(db);var service=new TatToanService(db,TatToanClock());var initial=await service.NhapAsync(1,f.Contract);initial.Input.LyDo="THOA_THUAN";await service.LuuAsync(1,initial.Input);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tai_khoan SET vai_tro={role} WHERE id=3");
        if(role=="ADMIN")Assert.False((await service.XemAsync(3,f.Contract)).DuocSua);
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.XemAsync(3,f.Contract));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.NhapAsync(3,f.Contract));
        await db.Database.ExecuteSqlRawAsync("UPDATE tai_khoan SET vai_tro='KHACH_THUE' WHERE id=3; UPDATE khach_thue SET tai_khoan_id=NULL WHERE id=1");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.XemAsync(3,f.Contract));
    }
}
