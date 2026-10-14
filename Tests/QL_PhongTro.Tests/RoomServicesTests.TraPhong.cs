using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private static MockTimeProvider CheckoutClock() => new() { UtcNow = new(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc) };
    private async Task<int> CheckoutFixture(QL_PhongTro.Data.AppDbContext db)
    {
        var f = await MonthlyFixture(db);
        // MonthlyFixture installs the real schema; move readings to the previous closed month.
        await db.Database.ExecuteSqlRawAsync("UPDATE chi_so_dien_nuoc SET tu_ngay='2026-09-01',den_ngay='2026-09-30'; UPDATE ky_hop_dong SET gia_thue=3500000;");
        db.ChangeTracker.Clear();
        return f.A;
    }
    private static TraPhongInput CheckoutInput(int id) => new() { HopDongId = id, NgayTra = new(2026,10,13), ChiSoDien = 224, ChiSoNuoc = 30 };

    [Fact]
    public async Task CheckoutProratesRentAndMetersPersistsSnapshotsWithoutClosingContract()
    {
        using var db = Context(); var id = await CheckoutFixture(db);
        var service = new TraPhongService(db, CheckoutClock()); var input = CheckoutInput(id);
        var preview = await service.NhapAsync(1, input, true);
        Assert.Equal(1467742, preview.HoaDon!.ChiTiet.Single(x => x.LoaiKhoan == "TIEN_PHONG").ThanhTien);
        Assert.Equal(new long[] { 296000,80000 }, preview.HoaDon.ChiTiet.Where(x => x.DichVuId.HasValue).Select(x => x.ThanhTien));
        Assert.Equal(1843742, preview.HoaDon.TongTien);
        input.DauVanTay = TraPhongService.DauVanTay(preview);
        var billId = await service.LuuAsync(1, input);
        using var verify = Context();
        var bill = await verify.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == billId);
        Assert.Equal("KY_CUOI",bill.LoaiHoaDon); Assert.Equal("NHAP",bill.TrangThai);
        Assert.Equal(new DateOnly(2026,10,13),bill.DenNgay); Assert.Equal(3,bill.ChiTiet.Count);
        Assert.Equal("DANG_HIEU_LUC", (await verify.HopDongs.FindAsync(id))!.TrangThai);
        Assert.Null((await verify.HopDongs.FindAsync(id))!.NgayTraPhong);
        Assert.Equal("DANG_THUE",(await verify.PhongTros.FindAsync(id))!.TrangThai);
        Assert.True(await verify.NhatKyHoatDongs.AnyAsync(x => x.LoaiDoiTuong == "hoa_don" && x.DoiTuongId == billId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LuuAsync(1,input));
        Assert.Equal(1,await verify.HoaDons.CountAsync());
        await verify.Database.ExecuteSqlRawAsync("UPDATE cau_hinh_dich_vu SET don_gia=0");
        Assert.Equal(billId,(await service.NhapAsync(1,new() { HopDongId=id })).HoaDonDaCo);
        Assert.Equal(1843742,(await verify.HoaDons.AsNoTracking().SingleAsync()).TongTien);
    }

    [Theory]
    [InlineData(0,149,30)] [InlineData(0,224,24)] [InlineData(0,-1,30)]
    [InlineData(0,224.0001,30)] [InlineData(2,224,30)] [InlineData(-20,224,30)]
    public async Task CheckoutRejectsInvalidFinalReadingAndDates(int offset, decimal electric, decimal water)
    {
        using var db = Context(); var id = await CheckoutFixture(db);
        var service = new TraPhongService(db,CheckoutClock()); var input = CheckoutInput(id);
        input.NgayTra = input.NgayTra!.Value.AddDays(offset); input.ChiSoDien = electric; input.ChiSoNuoc = water;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.NhapAsync(1,input,true));
        Assert.Empty(await db.HoaDons.ToListAsync());
    }

    [Fact]
    public async Task CheckoutRejectsForeignOwnerMissingDateStalePreviewAndAuditFailure()
    {
        using var db = Context(); var id = await CheckoutFixture(db);
        var service = new TraPhongService(db,CheckoutClock()); var input = CheckoutInput(id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=> service.NhapAsync(2,input,true));
        input.NgayTra=null;
        await Assert.ThrowsAsync<InvalidOperationException>(()=> service.NhapAsync(1,input,true));
        input=CheckoutInput(id);
        input.DauVanTay=TraPhongService.DauVanTay(await service.NhapAsync(1,input,true));
        await db.Database.ExecuteSqlRawAsync("UPDATE cau_hinh_dich_vu SET don_gia=don_gia+1000");
        await Assert.ThrowsAsync<InvalidOperationException>(()=> service.LuuAsync(1,input));
        input.DauVanTay=TraPhongService.DauVanTay(await service.NhapAsync(1,input,true));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_checkout_audit BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'synthetic failure'); END;");
        await Assert.ThrowsAnyAsync<Exception>(()=> service.LuuAsync(1,input));
        using var verify=Context(); Assert.Empty(await verify.HoaDons.ToListAsync()); Assert.Empty(await verify.ChiTietHoaDons.ToListAsync());
    }

    [Fact]
    public async Task CheckoutUsesPriceOnReturnDateAndRoomOverride()
    {
        using var db=Context(); var id=await CheckoutFixture(db);
        await db.Database.ExecuteSqlRawAsync("UPDATE cau_hinh_dich_vu SET den_ngay='2026-10-12' WHERE dich_vu_id=(SELECT id FROM dich_vu WHERE ma_dich_vu='DIEN'); INSERT INTO cau_hinh_dich_vu(toa_nha_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,dang_ap_dung,da_chot_gia,nguoi_tao_id,ngay_tao) SELECT 1,id,'THEO_CHI_SO','kWh',5000,'2026-10-13',1,1,1,'2026-10-01' FROM dich_vu WHERE ma_dich_vu='DIEN';");
        var service=new TraPhongService(db,CheckoutClock());
        var preview=await service.NhapAsync(1,CheckoutInput(id),true);
        Assert.Equal(5000,preview.HoaDon!.ChiTiet.Single(x=>x.TenKhoan=="Điện kỳ").DonGia);
        await db.Database.ExecuteSqlRawAsync("UPDATE dich_vu_phong SET don_gia_rieng=6000 WHERE phong_id=1 AND dich_vu_toa_nha_id IN (SELECT id FROM dich_vu_toa_nha WHERE dich_vu_id=(SELECT id FROM dich_vu WHERE ma_dich_vu='DIEN'));");
        preview=await service.NhapAsync(1,CheckoutInput(id),true);
        Assert.Equal(6000,preview.HoaDon!.ChiTiet.Single(x=>x.TenKhoan=="Điện kỳ").DonGia);
    }

    [Fact]
    public async Task CheckoutCannotCreateDuplicateInAnotherMonthOrBillSameMonthTwice()
    {
        using var db=Context(); var id=await CheckoutFixture(db);
        var service=new TraPhongService(db,CheckoutClock()); var input=CheckoutInput(id);
        input.DauVanTay=TraPhongService.DauVanTay(await service.NhapAsync(1,input,true));
        await service.LuuAsync(1,input);
        var futureClock=CheckoutClock(); futureClock.UtcNow=new(2026,11,15,0,0,0,DateTimeKind.Utc);
        input.NgayTra=new(2026,11,13);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new TraPhongService(db,futureClock).NhapAsync(1,input,true));
        var other=CheckoutInput(id+1);
        db.HoaDons.Add(new() { MaHoaDon="EXISTING-MONTH",HopDongId=other.HopDongId,Nam=2026,Thang=10,TuNgay=new(2026,10,1),DenNgay=new(2026,10,31),NgayChot=new(2026,10,31),SoNguoiTinhPhi=1,NguoiLapId=1,NgayLap=CheckoutClock().UtcNow,HanThanhToan=new(2026,11,7) });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.NhapAsync(1,other,true));
    }

    [Fact]
    public async Task CheckoutHttpRendersPreviewSavesAndEnforcesOwnerCsrfAndRole()
    {
        using var db=Context(); var id=await CheckoutFixture(db);
        using var factory=BillingWeb(CheckoutClock()); using var client=factory.CreateClient(new() { AllowAutoRedirect=false });
        await Login(client,"owner");
        var html=await client.GetStringAsync($"/TraPhong/Nhap/{id}"); Assert.Contains("Chỉ số điện nước cuối cùng",html);
        var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value); Assert.NotEmpty(token);
        var fields=new Dictionary<string,string> { ["Input.HopDongId"]=id.ToString(),["Input.NgayTra"]="2026-10-13",["Input.ChiSoDien"]="224",["Input.ChiSoNuoc"]="30" };
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/TraPhong/Tinh",new FormUrlEncodedContent(fields))).StatusCode);
        fields["__RequestVerificationToken"]=token;
        var estimate=await client.PostAsync("/TraPhong/UocTinh",new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK,estimate.StatusCode);
        using(var json=System.Text.Json.JsonDocument.Parse(await estimate.Content.ReadAsStringAsync())) Assert.Equal("1843742",json.RootElement.GetProperty("total").GetString());
        var preview=await client.PostAsync("/TraPhong/Tinh",new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK,preview.StatusCode);
        var content=await preview.Content.ReadAsStringAsync(); Assert.Contains("1.843.742",content);
        var edit=await client.PostAsync("/TraPhong/Sua",new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK,edit.StatusCode); Assert.Contains("Chỉ số điện nước cuối cùng",await edit.Content.ReadAsStringAsync());
        fields["Input.DauVanTay"]=WebUtility.HtmlDecode(Regex.Match(content,"name=\"Input.DauVanTay\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(fields["Input.DauVanTay"]);
        var save=await client.PostAsync("/TraPhong/Luu",new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.Redirect,save.StatusCode);
        Assert.Contains("1.843.742",await client.GetStringAsync(save.Headers.Location));
        await Login(client,"other"); Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/TraPhong/Nhap/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(save.Headers.Location)).StatusCode);
        await Login(client,"tenant"); Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/TraPhong")).StatusCode);
    }

    [Theory]
    [InlineData(2026,10,1,31,3500000,112903)] [InlineData(2026,10,13,31,3500000,1467742)]
    [InlineData(2026,10,31,31,3500000,3500000)] [InlineData(2026,4,15,30,3500000,1750000)]
    [InlineData(2026,2,14,28,3500000,1750000)] [InlineData(2028,2,15,29,3500000,1810345)]
    [InlineData(2026,1,1,31,500000,16129)] [InlineData(2026,4,1,30,500000,16667)]
    [InlineData(2026,2,28,28,500000,500000)] [InlineData(2028,2,29,29,500000,500000)]
    [InlineData(2026,4,1,30,500025,16668)]
    public void CheckoutRentExamples(int year,int month,int days,int totalDays,long rent,long expected)
    {
        Assert.Equal(totalDays,DateTime.DaysInMonth(year,month));
        Assert.Equal(expected,TraPhongService.TienPhongTheoNgay(rent,days,totalDays));
    }

    [Fact]
    public async Task CheckoutUsesHandoverBaselineAndOnlyDaysSinceMoveIn()
    {
        using var db=Context(); var id=await CheckoutFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM chi_so_dien_nuoc WHERE hop_dong_id={id}; UPDATE ky_hop_dong SET ngay_bat_dau='2026-10-08' WHERE hop_dong_id={id};");
        db.HopDongChiSoDauKys.Add(new() { HopDongId=id,NgayBanGiao=new(2026,10,8),ChiSoDien=150,ChiSoNuoc=25,NguoiNhapId=1,NgayNhap=CheckoutClock().UtcNow });
        await db.SaveChangesAsync();
        var service=new TraPhongService(db,CheckoutClock());
        var preview=await service.NhapAsync(1,CheckoutInput(id),true);
        var rent=preview.HoaDon!.ChiTiet.Single(x=>x.LoaiKhoan=="TIEN_PHONG");
        Assert.Equal(6,rent.SoNgayTinhTien); Assert.Equal(677419,rent.ThanhTien);
        var input=CheckoutInput(id); input.ChiSoDien=150;input.ChiSoNuoc=25;
        Assert.All((await service.NhapAsync(1,input,true)).HoaDon!.ChiTiet.Where(x=>x.DichVuId.HasValue),x=>Assert.Equal(0,x.ThanhTien));
    }

    [Fact]
    public async Task CheckoutConcurrentSubmissionsPersistOneInvoice()
    {
        using(var setup=Context()) await CheckoutFixture(setup);
        async Task<bool> Submit()
        {
            using var db=Context(); var service=new TraPhongService(db,CheckoutClock()); var input=CheckoutInput(1);
            try {
                input.DauVanTay=TraPhongService.DauVanTay(await service.NhapAsync(1,input,true));
                await service.LuuAsync(1,input); return true;
            } catch(InvalidOperationException) { return false; }
        }
        var results=await Task.WhenAll(Task.Run(Submit),Task.Run(Submit));
        Assert.Single(results,x=>x);
        using var verify=Context(); Assert.Equal(1,await verify.HoaDons.CountAsync()); Assert.Equal(3,await verify.ChiTietHoaDons.CountAsync());
    }

    [Fact]
    public async Task CheckoutRejectsGenericDraftEditPublishAndReplacement()
    {
        using var db=Context(); var id=await CheckoutFixture(db);
        var service=new TraPhongService(db,CheckoutClock()); var input=CheckoutInput(id);
        input.DauVanTay=TraPhongService.DauVanTay(await service.NhapAsync(1,input,true));
        var invoice=await service.LuuAsync(1,input);
        var billing=new HoaDonDichVuService(db,new DichVuService(db),CheckoutClock());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>billing.LuuNhapAsync(1,new() { Id=invoice }));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>billing.PhatHanhNhapAsync(1,new() { Id=invoice,XacNhan=true }));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>billing.TaoNhapThayTheAsync(1,invoice,0));
        using var verify=Context(); Assert.Equal("NHAP",(await verify.HoaDons.SingleAsync()).TrangThai);
    }

    [Theory]
    [InlineData("HOP_DONG")]
    [InlineData("TAI_CHINH")]
    public async Task CheckoutRequiresBothModuleWritePermissions(string module)
    {
        using var db=Context(); var id=await CheckoutFixture(db);
        using var factory=BillingWeb(CheckoutClock()); using var client=factory.CreateClient(new() { AllowAutoRedirect=false });
        await Login(client,"owner");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='CHU_NHA' AND ModuleCode={module}");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/TraPhong")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/TraPhong/Nhap/{id}")).StatusCode);
    }
}
