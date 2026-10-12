using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class ContractCreationTests
{
    NguoiOGhepInput Roommate(string identity = "123456789") => new() { HoTen = "Người ở ghép", SoDienThoai = "0901234567", SoGiayTo = identity, NgayVao = new(2026,11,5), PhienBanPhong = 0 };
    [Fact] public async Task RoommateSavedWithNullDepartureAndSignerUnchanged()
    {
        Existing(start:"2026-10-01"); using var db = Context();
        Assert.IsType<RedirectToActionResult>(await Controller(db).ThemNguoi(8, Roommate(), default));
        var stay = await db.NguoiOGheps.SingleAsync(); Assert.Null(stay.NgayRa);
        Assert.Equal(new DateOnly(2026,10,7), stay.NgayVao);
        var profile = await db.KhachThues.SingleAsync(k => k.Id == stay.KhachThueId);
        Assert.Equal("123456789",profile.SoGiayTo); Assert.Null(profile.TaiKhoanId);
        Assert.Equal(1,(await db.HopDongs.SingleAsync()).KhachDungTenId);
        Assert.Contains(await db.NhatKyHoatDongs.ToListAsync(),x => x.LoaiDoiTuong == "nguoi_o_ghep");
    }
    [Theory][InlineData("", "0901234567", "123456789")][InlineData("Tên", "123", "123456789")][InlineData("Tên", "0901234567", "123")]
    public async Task RoommateInvalidFieldsRejected(string name,string phone,string identity)
    {
        Existing(start:"2026-10-01"); using var db=Context();var input=Roommate(identity);input.HoTen=name;input.SoDienThoai=phone;
        Assert.IsType<ViewResult>(await Controller(db).ThemNguoi(8,input,default));Assert.Empty(await db.NguoiOGheps.ToListAsync());
    }
    [Fact] public async Task RoommateMissingDateUsesServerToday(){Existing(start:"2026-10-01");using var db=Context();var input=Roommate();input.NgayVao=null;Assert.IsType<RedirectToActionResult>(await Controller(db).ThemNguoi(8,input,default));Assert.Equal(new DateOnly(2026,10,7),(await db.NguoiOGheps.SingleAsync()).NgayVao);}
    [Fact] public async Task RoommateServerTodayOutsidePeriodRejected(){Existing();using var db=Context();var input=Roommate();input.NgayVao=new(2026,11,4);Assert.IsType<ViewResult>(await Controller(db).ThemNguoi(8,input,default));Assert.Empty(await db.NguoiOGheps.ToListAsync());}
    [Fact] public async Task RoommateForeignOwnerForbidden(){Existing(start:"2026-10-01");using var db=Context(2);Assert.IsType<ForbidResult>(await Controller(db,2).ThemNguoi(8,Roommate(),default));Assert.IsType<ForbidResult>(await Controller(db,2).Details(8,default));Assert.Empty(await db.NguoiOGheps.ToListAsync());}
    [Fact] public async Task RoommateSignerIdentityRejected(){Existing(start:"2026-10-01");Execute("UPDATE khach_thue SET so_giay_to='123456789' WHERE id=1");using var db=Context();Assert.IsType<ViewResult>(await Controller(db).ThemNguoi(8,Roommate(),default));Assert.Single(await db.KhachThues.ToListAsync());}
    [Fact] public async Task RoommateDuplicateRejected(){Existing(start:"2026-10-01");Execute("UPDATE phong_tro SET so_nguoi_toi_da=3");using(var db=Context())await Controller(db).ThemNguoi(8,Roommate(),default);using var check=Context();var input=Roommate();input.PhienBanPhong=1;Assert.IsType<ViewResult>(await Controller(check).ThemNguoi(8,input,default));Assert.Single(await check.NguoiOGheps.ToListAsync());}
    [Fact] public async Task RoommateCapacityAndFutureArrivalsChecked()
    {
        Existing(start:"2026-10-01");Execute("INSERT INTO khach_thue(id,ho_ten,so_dien_thoai,so_giay_to,ngay_tao) VALUES(2,'Future resident','0901234567','123456789','2026-01-01'); INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao) VALUES(8,2,'2026-12-01')");
        using var check=Context();var next=Roommate("987654321");next.PhienBanPhong=1;var controller=Controller(check);
        Assert.IsType<ViewResult>(await controller.ThemNguoi(8,next,default));
        Assert.Contains(controller.ModelState.Values.SelectMany(x=>x.Errors),e=>e.ErrorMessage.Contains("tối đa 2 người"));Assert.Single(await check.NguoiOGheps.ToListAsync());
    }
    [Fact] public async Task RoommateConcurrentAddsCannotExceedCapacity()
    {
        Existing(start:"2026-10-01");async Task<IActionResult> Save(string identity){using var db=Context();return await Controller(db).ThemNguoi(8,Roommate(identity),default);}
        var results=await Task.WhenAll(Task.Run(()=>Save("123456789")),Task.Run(()=>Save("987654321")));
        Assert.Single(results.OfType<RedirectToActionResult>());Assert.Single(results.OfType<ViewResult>());
        using var check=Context();Assert.Single(await check.NguoiOGheps.ToListAsync());Assert.Equal(2,await check.KhachThues.CountAsync());
    }
    [Fact] public async Task RoommateAuditFailureRollsBackProfileAndStay()
    {
        Existing(start:"2026-10-01");Execute("CREATE TRIGGER fail_roommate_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='nguoi_o_ghep' BEGIN SELECT RAISE(ABORT,'test'); END");
        using var db=Context();Assert.IsType<ViewResult>(await Controller(db).ThemNguoi(8,Roommate(),default));using var check=Context();
        Assert.Empty(await check.NguoiOGheps.ToListAsync());Assert.Single(await check.KhachThues.ToListAsync());Assert.Equal(0,(await check.PhongTros.SingleAsync()).PhienBan);
    }
    [Fact] public void RoommateSignerCannotBeRemovedOrDuplicated(){Existing(start:"2026-10-01");Assert.Throws<SqliteException>(()=>Execute("UPDATE hop_dong SET khach_dung_ten_id=NULL WHERE id=8"));Assert.Throws<SqliteException>(()=>Execute("INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao) VALUES(8,1,'2026-11-05')"));}
    [Fact] public async Task RoommateDetailsCountsToday()
    {
        Existing(start:"2026-10-01");using var db=Context();var input=Roommate();input.NgayVao=new(2026,10,7);await Controller(db).ThemNguoi(8,input,default);
        var model=Assert.IsType<HopDongDetailsViewModel>(Assert.IsType<ViewResult>(await Controller(db).Details(8,default)).Model);
        Assert.Single(model.Nguois);Assert.NotNull(model.DungTen);Assert.Equal("0901234567",model.Nguois[0].SoDienThoai);
    }
    [Fact] public async Task RoommateFutureStayNotCountedTodayAndSignerMasked()
    {
        Existing();Execute("INSERT INTO khach_thue(id,ho_ten,so_dien_thoai,so_giay_to,ngay_tao) VALUES(2,'Future resident','0901234567','123456789','2026-01-01'); INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao) VALUES(8,2,'2026-11-05')");using var db=Context();
        var model=Assert.IsType<HopDongDetailsViewModel>(Assert.IsType<ViewResult>(await Controller(db).Details(8,default)).Model);
        Assert.Empty(model.Nguois);Assert.Equal("******4567",model.DungTen!.SoDienThoai);
    }
    [Fact] public async Task RoommateManagerCannotWriteEvenWhenAssigned()
    {
        Existing(start:"2026-10-01");Execute("UPDATE tai_khoan SET vai_tro='QUAN_LY' WHERE id=2; UPDATE toa_nha SET quan_ly_id=2");using var db=Context(2);
        Assert.IsType<ForbidResult>(await Controller(db,2).ThemNguoi(8,Roommate(),default));Assert.Empty(await db.NguoiOGheps.ToListAsync());
    }
    [Fact] public async Task RoommateLegacyMissingSignerCannotBeAdded()
    {
        Existing(status:"NHAP");Execute("UPDATE hop_dong SET khach_dung_ten_id=NULL WHERE id=8");using var db=Context();
        Assert.IsType<ViewResult>(await Controller(db).ThemNguoi(8,Roommate(),default));Assert.Empty(await db.NguoiOGheps.ToListAsync());Assert.Single(await db.KhachThues.ToListAsync());
    }
    [Fact] public void RoommateUpgradeRepeatedPreservesDataAndIntegrity(){Existing(start:"2026-10-01");DatabaseUpdates.Update(path,seed);DatabaseUpdates.Update(path,seed);using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA integrity_check";Assert.Equal("ok",cmd.ExecuteScalar());cmd.CommandText="PRAGMA foreign_key_check";using var reader=cmd.ExecuteReader();Assert.False(reader.Read());}
}
