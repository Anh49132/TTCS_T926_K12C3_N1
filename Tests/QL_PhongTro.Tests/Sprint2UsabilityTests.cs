using System.Net;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task RegisterHasPasswordToggleAndConfirmationEmailIsEditable()
    {
        using var guest = Client();

        var register = await guest.GetStringAsync("/Account/Register");
        Assert.Matches("class=\"[^\"]*\\bpassword-field\\b[^\"]*\"", register);
        Assert.Contains("data-target=\"MatKhau\"", register);

        var confirmation = WebUtility.HtmlDecode(await guest.GetStringAsync("/Account/ConfirmEmail?email=khach%40example.test"));
        var emailInput = System.Text.RegularExpressions.Regex.Match(confirmation, "<input[^>]*name=\"Email\"[^>]*>").Value;
        Assert.Contains("type=\"email\"", emailInput);
        Assert.Contains("value=\"khach@example.test\"", emailInput);
        Assert.DoesNotContain("readonly", emailInput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("formaction=\"/Account/ResendConfirmation\"", confirmation);
    }

    [Fact]
    public async Task RoomManagementSearchesByRoomCode()
    {
        var key = Guid.NewGuid().ToString("N");
        Execute("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,dang_hoat_dong) VALUES($owner,$name,'Địa chỉ tìm phòng',1)",
            ("$owner", accounts["CHU_NHA"]), ("$name", "Tòa tìm phòng " + key));
        var buildingId = Convert.ToInt32(Scalar("SELECT id FROM toa_nha WHERE ten_toa_nha=$name", ("$name", "Tòa tìm phòng " + key)));
        foreach (var code in new[] { "SEARCH-A101", "SEARCH-B202" })
            Execute("INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES($building,$code,1,20,2000000,2000000,2,'TRONG',$now)",
                ("$building", buildingId), ("$code", code), ("$now", DateTime.UtcNow.ToString("O")));

        using var owner = await Login("CHU_NHA");
        var html = WebUtility.HtmlDecode(await owner.GetStringAsync($"/PhongTro?toaNhaId={buildingId}&tuKhoa=A101"));

        Assert.Contains("SEARCH-A101", html);
        Assert.DoesNotContain("SEARCH-B202", html);
    }

    [Fact]
    public async Task OwnerCanEditPublishedListingAndPublicIndexShowsChanges()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        using var owner = await Login("CHU_NHA");
        var editPage = await owner.GetStringAsync($"/TinDang/Tao?phongId={roomId}");

        var response = await owner.PostAsync("/TinDang/Tao", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(editPage),
            ["TinDangId"] = listingId.ToString(),
            ["PhongId"] = roomId.ToString(),
            ["TieuDe"] = "Tin đã sửa hiển thị công khai",
            ["NoiDung"] = "Nội dung chủ nhà vừa cập nhật",
            ["intent"] = "DANG_HIEN_THI"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/TinDang/ChiTiet/{listingId}", response.Headers.Location?.OriginalString);
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", listingId)));
        Assert.Equal("Tin đã sửa hiển thị công khai", Scalar("SELECT tieu_de FROM tin_dang WHERE id=$id", ("$id", listingId)));
        using var guest = Client();
        Assert.Contains("Tin đã sửa hiển thị công khai", WebUtility.HtmlDecode(await guest.GetStringAsync("/TinDang")));
    }

    [Fact]
    public async Task NewPublishedListingImmediatelyAppearsOnPublicIndex()
    {
        var key = Guid.NewGuid().ToString("N");
        Execute("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,dang_hoat_dong) VALUES($owner,$name,'20 Đường Mới',1)",
            ("$owner", accounts["CHU_NHA"]), ("$name", "Tòa đăng mới " + key));
        var buildingId = Convert.ToInt32(Scalar("SELECT id FROM toa_nha WHERE ten_toa_nha=$name", ("$name", "Tòa đăng mới " + key)));
        Execute("INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES($building,'NEW-P01',1,24,2800000,2800000,2,'TRONG',$now)",
            ("$building", buildingId), ("$now", DateTime.UtcNow.ToString("O")));
        var roomId = Convert.ToInt32(Scalar("SELECT id FROM phong_tro WHERE toa_nha_id=$building AND ma_phong='NEW-P01'", ("$building", buildingId)));
        using var owner = await Login("CHU_NHA");
        var createPage = await owner.GetStringAsync($"/TinDang/Tao?phongId={roomId}");

        var response = await owner.PostAsync("/TinDang/Tao", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(createPage),
            ["PhongId"] = roomId.ToString(),
            ["TieuDe"] = "Tin phòng mới xuất hiện ngay",
            ["NoiDung"] = "Thông tin phòng mới",
            ["intent"] = "DANG_HIEN_THI"
        }));

        var listingId = Convert.ToInt32(Scalar("SELECT id FROM tin_dang WHERE phong_id=$room", ("$room", roomId)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/TinDang/ChiTiet/{listingId}", response.Headers.Location?.OriginalString);
        using var guest = Client();
        Assert.Contains("Tin phòng mới xuất hiện ngay", WebUtility.HtmlDecode(await guest.GetStringAsync("/TinDang")));
    }

    [Fact]
    public async Task PublicSearchCombinesKeywordWardDistrictAndExistingCriteria()
    {
        var wanted = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var other = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        Execute("UPDATE tin_dang SET tieu_de='Phòng ban công xanh',noi_dung='Yên tĩnh gần công viên' WHERE id=$id", ("$id", wanted));
        Execute("UPDATE toa_nha SET phuong_xa='Phường Bến Nghé',quan_huyen='Quận 1' WHERE id=(SELECT p.toa_nha_id FROM phong_tro p JOIN tin_dang t ON t.phong_id=p.id WHERE t.id=$id)", ("$id", wanted));
        Execute("UPDATE tin_dang SET tieu_de='Phòng không phù hợp' WHERE id=$id", ("$id", other));
        Execute("UPDATE toa_nha SET phuong_xa='Phường 7',quan_huyen='Quận 3' WHERE id=(SELECT p.toa_nha_id FROM phong_tro p JOIN tin_dang t ON t.phong_id=p.id WHERE t.id=$id)", ("$id", other));
        using var guest = Client();
        var url = "/TimTin?TuKhoa=" + Uri.EscapeDataString("ban công")
            + "&PhuongXa=" + Uri.EscapeDataString("Phường Bến Nghé")
            + "&QuanHuyen=" + Uri.EscapeDataString("Quận 1")
            + "&GiaToiDa=3000000&DienTichToiThieu=20&SoNguoiToiDa=2";

        var html = WebUtility.HtmlDecode(await guest.GetStringAsync(url));

        Assert.Contains($"/TinDang/ChiTiet/{wanted}", html);
        Assert.DoesNotContain($"/TinDang/ChiTiet/{other}", html);
    }

    [Fact]
    public async Task PublicListingIndexOffersKeywordAndRequestedFilters()
    {
        using var guest = Client();
        var html = await guest.GetStringAsync("/TinDang");

        Assert.Contains("action=\"/TimTin\"", html);
        Assert.Contains("class=\"listing-search-form\"", html);
        Assert.Contains("class=\"listing-search-grid\"", html);
        Assert.Contains("class=\"listing-search-submit\"", html);
        foreach (var field in new[] { "TuKhoa", "PhuongXa", "QuanHuyen", "GiaToiThieu", "GiaToiDa", "DienTichToiThieu", "DienTichToiDa", "SoNguoiToiDa" })
            Assert.Contains($"name=\"{field}\"", html);
    }
}
