using System.Net;
using System.Text.RegularExpressions;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class BaoHongTests
{
    [Fact]
    public async Task ManagerNotificationsBellLegacyLinkAndFiltersRespectScopeAndPermission()
    {
        using var f=new Fixture();var ids=await FilterReports(f);
        var password=Guid.NewGuid().ToString("N")+"Aa1!";
        f.Execute($"UPDATE tai_khoan SET mat_khau='{BCrypt.Net.BCrypt.HashPassword(password,4)}'");
        using var web=FilterWeb(f);using var client=await FilterLogin(web,password);
        var old=await client.GetAsync("/Modules/YEU_CAU_THUE?section=thong-bao");
        Assert.Equal(HttpStatusCode.Redirect,old.StatusCode);Assert.Equal("/ThongBaoQuanLy",old.Headers.Location!.ToString());
        var page=WebUtility.HtmlDecode(await client.GetStringAsync("/ThongBaoQuanLy"));
        Assert.Contains("href=\"/ThongBaoQuanLy\"",page);Assert.Contains("Thông báo báo hỏng",page);
        Assert.Contains("data-manager-bell",page);
        var preview=await client.GetAsync("/ThongBaoQuanLy/TomTat");
        Assert.Equal(HttpStatusCode.OK,preview.StatusCode);
        Assert.True(preview.Headers.CacheControl!.NoStore);
        using var json=System.Text.Json.JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.Equal(3,json.RootElement.GetProperty("canTiepNhan").GetInt32());
        var items=json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(6,items.Count);
        Assert.All(items,x=>Assert.DoesNotContain($"/Details/{ids["foreign"]}",x.GetProperty("duongDan").GetString()!));
        Assert.Equal(6,Regex.Matches(page,"data-notice-id=").Count);
        Assert.DoesNotContain($"data-notice-id=\"{ids["foreign"]}\"",page);
        Assert.Equal(3,Regex.Matches(await client.GetStringAsync("/ThongBaoQuanLy?trangThai=MOI"),"data-notice-id=").Count);
        Assert.Equal(2,Regex.Matches(await client.GetStringAsync("/ThongBaoQuanLy?canhBao=QUA_HAN"),"data-notice-id=").Count);
        var empty=WebUtility.HtmlDecode(await client.GetStringAsync("/ThongBaoQuanLy?toaNhaId=4"));
        Assert.Contains("Không có thông báo phù hợp",empty);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/ThongBaoQuanLy?toaNhaId=2")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/ThongBaoQuanLy?page=bad")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/ThongBao/DanhSach")).StatusCode);
        using var tenant=await FilterLogin(web,password,"tenant@test.local");
        Assert.Equal(HttpStatusCode.Forbidden,(await tenant.GetAsync("/ThongBaoQuanLy")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await tenant.GetAsync("/ThongBaoQuanLy/TomTat")).StatusCode);
        f.Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/ThongBaoQuanLy")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/ThongBaoQuanLy/TomTat")).StatusCode);
    }
}
