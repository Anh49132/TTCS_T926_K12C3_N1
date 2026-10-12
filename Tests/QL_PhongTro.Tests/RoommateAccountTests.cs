using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class ContractCreationTests
{
    sealed class ResidentClock(DateOnly day) : ITimeProvider { public DateTime UtcNow => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7); }
    HopDongController RoommateControllerAt(AppDbContext db, DateOnly day)
    {
        var http = new DefaultHttpContext { User = Principal(1) };
        return new(db, new ResidentClock(day)) { ControllerContext = new() { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
    }
    void AccountResident()
    {
        Existing(start:"2026-10-01");
        Execute("""
            INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
            VALUES(3,'Account resident','resident@s301.test','0908888888','test','KHACH_THUE',1,1,'2026-01-01','2026-01-01');
            INSERT INTO khach_thue(id,tai_khoan_id,ho_ten,so_dien_thoai,so_giay_to,ngay_tao)
            VALUES(3,3,'Account resident','0908888888','987654321012','2026-01-01');
            """);
    }
    NguoiOGhepInput AccountInput() => new() { CachThem = "CO_TAI_KHOAN", TaiKhoanId = 3, PhienBanPhong = 0 };
    [Fact] public async Task ExactAccountLookupMasksDataAndDoesNotWrite()
    {
        AccountResident(); using var db=Context();
        var result=Assert.IsType<ViewResult>(await Controller(db).TimNguoiOGhep(8,3,default));
        var model=Assert.IsType<HopDongDetailsViewModel>(result.Model);var card=Assert.IsType<TaiKhoanOGhepXacNhan>(model.TaiKhoanXacNhan);
        Assert.Equal("Account resident",card.HoTen);Assert.Equal("******8888",card.DienThoaiDaChe);Assert.Equal("********1012",card.CanCuocDaChe);
        Assert.True(card.EmailDaXacThuc);Assert.True(card.DuThongTin);Assert.Empty(await db.NguoiOGheps.ToListAsync());Assert.Equal(2,await db.KhachThues.CountAsync());
    }
    [Fact] public async Task LookupForeignOwnerAndManagerCannotSeeCard()
    {
        AccountResident();using var db=Context(2);Assert.IsType<ForbidResult>(await Controller(db,2).TimNguoiOGhep(8,3,default));
        Execute("UPDATE tai_khoan SET vai_tro='QUAN_LY' WHERE id=2; UPDATE toa_nha SET quan_ly_id=2");
        Assert.IsType<ForbidResult>(await Controller(db,2).TimNguoiOGhep(8,3,default));
    }
    [Theory][InlineData(null)][InlineData(0)][InlineData(-1)][InlineData(12345)][InlineData(1)]
    public async Task InvalidOrNonTenantIdHasNoConfirmation(int? accountId)
    {
        AccountResident();using var db=Context();var c=Controller(db);var result=Assert.IsType<ViewResult>(await c.TimNguoiOGhep(8,accountId,default));
        Assert.Null(Assert.IsType<HopDongDetailsViewModel>(result.Model).TaiKhoanXacNhan);Assert.False(c.ModelState.IsValid);
    }
    [Fact] public async Task AccountAddReusesProfileAndIgnoresPostedEditsAndDate()
    {
        AccountResident();using var db=Context();var input=AccountInput();input.HoTen="Injected edit";input.SoGiayTo="111111111111";input.SoDienThoai="0901111111";input.NgayVao=new(2099,1,1);
        Assert.IsType<RedirectToActionResult>(await Controller(db).ThemNguoi(8,input,default));
        var stay=await db.NguoiOGheps.SingleAsync();Assert.Equal(3,stay.KhachThueId);Assert.Equal(new DateOnly(2026,10,7),stay.NgayVao);
        var profile=await db.KhachThues.SingleAsync(k=>k.Id==3);Assert.Equal(3,profile.TaiKhoanId);Assert.Equal("Account resident",profile.HoTen);Assert.Equal("987654321012",profile.SoGiayTo);Assert.Equal("0908888888",profile.SoDienThoai);Assert.Equal(2,await db.KhachThues.CountAsync());
    }
    [Theory][InlineData("UPDATE tai_khoan SET dang_hoat_dong=0 WHERE id=3")][InlineData("UPDATE tai_khoan SET is_deleted=1 WHERE id=3")][InlineData("UPDATE khach_thue SET ho_ten='' WHERE id=3")][InlineData("UPDATE khach_thue SET so_dien_thoai=NULL WHERE id=3")][InlineData("UPDATE khach_thue SET so_giay_to=NULL WHERE id=3")][InlineData("DELETE FROM khach_thue WHERE id=3")]
    public async Task AccountStatusAndCompletenessAreRecheckedOnSave(string change)
    {
        AccountResident();using(var db=Context())Assert.IsType<ViewResult>(await Controller(db).TimNguoiOGhep(8,3,default));
        Execute(change);using var check=Context();Assert.IsType<ViewResult>(await Controller(check).ThemNguoi(8,AccountInput(),default));Assert.Empty(await check.NguoiOGheps.ToListAsync());
    }
    [Fact] public async Task ManualPhoneWithAccountMustUseAccountTab()
    {
        AccountResident();using var db=Context();var input=Roommate();input.SoDienThoai="0908888888";var c=Controller(db);
        Assert.IsType<ViewResult>(await c.ThemNguoi(8,input,default));Assert.Contains(c.ModelState.Values.SelectMany(x=>x.Errors),e=>e.ErrorMessage.Contains("Có tài khoản"));Assert.Equal(2,await db.KhachThues.CountAsync());Assert.Empty(await db.NguoiOGheps.ToListAsync());
    }
    [Fact] public async Task ManualIdentityWithLinkedAccountCannotCreateDuplicate()
    {
        AccountResident();using var db=Context();Assert.IsType<ViewResult>(await Controller(db).ThemNguoi(8,Roommate("987654321012"),default));Assert.Equal(2,await db.KhachThues.CountAsync());
    }
    [Fact] public async Task MatchingUnlinkedProfileIsReusedWithoutEdits()
    {
        Existing(start:"2026-10-01");Execute("INSERT INTO khach_thue(id,ho_ten,so_dien_thoai,so_giay_to,ngay_tao) VALUES(3,'Người ở ghép','0901234567','123456789','2026-01-01')");using var db=Context();
        Assert.IsType<RedirectToActionResult>(await Controller(db).ThemNguoi(8,Roommate(),default));Assert.Equal(3,(await db.NguoiOGheps.SingleAsync()).KhachThueId);Assert.Equal(2,await db.KhachThues.CountAsync());
    }
    [Fact] public async Task MismatchedUnlinkedIdentityIsRejected()
    {
        Existing(start:"2026-10-01");Execute("INSERT INTO khach_thue(id,ho_ten,so_dien_thoai,so_giay_to,ngay_tao) VALUES(3,'Other identity','0909999999','123456789','2026-01-01')");using var db=Context();
        Assert.IsType<ViewResult>(await Controller(db).ThemNguoi(8,Roommate(),default));Assert.Empty(await db.NguoiOGheps.ToListAsync());Assert.Equal(2,await db.KhachThues.CountAsync());
    }
    [Fact] public async Task AccountAlreadyAddedOrSignerCannotBeAddedAgain()
    {
        AccountResident();Execute("UPDATE phong_tro SET so_nguoi_toi_da=3");using(var db=Context())Assert.IsType<RedirectToActionResult>(await Controller(db).ThemNguoi(8,AccountInput(),default));
        using var check=Context();var input=AccountInput();input.PhienBanPhong=1;Assert.IsType<ViewResult>(await Controller(check).ThemNguoi(8,input,default));Assert.Single(await check.NguoiOGheps.ToListAsync());
    }
    void OtherActiveContract(bool signer, string status="DANG_HIEU_LUC", string end="2027-09-30", string? departure=null)
    {
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="""
            INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao)
            VALUES(2,1,'B204',2,25,3500000,3500000,2,'DANG_THUE','2026-01-01');
            INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES(20,'HD-other',2,$signer,$status);
            INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES(20,'2026-01-01',$end,3500000);
            """;cmd.Parameters.AddWithValue("$signer",signer?3:1);cmd.Parameters.AddWithValue("$status",status);cmd.Parameters.AddWithValue("$end",end);cmd.ExecuteNonQuery();
        if(!signer){cmd.CommandText="INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao,ngay_ra) VALUES(20,3,'2026-01-01',$departure)";cmd.Parameters.AddWithValue("$departure",(object?)departure??DBNull.Value);cmd.ExecuteNonQuery();}
    }
    [Theory][InlineData(true,"DANG_HIEU_LUC")][InlineData(false,"DANG_HIEU_LUC")][InlineData(true,"CHO_HIEU_LUC")]
    public async Task AccountInAnotherCommittedContractCannotJoin(bool signer,string status)
    {
        AccountResident();OtherActiveContract(signer,status);using var db=Context();var c=Controller(db);
        Assert.IsType<ViewResult>(await c.ThemNguoi(8,AccountInput(),default));Assert.Contains(c.ModelState.Values.SelectMany(x=>x.Errors),e=>e.ErrorMessage.Contains("hợp đồng còn hiệu lực khác"));Assert.False(await db.NguoiOGheps.AnyAsync(g=>g.HopDongId==8));
    }
    [Theory][InlineData("DA_KET_THUC")][InlineData("DA_HUY")][InlineData("NHAP")]
    public async Task OldOrUncommittedContractDoesNotPreventJoining(string status)
    {
        AccountResident();OtherActiveContract(true,status);using var db=Context();Assert.IsType<RedirectToActionResult>(await Controller(db).ThemNguoi(8,AccountInput(),default));
    }
    [Theory][InlineData("2026-10-06",true)][InlineData("2026-10-07",false)]
    public async Task OtherDepartureBoundaryIsInclusive(string departure,bool allowed)
    {
        AccountResident();OtherActiveContract(false,departure:departure);using var db=Context();var result=await Controller(db).ThemNguoi(8,AccountInput(),default);
        if(allowed)Assert.IsType<RedirectToActionResult>(result);else Assert.IsType<ViewResult>(result);
    }
    [Fact] public async Task DuplicateIdentityAcrossUnlinkedProfilesInOtherContractIsBlocked()
    {
        AccountResident();Execute("UPDATE khach_thue SET tai_khoan_id=NULL WHERE id=3; UPDATE khach_thue SET ho_ten='Người ở ghép',so_dien_thoai='0901234567',so_giay_to='123456789' WHERE id=3");OtherActiveContract(true);using var db=Context();Assert.IsType<ViewResult>(await Controller(db).ThemNguoi(8,Roommate(),default));Assert.False(await db.NguoiOGheps.AnyAsync(g=>g.HopDongId==8));
    }
    [Fact] public async Task AccountAddAuditFailurePreservesLinkedProfile()
    {
        AccountResident();Execute("CREATE TRIGGER fail_account_resident BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='nguoi_o_ghep' BEGIN SELECT RAISE(ABORT,'test'); END");using var db=Context();
        Assert.IsType<ViewResult>(await Controller(db).ThemNguoi(8,AccountInput(),default));using var check=Context();Assert.Empty(await check.NguoiOGheps.ToListAsync());Assert.Equal(2,await check.KhachThues.CountAsync());Assert.Equal(0,(await check.PhongTros.SingleAsync()).PhienBan);
    }
}
