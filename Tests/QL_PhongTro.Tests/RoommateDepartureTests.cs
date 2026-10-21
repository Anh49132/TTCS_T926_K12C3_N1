using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class ContractCreationTests
{
    private void DepartureStay()
    {
        Existing(start:"2026-09-01");
        Execute("""
            INSERT INTO khach_thue(id,ho_ten,so_dien_thoai,so_giay_to,ngay_tao) VALUES(2,'Bạn cùng phòng','0907654321','123456789','2026-09-01');
            INSERT INTO nguoi_o_ghep(id,hop_dong_id,khach_thue_id,ngay_vao) VALUES(1,8,2,'2026-09-01');
            """);
    }
    private static ChuyenDiInput Departure(DateOnly? day,int version=0)=>new(){NgayRa=day,PhienBanPhong=version};
    [Fact] public async Task DeparturePreservesProfileAndSeparatesDepartedFromCurrent()
    {
        DepartureStay();using var db=Context();var controller=Controller(db);
        Assert.IsType<RedirectToActionResult>(await controller.ChuyenDi(8,1,Departure(new(2026,10,6)),default));
        var model=Assert.IsType<HopDongDetailsViewModel>(Assert.IsType<ViewResult>(await controller.Details(8,default)).Model);
        Assert.Empty(model.Nguois);var departed=Assert.Single(model.DaChuyenDi);Assert.Equal(new DateOnly(2026,9,1),departed.NgayVao);Assert.Equal(new DateOnly(2026,10,6),departed.NgayRa);
        Assert.Equal("123456789",departed.SoGiayTo);Assert.Equal(2,await db.KhachThues.CountAsync());Assert.Equal(1,(await db.PhongTros.SingleAsync()).PhienBan);
        Assert.Contains(await db.NhatKyHoatDongs.ToListAsync(),x=>x.LoaiDoiTuong=="nguoi_o_ghep" && x.HanhDong=="SUA");
    }
    [Theory][InlineData(7)][InlineData(10)]
    public async Task DepartureTodayIsStillPresentAndFutureIsAllowed(int day)
    {
        DepartureStay();using var db=Context();var controller=Controller(db);Assert.IsType<RedirectToActionResult>(await controller.ChuyenDi(8,1,Departure(new(2026,10,day)),default));
        var model=Assert.IsType<HopDongDetailsViewModel>(Assert.IsType<ViewResult>(await controller.Details(8,default)).Model);
        Assert.Single(model.Nguois);Assert.Empty(model.DaChuyenDi);Assert.Equal(new DateOnly(2026,10,day),model.Nguois[0].NgayRa);
    }
    [Fact] public async Task DepartureBeforeArrivalRejected(){DepartureStay();using var db=Context();Assert.IsType<ViewResult>(await Controller(db).ChuyenDi(8,1,Departure(new(2026,8,31)),default));Assert.Null((await db.NguoiOGheps.SingleAsync()).NgayRa);Assert.Empty(await db.NhatKyHoatDongs.ToListAsync());}
    [Fact] public async Task DepartureMissingDateRejected(){DepartureStay();using var db=Context();Assert.IsType<ViewResult>(await Controller(db).ChuyenDi(8,1,Departure(null),default));Assert.Null((await db.NguoiOGheps.SingleAsync()).NgayRa);}
    [Fact] public async Task DepartureOnArrivalAllowed(){DepartureStay();using var db=Context();Assert.IsType<RedirectToActionResult>(await Controller(db).ChuyenDi(8,1,Departure(new(2026,9,1)),default));}
    [Fact] public async Task DepartureForeignOwnerAndWrongStayForbidden(){DepartureStay();using var db=Context(2);Assert.IsType<ForbidResult>(await Controller(db,2).ChuyenDi(8,1,Departure(new(2026,10,6)),default));using var owner=Context();Assert.IsType<ForbidResult>(await Controller(owner).ChuyenDi(8,99,Departure(new(2026,10,6)),default));Assert.Null((await owner.NguoiOGheps.SingleAsync()).NgayRa);}
    [Fact] public async Task DepartureStaleVersionAndRepeatedSaveRejected()
    {
        DepartureStay();using var db=Context();Assert.IsType<ViewResult>(await Controller(db).ChuyenDi(8,1,Departure(new(2026,10,6),9),default));
        Assert.IsType<RedirectToActionResult>(await Controller(db).ChuyenDi(8,1,Departure(new(2026,10,6)),default));
        Assert.IsType<ViewResult>(await Controller(db).ChuyenDi(8,1,Departure(new(2026,10,5),1),default));Assert.Equal(new DateOnly(2026,10,6),(await db.NguoiOGheps.SingleAsync()).NgayRa);
    }
    [Fact] public async Task DepartureFreesCapacityOnlyStartingNextDay()
    {
        DepartureStay();using(var db=Context())await Controller(db).ChuyenDi(8,1,Departure(new(2026,10,6)),default);
        using(var db=Context()){var input=Roommate("987654321");input.PhienBanPhong=1;input.NgayVao=new(2026,10,6);Assert.IsType<ViewResult>(await RoommateControllerAt(db,new(2026,10,6)).ThemNguoi(8,input,default));}
        using var next=Context();var allowed=Roommate("987654321");allowed.PhienBanPhong=1;allowed.NgayVao=new(2026,10,7);Assert.IsType<RedirectToActionResult>(await Controller(next).ThemNguoi(8,allowed,default));Assert.Equal(2,await next.NguoiOGheps.CountAsync());
    }
    [Fact] public async Task DepartureAuditFailureRollsBackDateAndRoomVersion()
    {
        DepartureStay();Execute("CREATE TRIGGER fail_departure_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='nguoi_o_ghep' BEGIN SELECT RAISE(ABORT,'test'); END");
        using var db=Context();Assert.IsType<ViewResult>(await Controller(db).ChuyenDi(8,1,Departure(new(2026,10,6)),default));using var check=Context();Assert.Null((await check.NguoiOGheps.SingleAsync()).NgayRa);Assert.Equal(0,(await check.PhongTros.SingleAsync()).PhienBan);Assert.Empty(await check.NhatKyHoatDongs.ToListAsync());
    }
    [Fact] public async Task DepartureConcurrentDatesOnlyOneSaved()
    {
        DepartureStay();async Task<IActionResult> Save(int day){using var db=Context();return await Controller(db).ChuyenDi(8,1,Departure(new(2026,10,day)),default);}
        var results=await Task.WhenAll(Task.Run(()=>Save(5)),Task.Run(()=>Save(6)));Assert.Single(results,x=>x is RedirectToActionResult);Assert.Single(results,x=>x is ViewResult);
        using var check=Context();Assert.Equal(1,(await check.PhongTros.SingleAsync()).PhienBan);
    }
}
