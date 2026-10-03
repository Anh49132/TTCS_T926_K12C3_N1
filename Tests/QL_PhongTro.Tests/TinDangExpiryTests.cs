using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task ExpiryScanHidesOnlyExpiredActiveListingsAndShowsOwnerWarning()
    {
        using var owner = await Login("CHU_NHA");
        var now = DateTime.UtcNow;
        var expiredListingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var activeListingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        Execute("""
            UPDATE tin_dang
            SET tieu_de=$title, ngay_het_han=$expiry
            WHERE id=$id
            """, ("$title", "Tin đã hết hạn kiểm thử"),
            ("$expiry", now.AddMinutes(-1).ToString("O")), ("$id", expiredListingId));
        Execute("""
            UPDATE tin_dang
            SET tieu_de=$title, ngay_het_han=$expiry
            WHERE id=$id
            """, ("$title", "Tin còn hạn kiểm thử"),
            ("$expiry", now.AddDays(1).ToString("O")), ("$id", activeListingId));

        using (var scope = factory.Services.CreateScope())
        {
            var expiryService = scope.ServiceProvider.GetRequiredService<TinDangExpiryService>();
            Assert.Equal(1, await expiryService.ScanExpiredAsync(now));
        }

        Assert.Equal("TAM_AN", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", expiredListingId)));
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", activeListingId)));

        var managementPage = WebUtility.HtmlDecode(await owner.GetStringAsync("/TinDang/QuanLy"));
        var expiredRow = Regex.Match(managementPage, "<tr>[\\s\\S]*?Tin đã hết hạn kiểm thử[\\s\\S]*?</tr>").Value;
        var activeRow = Regex.Match(managementPage, "<tr>[\\s\\S]*?Tin còn hạn kiểm thử[\\s\\S]*?</tr>").Value;
        Assert.Contains("Tin đã quá hạn hiển thị", expiredRow);
        Assert.DoesNotContain("Tin đã quá hạn hiển thị", activeRow);
    }
}
