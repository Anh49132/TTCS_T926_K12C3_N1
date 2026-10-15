using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class MeterReadingListTests
{
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseContentRoot(app); builder.UseEnvironment("Development"); builder.UseSetting("DatabasePath",path);
        builder.ConfigureLogging(x=>x.ClearProviders());
        builder.ConfigureServices(x=>{x.AddDataProtection().UseEphemeralDataProtectionProvider();x.AddSingleton<ITimeProvider>(clock);});
    });
    private static async Task<HttpClient> Login(WebApplicationFactory<Program> factory,string email)
    {
        var client=factory.CreateClient(new(){AllowAutoRedirect=false}); var html=await client.GetStringAsync("/Account/Login");
        var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>
        {{"TaiKhoanDangNhap",email},{"MatKhau",Password},{"__RequestVerificationToken",token}}))).StatusCode);
        return client;
    }
    [Fact] public async Task HttpMenuReadOnlyHtmlAndGetPreservesDatabase()
    {
        Handover();using var factory=Factory();using var manager=await Login(factory,"manager@meter.test");
        // Login legitimately writes authentication state; compare only after login.
        string Snapshot()
        {using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT group_concat(name || ':' || sql,'|') FROM sqlite_master";var schema=cmd.ExecuteScalar()?.ToString();
         var tables=new List<string>();cmd.CommandText="SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";using(var reader=cmd.ExecuteReader()){while(reader.Read())tables.Add(reader.GetString(0));}
         var rows=new List<string>{schema??""};foreach(var table in tables){cmd.CommandText="SELECT * FROM \""+table.Replace("\"","\"\"")+"\" ORDER BY rowid";using var reader=cmd.ExecuteReader();while(reader.Read()){var values=new object[reader.FieldCount];reader.GetValues(values);rows.Add(System.Text.Json.JsonSerializer.Serialize(values));}}return string.Join("\n",rows);}
        var before=Snapshot();var response=await manager.GetAsync("/ChiSoDienNuoc?toaNhaId=1");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("no-store",response.Headers.CacheControl!.ToString());var html=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(before,Snapshot());Assert.Contains("id=\"meter-title\">Chỉ số điện nước",html);Assert.Contains("10/2026",html);Assert.Contains("Chỉ số bàn giao",html);
        Assert.Contains("Chưa có dữ liệu",html);Assert.Contains("Chưa chốt",html);Assert.Contains("<span>0</span>",html);
        Assert.DoesNotContain("Foreign building",html);Assert.DoesNotContain("SECRET",html);Assert.DoesNotContain("EMPTY",html);
        Assert.True(html.IndexOf("A001",StringComparison.Ordinal)<html.IndexOf("A002",StringComparison.Ordinal));
        var content=html[html.IndexOf("<section aria-labelledby=\"meter-title\"",StringComparison.Ordinal)..];
        Assert.DoesNotContain("type=\"number\"",content);Assert.DoesNotContain("method=\"post\"",content);Assert.DoesNotContain("Chỉ số mới",content);
        Assert.Contains("href=\"/ChiSoDienNuoc\"",html);
        var legacy=await manager.GetAsync("/Modules/DIEN_NUOC");Assert.Equal(HttpStatusCode.Redirect,legacy.StatusCode);Assert.Equal("/ChiSoDienNuoc",legacy.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden,(await manager.GetAsync("/ChiSoDienNuoc?toaNhaId=2")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await manager.GetAsync("/ChiSoDienNuoc?toaNhaId=bad")).StatusCode);
    }
    [Fact] public async Task HttpRolePermissionAndAssignmentChecks()
    {
        using var factory=Factory();using var guest=factory.CreateClient(new(){AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.Redirect,(await guest.GetAsync("/ChiSoDienNuoc")).StatusCode);
        foreach(var email in new[]{"tenant@meter.test","admin@meter.test"})
        {using var client=await Login(factory,email);Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/ChiSoDienNuoc")).StatusCode);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/Modules/DIEN_NUOC")).StatusCode);}
        using var manager=await Login(factory,"manager@meter.test");using var other=await Login(factory,"other@meter.test");
        Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync("/ChiSoDienNuoc?toaNhaId=1")).StatusCode);
        Execute("UPDATE toa_nha SET quan_ly_id=NULL WHERE id=1");Assert.Equal(HttpStatusCode.Forbidden,(await manager.GetAsync("/ChiSoDienNuoc?toaNhaId=1")).StatusCode);
        Assert.Contains("chưa được phân công",WebUtility.HtmlDecode(await manager.GetStringAsync("/ChiSoDienNuoc")));
        Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='QUAN_LY' AND ModuleCode='DIEN_NUOC'");
        Assert.Equal(HttpStatusCode.Forbidden,(await manager.GetAsync("/ChiSoDienNuoc")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await manager.GetAsync("/Modules/DIEN_NUOC")).StatusCode);
        Execute("DELETE FROM role_permission WHERE RoleCode='QUAN_LY' AND ModuleCode='DIEN_NUOC'");Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync("/ChiSoDienNuoc")).StatusCode);
    }
}
