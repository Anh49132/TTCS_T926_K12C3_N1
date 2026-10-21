using System.Net;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Theory]
    [InlineData("quaHanTu=-1")]
    [InlineData("tienTu=-1")]
    [InlineData("quaHanTu=10&quaHanDen=2")]
    [InlineData("tienTu=500&tienDen=100")]
    [InlineData("tienTu=1.5")]
    [InlineData("tienTu=1.000.000")]
    [InlineData("tienTu=abc")]
    [InlineData("tienTu=9223372036854775808")]
    [InlineData("quaHanTu=2147483648")]
    [InlineData("sapXep=UNKNOWN")]
    public async Task DebtRejectsInvalidFiltersInsteadOfSilentlyChangingThem(string query)
    {
        using (var db = Context()) await CollectionFixtureAsync(db);
        using var web = BillingWeb(CollectionClock());
        using var owner = web.CreateClient(new() { AllowAutoRedirect = false });
        await Login(owner, "owner");
        var page = await owner.GetAsync("/CongNo?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Contains("Chưa thể áp dụng bộ lọc", html);
        Assert.DoesNotContain("TỔNG KẾT QUẢ ĐANG LỌC", html);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/CongNo/ExportCsv?" + query)).StatusCode);
    }

    [Fact]
    public async Task DebtServiceRejectsNegativeAndReversedRanges()
    {
        using var db = Context(); var service = new CongNoService(db, CollectionClock());
        await Assert.ThrowsAsync<ArgumentException>(() => service.LayAsync(1, false, null, -1, null, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.LayAsync(1, false, null, 5, 1, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.LayAsync(1, false, null, null, null, 50, 10));
    }
}
