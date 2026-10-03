using System.Net;
using System.Globalization;
using QL_PhongTro.Models;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task LandlordCanSwitchListingBetweenAllStatuses()
    {
        using var owner = await Login("CHU_NHA");
        var listingId = CreatePublicListing("TRONG", "NHAP", includePhoto: false);
        var managementPage = WebUtility.HtmlDecode(await owner.GetStringAsync("/TinDang/QuanLy"));
        Assert.Contains("Quản lý tin đăng", managementPage);
        Assert.Contains("href=\"/TinDang/QuanLy\"", managementPage);
        Assert.Contains("Hiển thị", managementPage);
        Assert.Contains("Tạm ẩn", managementPage);
        Assert.Contains("Đã cho thuê", managementPage);

        var states = new (string Code, string Label)[]
        {
            ("DANG_HIEN_THI", "Đang hiển thị"),
            ("TAM_AN", "Tạm ẩn"),
            ("DA_CHO_THUE", "Đã cho thuê"),
            ("NHAP", "Nháp")
        };

        foreach (var (code, label) in states)
        {
            var token = await PropertyToken(owner, "/TinDang/QuanLy");
            var response = await owner.PostAsync($"/TinDang/CapNhatTrangThai?id={listingId}",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["trangThai"] = code
                }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(code, Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", listingId)));
            var updatedPage = WebUtility.HtmlDecode(await owner.GetStringAsync(response.Headers.Location));
            Assert.Contains($"Đã cập nhật trạng thái tin đăng thành “{label}”.", updatedPage);
        }

        Assert.NotNull(Scalar("SELECT ngay_dang FROM tin_dang WHERE id=$id", ("$id", listingId)));
    }

    [Fact]
    public async Task LandlordCannotPublishAnotherListingWhenRoomAlreadyHasPublishedListing()
    {
        using var owner = await Login("CHU_NHA");
        var activeListingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var roomId = Convert.ToInt32(Scalar("SELECT phong_id FROM tin_dang WHERE id=$id", ("$id", activeListingId)));
        Execute("""
            INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao)
            VALUES($room,$owner,'Tin nháp thứ hai','NHAP',$created)
            """, ("$room", roomId), ("$owner", accounts["CHU_NHA"]), ("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        var draftId = Convert.ToInt32(Scalar("SELECT id FROM tin_dang WHERE phong_id=$room AND trang_thai='NHAP'", ("$room", roomId)));

        var token = await PropertyToken(owner, "/TinDang/QuanLy");
        var response = await owner.PostAsync($"/TinDang/CapNhatTrangThai?id={draftId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["trangThai"] = "DANG_HIEN_THI"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", draftId)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM tin_dang WHERE phong_id=$room AND trang_thai='DANG_HIEN_THI'", ("$room", roomId)));
        var blockedPage = WebUtility.HtmlDecode(await owner.GetStringAsync(response.Headers.Location));
        Assert.Contains("Không thể hiển thị tin này vì phòng đã có một tin khác đang hiển thị.", blockedPage);
    }
}
