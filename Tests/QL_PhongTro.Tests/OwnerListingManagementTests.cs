using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    private static string AntiForgery(string html) => WebUtility.HtmlDecode(
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    [Fact]
    public async Task QuanLy_ChiDangVaGoTinCuaToaDuocPhanCong()
    {
        var assignedListingId = CreatePublicListing("TRONG", "TAM_AN", includePhoto: false);
        var (assignedRoomId, assignedBuildingId) = GetListingRoomAndBuilding(assignedListingId);
        var otherListingId = CreatePublicListing("TRONG", "TAM_AN", includePhoto: false);
        var (otherRoomId, _) = GetListingRoomAndBuilding(otherListingId);
        Execute("UPDATE toa_nha SET quan_ly_id=$manager WHERE id=$building",
            ("$manager", accounts["QUAN_LY"]), ("$building", assignedBuildingId));

        using var manager = await Login("QUAN_LY");
        var manageHtml = WebUtility.HtmlDecode(await manager.GetStringAsync("/TinDang/QuanLy"));
        Assert.Contains("href=\"/TinDang/QuanLy\"", manageHtml);
        Assert.Contains($"/TinDang/Sua/{assignedListingId}", manageHtml);
        Assert.DoesNotContain($"/TinDang/Sua/{otherListingId}", manageHtml);

        var createHtml = await manager.GetStringAsync($"/TinDang/Tao?phongId={assignedRoomId}");
        var publish = await manager.PostAsync("/TinDang/Tao", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(createHtml),
            ["PhongId"] = assignedRoomId.ToString(),
            ["TieuDe"] = "Tin do quản lý đăng",
            ["NoiDung"] = "Đúng phạm vi tòa được phân công"
        }));
        Assert.Equal(HttpStatusCode.Redirect, publish.StatusCode);
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", assignedListingId)));
        Assert.Equal((long)accounts["QUAN_LY"], Scalar("SELECT nguoi_dang_id FROM tin_dang WHERE id=$id", ("$id", assignedListingId)));

        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/TinDang/Tao?phongId={otherRoomId}")).StatusCode);
        manageHtml = await manager.GetStringAsync("/TinDang/QuanLy");
        var removeOther = await manager.PostAsync($"/TinDang/Go/{otherListingId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(manageHtml)
        }));
        Assert.Equal(HttpStatusCode.NotFound, removeOther.StatusCode);
        Assert.Equal("TAM_AN", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", otherListingId)));

        var removeAssigned = await manager.PostAsync($"/TinDang/Go/{assignedListingId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(manageHtml)
        }));
        Assert.Equal(HttpStatusCode.Redirect, removeAssigned.StatusCode);
        Assert.Equal("TAM_AN", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", assignedListingId)));
    }

    [Fact]
    public async Task KhachThueBiChanNhungAdminDuocMoQuanLyTinDang()
    {
        using var tenant = await Login("KHACH_THUE");
        using var admin = await Login("ADMIN");

        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync("/TinDang/QuanLy")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/TinDang/QuanLy")).StatusCode);
    }

    [Fact]
    public async Task ChuNha_MoLaiBanNhapGiuNguyenNoiDungDaLuu()
    {
        var listingId = CreatePublicListing("TRONG", "NHAP", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        Execute("UPDATE tin_dang SET tieu_de='Tiêu đề bản nháp',noi_dung='Mô tả bản nháp đã lưu' WHERE id=$id", ("$id", listingId));
        using var owner = await Login("CHU_NHA");
        var html = WebUtility.HtmlDecode(await owner.GetStringAsync($"/TinDang/Tao?phongId={roomId}"));
        Assert.Contains("Tiêu đề bản nháp", html);
        Assert.Contains("Mô tả bản nháp đã lưu", html);
    }

    [Fact]
    public async Task ChuNha_DangLaiVaGoTinCuaPhongMinh()
    {
        var listingId = CreatePublicListing("TRONG", "TAM_AN", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        using var owner = await Login("CHU_NHA");

        var manage = WebUtility.HtmlDecode(await owner.GetStringAsync("/TinDang/QuanLy"));
        Assert.Contains("Tin đăng cho thuê", manage);
        Assert.Contains("Đổi trạng thái tin phòng", manage);

        var createHtml = await owner.GetStringAsync($"/TinDang/Tao?phongId={roomId}");
        var publish = await owner.PostAsync("/TinDang/Tao", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(createHtml),
            ["PhongId"] = roomId.ToString(),
            ["TieuDe"] = "Tin chủ nhà vừa đăng lại",
            ["NoiDung"] = "Mô tả mới"
        }));
        Assert.Equal(HttpStatusCode.Redirect, publish.StatusCode);
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", listingId)));
        Assert.Equal("Tin chủ nhà vừa đăng lại", Scalar("SELECT tieu_de FROM tin_dang WHERE id=$id", ("$id", listingId)));

        manage = WebUtility.HtmlDecode(await owner.GetStringAsync("/TinDang/QuanLy"));
        var remove = await owner.PostAsync($"/TinDang/Go/{listingId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(manage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, remove.StatusCode);
        Assert.Equal("/TinDang/QuanLy", remove.Headers.Location?.OriginalString);
        Assert.Equal("TAM_AN", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", listingId)));

        using var guest = Client();
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/TinDang/ChiTiet/{listingId}")).StatusCode);
    }

    [Fact]
    public async Task ChuNha_KhongGoDuocTinKhongThuocToaNhaMinh()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        Execute("UPDATE toa_nha SET chu_nha_id=$other WHERE id=(SELECT p.toa_nha_id FROM phong_tro p JOIN tin_dang t ON t.phong_id=p.id WHERE t.id=$id)",
            ("$other", accounts["ADMIN"]), ("$id", listingId));
        using var owner = await Login("CHU_NHA");
        var html = await owner.GetStringAsync("/TinDang/QuanLy");

        var response = await owner.PostAsync($"/TinDang/Go/{listingId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(html)
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", listingId)));
    }

    [Fact]
    public async Task TinQuaHan_TuDongTamAnVaCanhBaoChuNha()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        Execute("UPDATE tin_dang SET ngay_het_han=$expired WHERE id=$id",
            ("$expired", DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-dd HH:mm:ss")), ("$id", listingId));
        using var owner = await Login("CHU_NHA");

        var manage = WebUtility.HtmlDecode(await owner.GetStringAsync("/TinDang/QuanLy"));

        Assert.Equal("TAM_AN", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", listingId)));
        Assert.Contains("Đã hết hạn", manage);
        Assert.Contains("tự chuyển sang Tạm ẩn", manage);
        Assert.Contains("Đổi trạng thái tin phòng", manage);
        using var guest = Client();
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/TinDang/ChiTiet/{listingId}")).StatusCode);
    }

    [Fact]
    public async Task YeuCauDaHuy_KhongChanKhachGuiYeuCauMoi()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        RentalRequestSchema.Initialize(database);
        Execute("UPDATE tai_khoan SET email_confirmed=1,must_change_password=0 WHERE id=$id", ("$id", accounts["KHACH_THUE"]));
        Execute("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES($account,'Khách test',$now)",
            ("$account", accounts["KHACH_THUE"]), ("$now", DateTime.UtcNow.ToString("O")));
        var tenantId = Convert.ToInt32(Scalar("SELECT id FROM khach_thue WHERE tai_khoan_id=$account", ("$account", accounts["KHACH_THUE"])));
        Execute("""
            INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,trang_thai,ngay_tao)
            VALUES('YC-CANCELLED',$listing,$tenant,'XEM_PHONG',$date,1,'DA_HUY',$now)
            """, ("$listing", listingId), ("$tenant", tenantId), ("$date", DateOnly.FromDateTime(DateTime.Today.AddDays(2)).ToString("yyyy-MM-dd")), ("$now", DateTime.UtcNow.ToString("O")));

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, accounts["KHACH_THUE"].ToString())], "test"))
        };
        await using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + database).Options,
            new HttpContextAccessor { HttpContext = http });
        var service = new YeuCauThueService(context, new SystemTimeProvider());
        Assert.Null(await service.FindOpenRequest(listingId, accounts["KHACH_THUE"]));

        var created = await service.Send(listingId, accounts["KHACH_THUE"], new GuiYeuCauViewModel
        {
            LoaiYeuCau = "XEM_PHONG",
            NgayMongMuon = service.Today.AddDays(2),
            SoNguoiDuKien = 1,
            LoiNhan = "Yêu cầu mới sau khi đã hủy"
        });

        Assert.NotNull(created);
        Assert.Equal(2L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue WHERE tin_dang_id=$listing AND khach_thue_id=$tenant", ("$listing", listingId), ("$tenant", tenantId)));
    }

    [Fact]
    public async Task PublicIndexSortsPricesAndPaginatesTwelveListings()
    {
        var expected = new List<int>();
        for (var i = 0; i < 25; i++)
        {
            var id = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
            var (roomId, _) = GetListingRoomAndBuilding(id);
            Execute("UPDATE phong_tro SET gia_thue=$price WHERE id=$id", ("$price", 1000000 + i * 100000), ("$id", roomId));
            expected.Add(id);
        }
        CreatePublicListing("TRONG", "NHAP", includePhoto: false);
        using var guest = Client();
        async Task<int[]> Ids(string query)
        {
            var html = WebUtility.HtmlDecode(await guest.GetStringAsync("/TinDang?" + query));
            return Regex.Matches(html, "<h2><a href=\"/TinDang/ChiTiet/(\\d+)\"")
                .Select(m => int.Parse(m.Groups[1].Value)).ToArray();
        }
        Assert.Equal(expected.Take(12), await Ids("sapXep=gia-tang"));
        Assert.Equal(expected.Skip(12).Take(12), await Ids("sapXep=gia-tang&trang=2"));
        Assert.Equal(expected.Skip(24), await Ids("sapXep=gia-tang&trang=3"));
        Assert.Equal(expected.AsEnumerable().Reverse().Take(12), await Ids("sapXep=gia-giam"));
        Assert.Equal(expected.Skip(24), await Ids("sapXep=gia-tang&trang=999"));
        var page = await guest.GetStringAsync("/TinDang?sapXep=gia-giam");
        Assert.Contains("sapXep=gia-giam", page);
        Assert.Contains("trang=2", page);
    }
}
