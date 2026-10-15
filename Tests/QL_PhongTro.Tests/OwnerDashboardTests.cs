using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task OwnerDashboardAggregatesOnlyActiveOwnedBuildings()
    {
        QL_PhongTro.Data.RentalRequestSchema.Initialize(database);
        var occupied = CreatePublicListing("DANG_THUE", "TAM_AN", includePhoto: false);
        var reserved = CreatePublicListing("DA_DAT_COC", "TAM_AN", includePhoto: false);
        var foreign = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var inactive = CreatePublicListing("TRONG", "TAM_AN", includePhoto: false);
        int Building(int listingId) => Convert.ToInt32(Scalar(
            "SELECT p.toa_nha_id FROM phong_tro p JOIN tin_dang t ON t.phong_id=p.id WHERE t.id=$id", ("$id", listingId)));
        Execute("UPDATE toa_nha SET ten_toa_nha='Cơ sở đã thuê' WHERE id=$id", ("$id", Building(occupied)));
        Execute("UPDATE toa_nha SET ten_toa_nha='Cơ sở đã cọc' WHERE id=$id", ("$id", Building(reserved)));
        Execute("UPDATE toa_nha SET ten_toa_nha='Cơ sở người khác',chu_nha_id=$owner WHERE id=$id",
            ("$owner", accounts["ADMIN"]), ("$id", Building(foreign)));
        Execute("UPDATE toa_nha SET ten_toa_nha='Cơ sở ngừng hoạt động',dang_hoat_dong=0 WHERE id=$id", ("$id", Building(inactive)));

        using var owner = await Login("CHU_NHA");
        var html = WebUtility.HtmlDecode(await owner.GetStringAsync("/"));
        var buildings = Regex.Match(html, "<select[^>]*name=\"toaNhaId\"[^>]*>[\\s\\S]*?</select>").Value;
        Assert.Contains("Cơ sở đã thuê", buildings);
        Assert.Contains("Cơ sở đã cọc", buildings);
        Assert.DoesNotContain("Cơ sở người khác", buildings);
        Assert.DoesNotContain("Cơ sở ngừng hoạt động", buildings);
        var rented = Regex.Match(html, "<a[^>]*href=\"/PhongTro\\?trangThaiFilter=DANG_THUE\"[^>]*>[\\s\\S]*?</a>").Value;
        Assert.Contains("Phòng đang thuê", rented);
        Assert.Contains("1 <small>/ 2 phòng</small>", rented);
        Assert.Contains("50%", html);
        Assert.Contains("Chưa có dữ liệu thanh toán", html);
    }
}
