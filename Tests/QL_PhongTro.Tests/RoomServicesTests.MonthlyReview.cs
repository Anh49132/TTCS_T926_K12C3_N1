using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Theory]
    [InlineData("2026-09-01", "2026-09-08")]
    [InlineData("2026-11-07", "2026-11-14")]
    [InlineData("2028-02-25", "2028-03-03")]
    [InlineData("2026-12-29", "2027-01-05")]
    public async Task MonthlyReviewBusinessDateAndDefaultDueAreSeparateFromUtcAction(string issue, string due)
    {
        using var db = Context(); await MonthlyFixture(db);
        var clock = new MockTimeProvider { UtcNow = new(2026,10,8,14,0,0,DateTimeKind.Utc) };
        await new HoaDonDichVuService(db, new(db), clock).PhatHanhThangAsync(1,1,2026,10,issueDate:DateOnly.Parse(issue));
        Assert.All(await db.HoaDons.ToListAsync(), invoice =>
        {
            Assert.Equal(DateOnly.Parse(issue),invoice.NgayPhatHanhNghiepVu);
            Assert.Equal(DateOnly.Parse(due),invoice.HanThanhToan);
            Assert.Equal(clock.UtcNow,invoice.NgayPhatHanh); Assert.Equal(clock.UtcNow,invoice.NgayLap);
            Assert.Equal(10,invoice.Thang); Assert.Equal(2026,invoice.Nam);
        });
    }

    [Fact]
    public async Task MonthlyReviewCustomDueAndSkipReasonsPersistCorrectly()
    {
        using var db = Context(); var f = await MonthlyFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM chi_so_dien_nuoc WHERE hop_dong_id={f.B} AND dich_vu_id={f.Nuoc}");
        var service = new HoaDonDichVuService(db,new(db));
        var preview = await service.XemThangAsync(1,1,2026,10,new(2026,11,7),new(2026,11,25));
        Assert.Single(preview.DuKien); Assert.Single(preview.BoQua);
        Assert.Contains("Thiếu chỉ số nước",preview.BoQua.Single().LyDo);
        var result = await service.PhatHanhDanhSachAsync(1,1,2026,10,[f.A],new(2026,11,7),new(2026,11,25));
        Assert.Equal(1,result.SoDaPhatHanh);
        Assert.Contains(result.Phongs,x=>x.HopDongId==f.B && x.TrangThai=="BO_QUA" && x.LyDo.Contains("Thiếu chỉ số nước"));
        var invoice = await db.HoaDons.SingleAsync(); Assert.Equal(f.A,invoice.HopDongId);
        Assert.Equal(new DateOnly(2026,11,25),invoice.HanThanhToan);
        Assert.False(await db.HoaDons.AnyAsync(x=>x.HopDongId==f.B));
    }

    [Fact]
    public async Task MonthlyReviewBothMetersMissingAndNoPriceAreExplained()
    {
        using var db = Context(); var f = await MonthlyFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM chi_so_dien_nuoc WHERE hop_dong_id={f.B}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dich_vu_phong SET don_gia_rieng=0 WHERE phong_id={f.A} AND dich_vu_toa_nha_id=(SELECT id FROM dich_vu_toa_nha WHERE dich_vu_id={f.Dien} AND toa_nha_id=1)");
        var service=new HoaDonDichVuService(db,new(db)); var preview=await service.XemThangAsync(1,1,2026,10);
        Assert.Empty(preview.DuKien);
        Assert.Contains(preview.BoQua,x=>x.HopDongId==f.B && x.LyDo.Contains("Chưa chốt chỉ số điện và nước"));
        Assert.Contains(preview.BoQua,x=>x.HopDongId==f.A && x.LyDo.Contains("Thiếu đơn giá điện"));
        Assert.Equal(0,await service.PhatHanhThangAsync(1,1,2026,10)); Assert.Empty(await db.HoaDons.ToListAsync());
    }

    [Fact]
    public async Task MonthlyReviewRejectsDueBeforeIssueAndStaleAmount()
    {
        using var db=Context();var f=await MonthlyFixture(db);var service=new HoaDonDichVuService(db,new(db));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PhatHanhThangAsync(1,1,2026,10,issueDate:new(2026,11,7),dueDate:new(2026,11,6)));
        Assert.Empty(await db.HoaDons.ToListAsync());
        var preview=await service.XemThangAsync(1,1,2026,10);var fingerprints=preview.DuKien.ToDictionary(x=>x.HoaDon.HopDongId,HoaDonDichVuService.ReviewFingerprint);
        await db.Database.ExecuteSqlRawAsync("UPDATE ky_hop_dong SET gia_thue=2000000");
        var result=await service.PhatHanhDanhSachAsync(1,1,2026,10,[f.A,f.B],expected:fingerprints);
        Assert.Equal(0,result.SoDaPhatHanh);Assert.All(result.Phongs,x=>Assert.Contains("Dữ liệu đã thay đổi",x.LyDo));
        Assert.Empty(await db.HoaDons.ToListAsync());Assert.False(await db.ChiSoDienNuocs.AnyAsync(x=>x.DaKhoa));
    }

    private static string FormValue(string html,string field) => Regex.Match(WebUtility.HtmlDecode(html),$"name=\"{field}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;

    [Fact]
    public async Task MonthlyReviewHttpRequiresConfirmationAndRejectsDatesOrSkippedRoomTampering()
    {
        using var db=Context();var f=await MonthlyFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM chi_so_dien_nuoc WHERE hop_dong_id={f.B} AND dich_vu_id={f.Nuoc}");
        using var factory=BillingWeb();using var client=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(client,"owner");
        var html=await client.GetStringAsync("/HoaDonDichVu/Monthly?toaNhaId=1&nam=2026&thang=10&ngayPhatHanh=2026-11-07&hanThanhToan=2026-11-25");
        Assert.Contains("07/11/2026",html);Assert.Contains("25/11/2026",html);Assert.Contains("skipped-table",html);
        var fields=new Dictionary<string,string>{["toaNhaId"]="1",["nam"]="2026",["thang"]="10",["selectedIds"]=f.A.ToString(),
            ["ngayPhatHanh"]="2026-11-07",["hanThanhToan"]="2026-11-25",["reviewToken"]=FormValue(html,"reviewToken"),["__RequestVerificationToken"]=FormValue(html,"__RequestVerificationToken")};
        Assert.NotEmpty(fields["reviewToken"]);
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/HoaDonDichVu/IssueMonthly",new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Empty(await db.HoaDons.AsNoTracking().ToListAsync());
        fields["xacNhan"]="true";fields["selectedIds"]=f.B.ToString();
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync("/HoaDonDichVu/IssueMonthly",new FormUrlEncodedContent(fields))).StatusCode);
        fields["selectedIds"]=f.A.ToString();fields["hanThanhToan"]="2026-11-26";
        await client.PostAsync("/HoaDonDichVu/IssueMonthly",new FormUrlEncodedContent(fields));Assert.Empty(await db.HoaDons.AsNoTracking().ToListAsync());
        fields["hanThanhToan"]="2026-11-25";
        var response=await client.PostAsync("/HoaDonDichVu/IssueMonthly",new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        var resultHtml=WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location));
        Assert.Contains("Kết quả từng phòng",resultHtml);Assert.Contains("Thiếu chỉ số nước",resultHtml);Assert.Contains("Đã tạo nháp",resultHtml);
        using var verify=Context();var invoice=await verify.HoaDons.SingleAsync();Assert.Equal(f.A,invoice.HopDongId);Assert.Equal(new DateOnly(2026,11,25),invoice.HanThanhToan);
        Assert.Equal("NHAP",invoice.TrangThai);Assert.Null(invoice.NgayPhatHanhNghiepVu);
        Assert.Empty(await verify.ThongBaoHoaDons.ToListAsync());
    }

    [Fact]
    public async Task MonthlyReviewRoomNoLongerRentedStillHasExplicitSkippedResult()
    {
        using var db=Context();var f=await MonthlyFixture(db);var service=new HoaDonDichVuService(db,new(db));
        var preview=await service.XemThangAsync(1,1,2026,10);
        var expected=preview.DuKien.ToDictionary(x=>x.HoaDon.HopDongId,HoaDonDichVuService.ReviewFingerprint);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET trang_thai='TRONG' WHERE id={f.A}");
        var result=await service.PhatHanhDanhSachAsync(1,1,2026,10,[f.A],expected:expected);
        Assert.Equal(0,result.SoDaPhatHanh);
        Assert.Contains(result.Phongs,x=>x.HopDongId==f.A && x.TrangThai=="BO_QUA" && x.LyDo.Contains("không còn đủ điều kiện"));
        Assert.Empty(await db.HoaDons.ToListAsync());
    }

    [Fact]
    public async Task MonthlyReviewPreviouslySkippedRoomIsNotIssuedEvenIfReadingsArriveAfterPreview()
    {
        using var db=Context();var f=await MonthlyFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM chi_so_dien_nuoc WHERE hop_dong_id={f.B} AND dich_vu_id={f.Nuoc}");
        var service=new HoaDonDichVuService(db,new(db));var preview=await service.XemThangAsync(1,1,2026,10);
        var expected=preview.DuKien.ToDictionary(x=>x.HoaDon.HopDongId,HoaDonDichVuService.ReviewFingerprint);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES({f.B},{f.Nuoc},'2026-10-01','2026-10-31','20','25',1,'2026-10-08')");
        var result=await service.PhatHanhDanhSachAsync(1,1,2026,10,expected.Keys.ToArray(),expected:expected);
        Assert.Equal(1,result.SoDaPhatHanh);Assert.False(await db.HoaDons.AnyAsync(x=>x.HopDongId==f.B));
        Assert.Contains(result.Phongs,x=>x.HopDongId==f.B && x.TrangThai=="KHONG_CHON");
    }
}
