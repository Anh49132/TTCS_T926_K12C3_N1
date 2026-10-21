using System.Net;
using System.Text.RegularExpressions;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class MeterReadingListTests
{
    private async Task<Dictionary<string,string>> MeterForm(HttpClient client)
    {
        var html=await client.GetStringAsync("/ChiSoDienNuoc?toaNhaId=1");
        var form=Regex.Matches(html,"(?s)<form[^>]*data-meter-form.*?</form>").Cast<Match>().Single(x=>x.Value.Contains("name=\"PhongId\" value=\"1\""));
        return Regex.Matches(form.Value,"<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"[^>]*>").Cast<Match>()
            .ToDictionary(x=>x.Groups[1].Value,x=>WebUtility.HtmlDecode(x.Groups[2].Value));
    }
    [Fact] public async Task SaveHttpFieldErrorsCorrectionReloadCsrfAndForgedPost()
    {
        MeterFixture();using var factory=Factory();using var manager=await Login(factory,"manager@meter.test");
        var fields=await MeterForm(manager);fields["DienMoi"]="-1";fields["NuocMoi"]="12";
        var response=await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var html=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());Assert.Contains("meter-1-DIEN-error",html);Assert.Contains("meter-1-NUOC-error",html);Assert.Contains("is-invalid",html);Assert.Equal(0,Count("chi_so_dien_nuoc"));
        fields["DienMoi"]="1";fields["NuocMoi"]="13.125";fields["previousReading"]="9999";
        var noToken=new Dictionary<string,string>(fields);noToken.Remove("__RequestVerificationToken");Assert.Equal(HttpStatusCode.BadRequest,(await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(noToken))).StatusCode);
        var forged=new Dictionary<string,string>(fields){["HopDongId"]="6"};Assert.Equal(HttpStatusCode.Forbidden,(await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(forged))).StatusCode);
        using var other=await Login(factory,"other@meter.test");var otherFields=await MeterForm(manager);otherFields["DienMoi"]="1";otherFields["NuocMoi"]="13";
        Assert.NotEqual(HttpStatusCode.Redirect,(await other.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(otherFields))).StatusCode);
        var otherPage=await other.GetStringAsync("/Account/ChangePassword");
        otherFields["__RequestVerificationToken"]=WebUtility.HtmlDecode(Regex.Match(otherPage,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(otherFields))).StatusCode);
        response=await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        html=WebUtility.HtmlDecode(await manager.GetStringAsync(response.Headers.Location));Assert.Contains("Đã chốt",html);Assert.Contains("Chưa chốt",html);Assert.DoesNotContain("is-invalid",html);
        Assert.Equal(2,Count("chi_so_dien_nuoc"));var reload=await MeterForm(manager);Assert.Equal("13.125",reload["NuocMoi"]);
        Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='QUAN_LY' AND ModuleCode='DIEN_NUOC'");
        Assert.Equal(HttpStatusCode.Forbidden,(await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(reload))).StatusCode);
        Execute("UPDATE role_permission SET AccessLevel='WRITE' WHERE RoleCode='QUAN_LY' AND ModuleCode='DIEN_NUOC'");
        reload["DienMoi"]="bad";response=await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(reload));Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal(2,Count("chi_so_dien_nuoc"));
    }
    [Fact] public async Task MeterUiAllowsExplicitBaselineWhenReferenceMissing()
    {
        MeterFixture(waterMeter:false,handover:false);using var factory=Factory();using var manager=await Login(factory,"manager@meter.test");
        var html=WebUtility.HtmlDecode(await manager.GetStringAsync("/ChiSoDienNuoc?toaNhaId=1"));
        Assert.Contains("name=\"DienDau\"",html);Assert.DoesNotContain("name=\"NuocDau\"",html);
        var fields=await MeterForm(manager);fields["DienMoi"]="110";fields["DienDau"]="100";
        var response=await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);Assert.Equal(1,Count("chi_so_dien_nuoc"));
    }
    [Fact] public async Task StaleHttpFormKeepsOldVersionUntilExplicitReload()
    {
        MeterFixture();using var factory=Factory();using var manager=await Login(factory,"manager@meter.test");
        var fields=await MeterForm(manager);fields["DienMoi"]="1";fields["NuocMoi"]="13";
        Assert.Equal(HttpStatusCode.Redirect,(await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields))).StatusCode);
        var response=await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var html=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());Assert.Contains("Chỉ số đã thay đổi",html);
        Assert.Contains("name=\"DienPhienBan\" value=\"-1\"",html);
        Assert.Equal("0",(await MeterForm(manager))["DienPhienBan"]);Assert.Equal(2,Count("chi_so_dien_nuoc"));
    }

    [Fact]
    public async Task PublishedInvoiceDisablesMeterEditingAndPostReturnsLockMessage()
    {
        MeterFixture();
        Invoice(1, 1, 10, issued: true);
        using var factory = Factory();
        using var manager = await Login(factory, "manager@meter.test");

        var html = WebUtility.HtmlDecode(await manager.GetStringAsync("/ChiSoDienNuoc?toaNhaId=1"));
        Assert.Contains("đã khóa", html);
        Assert.Contains("Đã khóa", html);
        Assert.Contains("readonly=\"readonly\"", html);
        Assert.Contains("disabled=\"disabled\"", html);

        var fields = await MeterForm(manager);
        fields["DienMoi"] = "10";
        fields["NuocMoi"] = "20";
        var response = await manager.PostAsync("/ChiSoDienNuoc/Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Không thể thêm hoặc sửa chỉ số", html);
        Assert.Equal(0, Count("chi_so_dien_nuoc"));
    }
}
