using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using QL_PhongTro.Models;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private async Task<int> XacNhanFixture(AppDbContext db,long deposit=5000000,bool fine=false)
    {
        var f=await TatToanFixture(db,deposit);
        await db.Database.ExecuteSqlRawAsync("UPDATE hop_dong SET tien_coc_thoa_thuan=7000000");db.ChangeTracker.Clear();
        var service=new TatToanService(db,TatToanClock());var input=TatToanFields(await service.NhapAsync(1,f.Contract));
        input.ApDungPhat=fine;input.TienPhat=fine?3500000:0;await service.LuuAsync(1,input);db.ChangeTracker.Clear();return f.Contract;
    }
    private static XacNhanTatToanInput DongY(TatToanViewModel model)
    {model.Confirm.DaKiemTra=true;model.Confirm.DongY=true;return model.Confirm;}

    [Fact]
    public async Task XacNhanMidMonthUsesInvoiceMeterSnapshotWithoutChangingMonthlyMeterSchema()
    {
        using var db=Context();var f=await MonthlyFixture(db);
        await db.Database.ExecuteSqlRawAsync("UPDATE chi_so_dien_nuoc SET tu_ngay='2026-09-01',den_ngay='2026-09-30'; UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1");db.ChangeTracker.Clear();
        var requests = new CheckoutRequestService(db, CollectionClock());
        var requestId = await requests.SubmitAsync(3, f.A, new(2026,10,13), "Đề nghị trả trước hạn");
        await requests.ChangeAsync(1, requestId, 0, "DA_TIEP_NHAN", new(2026,10,13), "Đã thống nhất");
        var handover=new TraPhongService(db,TatToanClock());var input=new TraPhongInput {HopDongId=f.A,NgayTra=new(2026,10,13),ChiSoDien=224,ChiSoNuoc=30,LyDo="THOA_THUAN"};
        input.DauVanTay=TraPhongService.DauVanTay(await handover.NhapAsync(1,input,true));var invoice=await handover.LuuAsync(1,input);
        var service=new TatToanService(db,TatToanClock());await service.LuuAsync(1,(await service.NhapAsync(1,f.A)).Input);
        var count=await db.ChiSoDienNuocs.CountAsync();await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,f.A)));db.ChangeTracker.Clear();
        Assert.Equal(count,await db.ChiSoDienNuocs.CountAsync());
        Assert.Equal("HOAN_TAT", (await db.YeuCauTraPhongs.AsNoTracking().SingleAsync(x=>x.Id==requestId)).TrangThai);
        Assert.True(await db.ThongBaoTraPhongs.AnyAsync(x=>x.YeuCauId==requestId && x.Loai=="HOAN_TAT" && x.NguoiNhanId==3));
        var bill=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==invoice);
        Assert.Equal("DA_PHAT_HANH",bill.TrangThai);Assert.Equal(795355,bill.TongTien);
        Assert.Equal(224,bill.ChiTiet.Single(x=>x.CachTinhApDung==CachTinhDichVu.TheoChiSo && x.ChiSoCuoi==224).ChiSoCuoi);
        Assert.All(await db.ChiSoDienNuocs.Where(x=>x.HopDongId==f.A).ToListAsync(),x=>Assert.True(x.DaKhoa));
    }

    [Theory]
    [InlineData(5000000,1550000,0)] [InlineData(3450000,0,0)] [InlineData(2000000,0,1450000)] [InlineData(0,0,3450000)]
    public async Task XacNhanClosesPublishesOffsetsOnceAndSharesFrozenResult(long deposit,long refund,long extra)
    {
        using var db=Context();var contract=await XacNhanFixture(db,deposit);var service=new TatToanService(db,TatToanClock());
        var input=DongY(await service.KiemTraChotAsync(1,contract));await service.HoanTatAsync(1,input);db.ChangeTracker.Clear();
        var owner=await service.XemAsync(1,contract);var tenant=await service.XemAsync(3,contract);
        Assert.Equal("DA_KET_THUC",owner.TrangThaiHopDong);Assert.Equal("DA_CHOT",owner.Bang.TrangThai);Assert.False(owner.DuocSua);
        Assert.Equal(refund,owner.Bang.TienPhaiHoan);Assert.Equal(extra,owner.Bang.TienKhachTraThem);Assert.Equal(owner.KetQua,tenant.KetQua);
        Assert.Equal(2450000,owner.Bang.NoHoaDonCuoi);Assert.Equal(450000,owner.Bang.TienHuHong);
        var bill=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==owner.Bang.HoaDonCuoiId);
        Assert.Equal("DA_PHAT_HANH",bill.TrangThai);Assert.Equal(2450000,bill.TongTien);Assert.Equal(2,bill.ChiTiet.Count(x=>x.LoaiKhoan=="PHAT_SINH"));
        Assert.Equal(Math.Min(deposit,3450000),owner.XacNhan!.TienCanTru);Assert.Equal(refund-extra,owner.XacNhan.SoTienTatToan);
        Assert.Equal(extra,owner.ConPhaiThu);Assert.Equal(refund,owner.CocDangGiu);Assert.Equal(0,owner.DaHoanCoc);
        Assert.Single(await db.XacNhanTatToans.ToListAsync());Assert.Single(await db.ThongBaoHoaDons.Where(x=>x.HoaDonId==bill.Id).ToListAsync());
        Assert.Single(await db.LichSuTrangThaiPhongs.ToListAsync());Assert.Equal("TRONG",(await db.PhongTros.SingleAsync(x=>x.Id==1)).TrangThai);
        Assert.Single(await db.TinDangs.Where(x=>x.TrangThai=="NHAP").ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,input));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuAsync(1,new() {HopDongId=contract}));
        await Assert.ThrowsAsync<SqliteException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE thanh_ly_hop_dong SET tien_hu_hong=0 WHERE hop_dong_id={contract}"));
        await Assert.ThrowsAsync<SqliteException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE chi_tiet_khau_tru SET so_tien=0"));
        var due=await new GhiThuHoaDonService(db,TatToanClock()).ChiTietAsync(1,bill.Id);Assert.Equal(Math.Min(extra,2450000),due.ConPhaiTra);
    }
    [Fact]
    public async Task XacNhanPenaltyIsChargedInInvoiceExactlyOnceAndCanCollectAfterClosure()
    {
        using var db=Context();var contract=await XacNhanFixture(db,5000000,true);var service=new TatToanService(db,TatToanClock());
        await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,contract)));db.ChangeTracker.Clear();
        var saved=await service.XemAsync(3,contract);Assert.Equal(1950000,saved.Bang.TienKhachTraThem);
        var bill=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==saved.Bang.HoaDonCuoiId);Assert.Equal(5950000,bill.TongTien);
        Assert.Single(bill.ChiTiet,x=>x.TenKhoan=="Phạt chấm dứt trước hạn");
        var collection=new GhiThuHoaDonService(db,TatToanClock());var key=Guid.NewGuid().ToString("N");
        var result=await collection.GhiAsync(1,new() {HoaDonId=bill.Id,SoTien=1950000,NgayThu=new(2026,10,14),HinhThuc="TIEN_MAT",KhoaChongTrung=key});
        Assert.Equal(0,result.ConPhaiTra);var after=await service.XemAsync(3,contract);Assert.Equal(saved.KetQua,after.KetQua);Assert.Equal(0,after.ConPhaiThu);
    }
    [Theory]
    [InlineData("missing")] [InlineData("final")] [InlineData("pending")] [InlineData("amount")] [InlineData("draft")]
    public async Task XacNhanRejectsIncompleteOrUnresolvedData(string problem)
    {
        using var db=Context();var contract=await XacNhanFixture(db);var bang=await db.BangTatToans.AsNoTracking().SingleAsync();
        switch(problem)
        {
            case "missing": await db.Database.ExecuteSqlRawAsync("DROP TRIGGER cham_dut_no_delete; DELETE FROM cham_dut_hop_dong");break;
            case "final": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hoa_don SET trang_thai='DA_HUY' WHERE id={bang.HoaDonCuoiId}");break;
            case "pending":db.ThanhToans.Add(new() {HoaDonId=bang.HoaDonCuoiId,SoTien=1,NgayThanhToan=new(2026,10,14),TrangThai="CHO_XAC_NHAN"});await db.SaveChangesAsync();break;
            case "amount":await db.Database.ExecuteSqlRawAsync("UPDATE thanh_ly_hop_dong SET tien_phai_hoan=tien_phai_hoan+1");break;
            case "draft":await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hoa_don(ma_hoa_don,hop_dong_id,thang,nam,tu_ngay,den_ngay,ngay_chot,so_nguoi_tinh_phi,ngay_lap,han_thanh_toan,nguoi_lap_id) VALUES('draft',{contract},8,2026,'2026-08-01','2026-08-31','2026-08-31',1,'2026-10-14','2026-10-21',1)");break;
        }
        db.ChangeTracker.Clear();await Assert.ThrowsAsync<InvalidOperationException>(()=>new TatToanService(db,TatToanClock()).KiemTraChotAsync(1,contract));Assert.Empty(await db.XacNhanTatToans.ToListAsync());
    }
    [Fact]
    public async Task XacNhanRejectsMissingSettlementUnconfirmedCheckboxesStaleAndForeignRoles()
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());
        var form=(await service.KiemTraChotAsync(1,contract)).Confirm;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,form));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.KiemTraChotAsync(2,contract));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.KiemTraChotAsync(3,contract));
        form.DongY=form.DaKiemTra=true;await db.Database.ExecuteSqlRawAsync("UPDATE phong_tro SET phien_ban=phien_ban+1 WHERE id=1");db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,form));
        Assert.Empty(await db.XacNhanTatToans.ToListAsync());
    }
    [Fact]
    public async Task XacNhanAuditFailureRollsBackInvoiceOffsetsContractRoomAndListing()
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());var input=DongY(await service.KiemTraChotAsync(1,contract));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_confirmation_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='xac_nhan_tat_toan' BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END");
        await Assert.ThrowsAnyAsync<Exception>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Empty(await check.XacNhanTatToans.ToListAsync());Assert.Empty(await check.ThongBaoHoaDons.ToListAsync());Assert.Empty(await check.TinDangs.ToListAsync());Assert.Empty(await check.LichSuTrangThaiPhongs.ToListAsync());
        Assert.False(await check.GiaoDichCocs.AnyAsync(x=>x.Loai=="CAN_TRU"));Assert.Equal("DANG_HIEU_LUC",(await check.HopDongs.SingleAsync(x=>x.Id==contract)).TrangThai);
        Assert.Equal("NHAP",(await check.BangTatToans.SingleAsync()).TrangThai);Assert.Equal(2000000,(await check.HoaDons.SingleAsync(x=>x.LoaiHoaDon=="KY_CUOI")).TongTien);
    }
    [Fact]
    public async Task XacNhanConcurrentConfirmationHasExactlyOneWinner()
    {
        int contract;XacNhanTatToanInput input;using(var setup=Context()){contract=await XacNhanFixture(setup);input=DongY(await new TatToanService(setup,TatToanClock()).KiemTraChotAsync(1,contract));}
        async Task<bool> Submit(){using var db=Context();try{await new TatToanService(db,TatToanClock()).HoanTatAsync(1,input);return true;}catch(InvalidOperationException){return false;}}
        var results=await Task.WhenAll(Task.Run(Submit),Task.Run(Submit));Assert.Single(results,x=>x);using var check=Context();Assert.Single(await check.XacNhanTatToans.ToListAsync());Assert.Equal(2,await check.GiaoDichCocs.CountAsync(x=>x.Loai=="CAN_TRU"));
    }
    [Fact]
    public async Task XacNhanRefundIsActualSeparateIdempotentAndCannotExceedDue()
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,contract)));db.ChangeTracker.Clear();
        var input=new HoanCocTatToanInput {HopDongId=contract,SoTien=1000000,HinhThuc="CHUYEN_KHOAN",KhoaChongTrung=Guid.NewGuid().ToString("N"),DaHoan=true};
        await service.HoanCocAsync(1,input);await service.HoanCocAsync(1,input);Assert.Equal(1,await db.GiaoDichCocs.CountAsync(x=>x.Loai=="HOAN_COC"));
        var saved=await service.XemAsync(3,contract);Assert.Equal(1000000,saved.DaHoanCoc);Assert.Equal(550000,saved.CocDangGiu);Assert.Equal(1550000,saved.Bang.TienPhaiHoan);
        input.KhoaChongTrung=Guid.NewGuid().ToString("N");await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanCocAsync(1,input));
        input.SoTien=550000;await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.HoanCocAsync(3,input));await service.HoanCocAsync(1,input);
        Assert.Equal(0,(await service.XemAsync(1,contract)).CocDangGiu);
    }
    [Fact]
    public async Task XacNhanRequiresSavedSettlement()
    {
        using var db=Context();var f=await TatToanFixture(db);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new TatToanService(db,TatToanClock()).KiemTraChotAsync(1,f.Contract));
    }
    [Theory]
    [InlineData("HOP_DONG")][InlineData("TAI_CHINH")]
    public async Task XacNhanRequiresBothWritePermissions(string module)
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='CHU_NHA' AND ModuleCode={module}");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new TatToanService(db,TatToanClock()).KiemTraChotAsync(1,contract));
    }
    [Fact]
    public async Task XacNhanHttpCsrfTwoConsentsReplayAndBothSidesReadOnly()
    {
        using var db=Context();var contract=await XacNhanFixture(db);using var factory=BillingWeb(TatToanClock());using var client=factory.CreateClient(new() {AllowAutoRedirect=false});await Login(client,"owner");
        var html=await client.GetStringAsync($"/TatToan/XacNhan/{contract}");
        string Field(string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var form=new Dictionary<string,string> {["Confirm.HopDongId"]=contract.ToString(),["Confirm.PhienBan"]=Field("Confirm.PhienBan"),["Confirm.DauVanTay"]=Field("Confirm.DauVanTay"),["Confirm.DaKiemTra"]="true",["Confirm.DongY"]="true"};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/TatToan/HoanTat",new FormUrlEncodedContent(form))).StatusCode);
        form["__RequestVerificationToken"]=Field("__RequestVerificationToken");form["Confirm.DongY"]="false";
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync("/TatToan/HoanTat",new FormUrlEncodedContent(form))).StatusCode);
        var modal=await client.PostAsync("/TatToan/XemTruocXacNhan",new FormUrlEncodedContent(form));Assert.Contains("Xác nhận hoàn tất trả phòng",WebUtility.HtmlDecode(await modal.Content.ReadAsStringAsync()));
        form["Confirm.DongY"]="true";Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/TatToan/HoanTat",new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync("/TatToan/HoanTat",new FormUrlEncodedContent(form))).StatusCode);
        var owner=WebUtility.HtmlDecode(await client.GetStringAsync($"/TatToan/ChiTiet/{contract}"));Assert.Contains("Đã kết thúc",owner);Assert.DoesNotContain("Sửa bảng nháp",owner);
        await Login(client,"tenant");var tenant=WebUtility.HtmlDecode(await client.GetStringAsync($"/TatToan/ChiTiet/{contract}"));Assert.Contains("Đã kết thúc",tenant);Assert.Contains("1.550.000",tenant);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/TatToan/XacNhan/{contract}")).StatusCode);
        await Login(client,"other");Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/TatToan/ChiTiet/{contract}")).StatusCode);
    }
    [Fact]
    public async Task XacNhanEndedContractRejectsNewInvoiceAndPublishAtServiceAndDatabase()
    {
        using var db=Context();var f=await TatToanFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hop_dong SET trang_thai='DA_KET_THUC' WHERE id={f.Contract}");
        var service=new HoaDonDichVuService(db,new DichVuService(db),TatToanClock());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PhatHanhNhapAsync(1,new() {Id=f.Final,XacNhan=true,NgayPhatHanh=new(2026,10,14),HanThanhToan=new(2026,10,21)}));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.TaoNhapThangAsync(1,1,f.Contract,2026,11));
        await Assert.ThrowsAsync<SqliteException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hoa_don SET trang_thai='DA_PHAT_HANH' WHERE id={f.Final}"));
        await Assert.ThrowsAsync<SqliteException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hoa_don(ma_hoa_don,hop_dong_id,thang,nam,tu_ngay,den_ngay,ngay_chot,so_nguoi_tinh_phi,ngay_lap,han_thanh_toan,nguoi_lap_id) VALUES('blocked',{f.Contract},11,2026,'2026-11-01','2026-11-30','2026-11-30',1,'2026-10-14','2026-11-30',1)"));
    }
    [Fact]
    public async Task XacNhanV33UpgradesCopyAndPreservesAllV32Rows()
    {
        using(var db=Context())
        {
            var f=await TatToanFixture(db);var service=new TatToanService(db,TatToanClock());
            await service.LuuAsync(1,TatToanFields(await service.NhapAsync(1,f.Contract)));
        }
        SchemaTestFixture.RemoveXacNhanExtensions(path);
        var target=Path.Combine(folder,"v33-copy.sqlite");
        using(var source=Open())using(var copy=new SqliteConnection($"Data Source={target};Pooling=False")){copy.Open();source.BackupDatabase(copy);}
        var tables=new List<string>();using(var source=Open()){using var cmd=source.CreateCommand();cmd.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name<>'app_schema_version' ORDER BY name";using var reader=cmd.ExecuteReader();while(reader.Read())tables.Add(reader.GetString(0));}
        string Snapshot(string file)
        {
            using var c=new SqliteConnection($"Data Source={file};Pooling=False");c.Open();var text=new System.Text.StringBuilder();
            foreach(var table in tables){using var cmd=c.CreateCommand();cmd.CommandText=$"SELECT * FROM \"{table}\" ORDER BY rowid";using var reader=cmd.ExecuteReader();while(reader.Read()){var row=new object[reader.FieldCount];reader.GetValues(row);text.Append(System.Text.Json.JsonSerializer.Serialize(row));}}
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
        }
        var before=Snapshot(path);DatabaseUpdates.Update(target,Path.Combine(app,"Data","permissions.seed.json"));
        Assert.Equal(before,Snapshot(path));Assert.Equal(before,Snapshot(target));
        DatabaseUpdates.Update(target,Path.Combine(app,"Data","permissions.seed.json"));DatabaseUpdates.Check(target);
        Assert.Single(Directory.GetFiles(folder,"v33-copy.sqlite.before-update-*.bak"));Assert.Equal(before,Snapshot(target));
        using var verify=new SqliteConnection($"Data Source={target};Pooling=False");verify.Open();using var check=verify.CreateCommand();
        check.CommandText="PRAGMA integrity_check";Assert.Equal("ok",check.ExecuteScalar());check.CommandText="PRAGMA foreign_key_check";using(var reader=check.ExecuteReader())Assert.False(reader.Read());
        check.CommandText="SELECT COUNT(*) FROM xac_nhan_tat_toan";Assert.Equal(0L,check.ExecuteScalar());
        check.CommandText="SELECT MAX(version) FROM app_schema_version";Assert.Equal(42L,check.ExecuteScalar());
    }
}
