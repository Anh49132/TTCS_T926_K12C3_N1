using System.Text.Json;
using System.Net;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Theory]
    [InlineData("other")][InlineData("tenant")][InlineData("admin")][InlineData("manager")][InlineData("anonymous")]
    public async Task AutoReleaseRoomHttpRejectsAllUnauthorizedRoles(string role)
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
            SELECT 4,'Admin test','admin@example.test','0900000004',mat_khau,'ADMIN',1,1,ngay_tao,ngay_cap_nhat FROM tai_khoan WHERE id=1;
            INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
            SELECT 5,'Manager test','manager@example.test','0900000005',mat_khau,'QUAN_LY',1,1,ngay_tao,ngay_cap_nhat FROM tai_khoan WHERE id=1;
            UPDATE toa_nha SET quan_ly_id=5 WHERE id=1;
            """);
        var input=DongY(await new TatToanService(db,TatToanClock()).KiemTraChotAsync(1,contract));
        var before=await ReleaseSnapshot(db);
        using var factory=BillingWeb(TatToanClock());using var client=factory.CreateClient(new(){AllowAutoRedirect=false});
        if(role!="anonymous")await Login(client,role);
        var form=new Dictionary<string,string>{["Confirm.HopDongId"]=contract.ToString(),["Confirm.PhienBan"]=input.PhienBan.ToString(),
            ["Confirm.DauVanTay"]=input.DauVanTay!,["Confirm.DaKiemTra"]="true",["Confirm.DongY"]="true"};
        var response=await Post(client,"/TatToan/HoanTat",form);
        Assert.Equal(role=="anonymous"?HttpStatusCode.Redirect:HttpStatusCode.Forbidden,response.StatusCode);
        if(role=="anonymous")Assert.Contains("/Account/Login",response.Headers.Location!.ToString());
        Assert.Equal(role=="anonymous"?HttpStatusCode.Redirect:HttpStatusCode.Forbidden,(await client.GetAsync($"/TatToan/XacNhan/{contract}")).StatusCode);
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Theory]
    [InlineData("NHAP")][InlineData("CHO_HIEU_LUC")][InlineData("DA_HUY")][InlineData("DA_KET_THUC")]
    public async Task AutoReleaseRoomRejectsInvalidContractStateWithoutChanges(string state)
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());
        var input=DongY(await service.KiemTraChotAsync(1,contract));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hop_dong SET trang_thai={state} WHERE id={contract}");
        db.ChangeTracker.Clear();var before=await ReleaseSnapshot(db);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Theory]
    [InlineData("inactive")][InlineData("deleted")][InlineData("foreign-building")][InlineData("revoked-contract")][InlineData("revoked-finance")]
    public async Task AutoReleaseRoomRejectsChangedOwnershipOrPermissionsBeforeSubmit(string change)
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());
        var input=DongY(await service.KiemTraChotAsync(1,contract));
        switch(change)
        {
            case "inactive":await db.Database.ExecuteSqlRawAsync("UPDATE tai_khoan SET dang_hoat_dong=0 WHERE id=1");break;
            case "deleted":await db.Database.ExecuteSqlRawAsync("UPDATE tai_khoan SET is_deleted=1 WHERE id=1");break;
            case "foreign-building":await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET toa_nha_id=2 WHERE id=(SELECT phong_id FROM hop_dong WHERE id={contract})");break;
            default:var module=change=="revoked-contract"?"HOP_DONG":"TAI_CHINH";
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA' AND ModuleCode={module}");break;
        }
        db.ChangeTracker.Clear();var before=await ReleaseSnapshot(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Fact]
    public async Task AutoReleaseRoomRejectsOtherActiveContractWithoutChanges()
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());
        var input=DongY(await service.KiemTraChotAsync(1,contract));
        // Seed inconsistent legacy data directly; the business guard must fail closed.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hop_dong SET phong_id=(SELECT phong_id FROM hop_dong WHERE id={contract}) WHERE id<>{contract}");
        db.ChangeTracker.Clear();var before=await ReleaseSnapshot(db);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Fact]
    public async Task AutoReleaseRoomVersionOverflowRollsBackWholeSettlement()
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET phien_ban=2147483647 WHERE id=(SELECT phong_id FROM hop_dong WHERE id={contract})");
        db.ChangeTracker.Clear();var service=new TatToanService(db,TatToanClock());
        var input=DongY(await service.KiemTraChotAsync(1,contract));var before=await ReleaseSnapshot(db);
        await Assert.ThrowsAsync<OverflowException>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task AutoReleaseRoomClosesExistingHistoryOrRollsItBackOnLateFailure(bool fail)
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        var roomId=(await db.HopDongs.SingleAsync(x=>x.Id==contract)).PhongId;
        db.LichSuTrangThaiPhongs.Add(new(){PhongId=roomId,HopDongId=contract,TrangThai="DANG_THUE",TuThoiDiem=TatToanClock().UtcNow.AddDays(-10)});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();var before=await ReleaseSnapshot(db);
        var service=new TatToanService(db,TatToanClock());var input=DongY(await service.KiemTraChotAsync(1,contract));
        if(fail)
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER release_history_failure BEFORE INSERT ON lich_su_trang_thai_phong WHEN NEW.trang_thai='TRONG' BEGIN SELECT RAISE(ABORT,'Synthetic history failure'); END");
            await Assert.ThrowsAnyAsync<Exception>(()=>service.HoanTatAsync(1,input));
            using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
        }
        else
        {
            await service.HoanTatAsync(1,input);db.ChangeTracker.Clear();
            var history=await db.LichSuTrangThaiPhongs.Where(x=>x.PhongId==roomId).OrderBy(x=>x.Id).ToListAsync();
            Assert.Equal(2,history.Count);Assert.Equal(TatToanClock().UtcNow,history[0].DenThoiDiem);
            Assert.Equal(history[0].DenThoiDiem,history[1].TuThoiDiem);Assert.Null(history[1].DenThoiDiem);Assert.Equal("TRONG",history[1].TrangThai);
        }
    }

    [Theory]
    [InlineData(2)][InlineData(3)]
    public async Task AutoReleaseRoomRejectsForeignOwnerAndTenantWithoutChanges(int actor)
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        var service=new TatToanService(db,TatToanClock());var input=DongY(await service.KiemTraChotAsync(1,contract));
        var before=await ReleaseSnapshot(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.HoanTatAsync(actor,input));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Fact]
    public async Task AutoReleaseRoomHttpResultAndRefreshReadCurrentRoomState()
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());
        await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,contract)));db.ChangeTracker.Clear();
        using var factory=BillingWeb(TatToanClock());using var client=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(client,"owner");
        var html=WebUtility.HtmlDecode(await client.GetStringAsync($"/TatToan/KetQua/{contract}"));
        Assert.Contains("id=\"current-room-state\">Trống",html);Assert.Contains("Làm mới trạng thái phòng",html);Assert.Contains("/PhongTro/Details/",html);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET trang_thai='NGUNG_CHO_THUE' WHERE id=(SELECT phong_id FROM hop_dong WHERE id={contract})");
        var refreshed=WebUtility.HtmlDecode(await client.GetStringAsync($"/TatToan/KetQua/{contract}"));
        Assert.Contains("id=\"current-room-state\">Ngừng cho thuê",refreshed);Assert.Contains("Đã kết thúc",refreshed);
    }

    private static async Task<string> ReleaseSnapshot(AppDbContext db) => JsonSerializer.Serialize(new {
        Contracts=await db.HopDongs.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        Rooms=await db.PhongTros.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        Bills=await db.HoaDons.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        Lines=await db.ChiTietHoaDons.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        Deposits=await db.GiaoDichCocs.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        Settlements=await db.BangTatToans.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        Confirmations=await db.XacNhanTatToans.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        History=await db.LichSuTrangThaiPhongs.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        Listings=await db.TinDangs.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        Notices=await db.ThongBaoHoaDons.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),
        AuditCount=await db.NhatKyHoatDongs.CountAsync()
    });

    [Fact]
    public async Task AutoReleaseRoomCommitsBothStatesAndIncrementsRoomVersionOnce()
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        var roomId=(await db.HopDongs.SingleAsync(x=>x.Id==contract)).PhongId;
        var version=(await db.PhongTros.AsNoTracking().SingleAsync(x=>x.Id==roomId)).PhienBan;
        var service=new TatToanService(db,TatToanClock());var input=DongY(await service.KiemTraChotAsync(1,contract));
        await service.HoanTatAsync(1,input);db.ChangeTracker.Clear();
        var result=await service.XemAsync(1,contract);
        Assert.Equal("DA_KET_THUC",result.TrangThaiHopDong);Assert.Equal("TRONG",result.TrangThaiPhong);
        Assert.Equal("Trống",result.TenTrangThaiPhong);Assert.Equal(roomId,result.PhongId);
        Assert.Equal(version+1,(await db.PhongTros.SingleAsync(x=>x.Id==roomId)).PhienBan);
        Assert.Single(await db.LichSuTrangThaiPhongs.Where(x=>x.PhongId==roomId && x.DenThoiDiem==null && x.TrangThai=="TRONG").ToListAsync());
        var snapshot=await ReleaseSnapshot(db);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Equal(snapshot,await ReleaseSnapshot(check));
    }

    [Fact]
    public async Task AutoReleaseRoomUpdateFailureRollsBackWholeSettlement()
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        var service=new TatToanService(db,TatToanClock());var input=DongY(await service.KiemTraChotAsync(1,contract));
        var before=await ReleaseSnapshot(db);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER release_test_failure BEFORE UPDATE OF trang_thai ON phong_tro WHEN NEW.trang_thai='TRONG' BEGIN SELECT RAISE(ABORT,'Synthetic room release failure'); END");
        await Assert.ThrowsAnyAsync<Exception>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Theory]
    [InlineData("TRONG")][InlineData("NGUNG_CHO_THUE")][InlineData("DA_DAT_COC")]
    public async Task AutoReleaseRoomRejectsIncompatibleRoomState(string state)
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());
        var input=DongY(await service.KiemTraChotAsync(1,contract));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET trang_thai={state} WHERE id=(SELECT phong_id FROM hop_dong WHERE id={contract})");
        db.ChangeTracker.Clear();var before=await ReleaseSnapshot(db);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Fact]
    public async Task AutoReleaseRoomStaleVersionRejectsWithoutPartialClose()
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());
        var input=DongY(await service.KiemTraChotAsync(1,contract));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET phien_ban=phien_ban+1 WHERE id=(SELECT phong_id FROM hop_dong WHERE id={contract})");
        db.ChangeTracker.Clear();var before=await ReleaseSnapshot(db);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Fact]
    public async Task AutoReleaseRoomRefreshShowsCurrentStateAndReplayDoesNotReleaseRentedRoom()
    {
        using var db=Context();var contract=await XacNhanFixture(db);var service=new TatToanService(db,TatToanClock());
        var input=DongY(await service.KiemTraChotAsync(1,contract));await service.HoanTatAsync(1,input);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET trang_thai='DANG_THUE',phien_ban=phien_ban+1 WHERE id=(SELECT phong_id FROM hop_dong WHERE id={contract})");
        db.ChangeTracker.Clear();var before=await ReleaseSnapshot(db);
        Assert.Equal("Đang thuê",(await service.XemAsync(1,contract)).TenTrangThaiPhong);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,input));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }
}
