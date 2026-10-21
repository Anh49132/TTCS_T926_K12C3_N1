using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task TenantRowsOpenTheListingLinkedToEachRequest()
    {
        var firstRequestId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "MOI", "YC-LISTING-FIRST");
        var secondRequestId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "DA_HEN_LICH", "YC-LISTING-SECOND");
        var firstListingId = Convert.ToInt32(Scalar("SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$id", ("$id", firstRequestId)));
        var secondListingId = Convert.ToInt32(Scalar("SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$id", ("$id", secondRequestId)));
        Assert.NotEqual(firstListingId, secondListingId);

        using var tenant = await Login("KHACH_THUE");
        var list = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));
        foreach (var (requestId, code, listingId) in new[]
        {
            (firstRequestId, "YC-LISTING-FIRST", firstListingId),
            (secondRequestId, "YC-LISTING-SECOND", secondListingId)
        })
        {
            var row = Regex.Match(list, $"(?s)<tr data-request-status=\"(?:MOI|DA_HEN_LICH)\"[^>]*>.*?{Regex.Escape(code)}.*?</tr>").Value;
            Assert.NotEmpty(row);
            Assert.Contains($"href=\"/YeuCau/MoTinDang/{requestId}\"", row);
            Assert.Contains($"data-open-listing-url=\"/YeuCau/MoTinDang/{requestId}\"", row);

            var resolve = await tenant.GetAsync($"/YeuCau/MoTinDang/{requestId}");
            Assert.Equal(HttpStatusCode.Redirect, resolve.StatusCode);
            var detail = WebUtility.HtmlDecode(await tenant.GetStringAsync(resolve.Headers.Location!.ToString()));
            Assert.Contains($"data-listing-id=\"{listingId}\"", detail);
        }
    }

    [Fact]
    public async Task CancelledRequestCanStillOpenItsAvailableListing()
    {
        var requestId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "DA_HUY", "YC-LISTING-CANCELLED");
        var listingId = Convert.ToInt32(Scalar("SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$id", ("$id", requestId)));
        using var tenant = await Login("KHACH_THUE");

        var resolve = await tenant.GetAsync($"/YeuCau/MoTinDang/{requestId}");
        Assert.Equal(HttpStatusCode.Redirect, resolve.StatusCode);
        var detail = WebUtility.HtmlDecode(await tenant.GetStringAsync(resolve.Headers.Location!.ToString()));

        Assert.Contains($"data-listing-id=\"{listingId}\"", detail);
        Assert.Equal("DA_HUY", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());
    }

    [Fact]
    public async Task UnavailableListingStillOpensReadOnlyForTheRequestOwner()
    {
        var requestId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "MOI", "YC-LISTING-UNAVAILABLE");
        var listingId = Convert.ToInt32(Scalar("SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$id", ("$id", requestId)));
        Execute("UPDATE tin_dang SET trang_thai='TAM_AN' WHERE id=$id", ("$id", listingId));
        using var tenant = await Login("KHACH_THUE");

        var list = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));
        var row = Regex.Match(list, "(?s)<tr data-request-status=\"MOI\"[^>]*>.*?YC-LISTING-UNAVAILABLE.*?</tr>").Value;
        Assert.NotEmpty(row);
        Assert.Contains("Mở tin đăng", row);
        Assert.Contains($"data-open-listing-url=\"/YeuCau/MoTinDang/{requestId}\"", row);

        var resolve = await tenant.GetAsync($"/YeuCau/MoTinDang/{requestId}");
        Assert.Equal(HttpStatusCode.Redirect, resolve.StatusCode);
        Assert.Equal($"/TinDang/ChiTiet/{listingId}", resolve.Headers.Location?.OriginalString);
        var detail = WebUtility.HtmlDecode(await tenant.GetStringAsync(resolve.Headers.Location!.ToString()));
        Assert.Contains($"data-listing-id=\"{listingId}\"", detail);
        Assert.Contains("không còn hiển thị công khai", detail);
        Assert.DoesNotContain("id=\"gui-yeu-cau\"", detail);
        Assert.Equal("MOI", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());
    }

    [Fact]
    public async Task TenantCannotResolveListingThroughAnotherTenantsRequest()
    {
        var requestId = CreateTenantCancellationRequest(accounts["ADMIN"], "MOI", "YC-LISTING-OTHER-TENANT");
        using var tenant = await Login("KHACH_THUE");

        var response = await tenant.GetAsync($"/YeuCau/MoTinDang/{requestId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("MOI", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE ma_yeu_cau=$code", ("$code", "YC-LISTING-OTHER-TENANT"))?.ToString());
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue WHERE khach_thue_id=(SELECT id FROM khach_thue WHERE tai_khoan_id=$tenant) AND ma_yeu_cau=$code",
            ("$tenant", accounts["KHACH_THUE"]), ("$code", "YC-LISTING-OTHER-TENANT")));
    }
}
