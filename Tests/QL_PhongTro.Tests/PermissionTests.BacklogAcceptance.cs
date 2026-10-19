using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task AssignedManagerCanReadContractsButCannotReceiveNegotiatedRent()
    {
        TenantHomeFixture();
        Execute("UPDATE toa_nha SET quan_ly_id=$manager WHERE id=80", ("$manager", accounts["QUAN_LY"]));
        Execute("UPDATE ky_hop_dong SET gia_thue=2345678 WHERE hop_dong_id=80");
        using var manager = await Login("QUAN_LY");
        var response = await manager.GetAsync("/HopDong");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("HD-HOME-MINE", html);
        Assert.DoesNotContain("2.345.678", html);
        Assert.DoesNotContain("2345678", html);
        Assert.DoesNotContain("<th>Tiền thuê</th>", html);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/HopDong/Details/80")).StatusCode);
        using var owner = await Login("CHU_NHA");
        var ownerHtml = WebUtility.HtmlDecode(await owner.GetStringAsync("/HopDong"));
        Assert.Contains("2.345.678", ownerHtml);
        Assert.Equal(2345678L, Scalar("SELECT gia_thue FROM ky_hop_dong WHERE hop_dong_id=80"));
    }

    [Fact]
    public async Task PublicSearchPaginatesThirteenListingsAsTwelveAndOne()
    {
        var ids = Enumerable.Range(0, 13).Select(_ => CreatePublicListing("TRONG", "DANG_HIEN_THI", false)).ToArray();
        using var guest = Client();
        var first = WebUtility.HtmlDecode(await guest.GetStringAsync("/TimTin"));
        var second = WebUtility.HtmlDecode(await guest.GetStringAsync("/TimTin?Trang=2"));
        int[] Visible(string html) => Regex.Matches(html, "href=\"/TinDang/ChiTiet/(\\d+)\"")
            .Select(x => int.Parse(x.Groups[1].Value)).Where(ids.Contains).Distinct().ToArray();
        var firstIds = Visible(first);
        var secondIds = Visible(second);
        Assert.Equal(12, firstIds.Length);
        Assert.Single(secondIds);
        Assert.Empty(firstIds.Intersect(secondIds));
        Assert.Equal(13, firstIds.Concat(secondIds).Distinct().Count());
    }
}
