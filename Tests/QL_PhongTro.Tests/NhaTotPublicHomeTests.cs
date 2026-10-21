using System.Net;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task AnonymousGuestViewsNhaTotBrandedHomePage()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: true);
        using var guest = Client();

        var html = WebUtility.HtmlDecode(await guest.GetStringAsync("/"));

        // Header branding & navigation
        Assert.Contains("NhàTốt", html);
        Assert.Contains("SỐNG ĐÚNG NƠI", html);
        Assert.Contains("Trang Chủ", html);
        Assert.Contains("Tìm Phòng", html);
        Assert.Contains("Tin Đăng Cho Thuê", html);
        Assert.Contains("Giới Thiệu", html);
        Assert.Contains("Đăng nhập", html);
        Assert.Contains("Đăng ký", html);

        // Hero banner content
        Assert.Contains("Tìm chốn ở phù hợp, bắt", html);
        Assert.Contains("đầu cuộc sống mới.", html);
        Assert.Contains("Khám phá nhà trọ, căn hộ và phòng ở minh bạch tại khu vực bạn yêu thích. Giá thật, ảnh thật, liên hệ trực tiếp.", html);

        // Quick search
        Assert.Contains("action=\"/TimTin\"", html);
        Assert.Contains("name=\"TuKhoa\"", html);
        Assert.Contains("name=\"QuanHuyen\"", html);
        Assert.Contains("name=\"GiaToiDa\"", html);

        // Featured listings & 3 core values
        Assert.Contains($"/TinDang/ChiTiet/{listingId}", html);
        Assert.Contains("Tại sao nên chọn NhàTốt?", html);
        Assert.Contains("Giá thật minh bạch", html);
        Assert.Contains("Ảnh thật xác thực", html);
        Assert.Contains("Liên hệ trực tiếp", html);
    }

    [Fact]
    public async Task GioiThieuPageRendersSuccessfullyWithCoreValuesAndSteps()
    {
        using var guest = Client();

        var response = await guest.GetAsync("/Home/GioiThieu");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("VỀ CHÚNG TÔI", html);
        Assert.Contains("NhàTốt · Sống Đúng Nơi", html);
        Assert.Contains("3 Cam kết vàng từ NhàTốt", html);
        Assert.Contains("Giá thật minh bạch", html);
        Assert.Contains("Ảnh thật xác thực", html);
        Assert.Contains("Liên hệ trực tiếp", html);
        Assert.Contains("Quy trình 4 bước thuê phòng dễ dàng", html);
    }
}
