using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class ContractCreationTests
{
    [Fact] public async Task AccountResidentHttpLookupCsrfMaskingLinkAndAutomaticDate()
    {
        AccountResident();using(var c=Open()){using var cmd=c.CreateCommand();cmd.CommandText="UPDATE tai_khoan SET mat_khau=$hash";cmd.Parameters.AddWithValue("$hash",BCrypt.Net.BCrypt.HashPassword("ResidentTest!2026",4));cmd.ExecuteNonQuery();}
        using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(builder=>{
            builder.UseContentRoot(Path.GetDirectoryName(Path.GetDirectoryName(seed))!);builder.UseEnvironment("Development");builder.UseSetting("DatabasePath",path);
            builder.ConfigureLogging(x=>x.ClearProviders());builder.ConfigureServices(x=>{x.AddDataProtection().UseEphemeralDataProtectionProvider();x.AddSingleton<ITimeProvider>(new FormClock());});
        });
        static string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        async Task<HttpClient> Login(string email){var client=factory.CreateClient(new(){AllowAutoRedirect=false});var page=await client.GetStringAsync("/Account/Login");Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"TaiKhoanDangNhap",email},{"MatKhau","ResidentTest!2026"},{"__RequestVerificationToken",Token(page)}}))).StatusCode);return client;}
        using var owner=await Login("owner@s301.test");var initial=await owner.GetStringAsync("/HopDong/Details/8?tab=people");
        var decoded=WebUtility.HtmlDecode(initial);Assert.Contains("Có tài khoản",decoded);Assert.Contains("Chưa có tài khoản",decoded);Assert.DoesNotContain("name=\"Input.NgayVao\"",initial);
        var search=new Dictionary<string,string>{{"taiKhoanId","3"}};
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync("/HopDong/TimNguoiOGhep/8",new FormUrlEncodedContent(search))).StatusCode);
        search["__RequestVerificationToken"]=Token(initial);
        var response=await owner.PostAsync("/HopDong/TimNguoiOGhep/8",new FormUrlEncodedContent(search));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var card=await response.Content.ReadAsStringAsync();decoded=WebUtility.HtmlDecode(card);
        Assert.Contains("Account resident",decoded);Assert.Contains("******8888",decoded);Assert.Contains("********1012",decoded);Assert.Contains("Đã xác thực",decoded);
        Assert.DoesNotContain("0908888888",decoded);Assert.DoesNotContain("987654321012",decoded);Assert.DoesNotContain("resident@s301.test",decoded);Assert.DoesNotContain("name=\"Input.HoTen\"",card);
        var add=new Dictionary<string,string>{{"Input.CachThem","CO_TAI_KHOAN"},{"Input.TaiKhoanId","3"},{"Input.PhienBanPhong","0"},{"Input.NgayVao","not-a-date"}};
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync("/HopDong/ThemNguoi/8",new FormUrlEncodedContent(add))).StatusCode);
        using(var other=await Login("other@s301.test")){search["__RequestVerificationToken"]=Token(await other.GetStringAsync("/Account/ChangePassword"));Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsync("/HopDong/TimNguoiOGhep/8",new FormUrlEncodedContent(search))).StatusCode);}
        add["__RequestVerificationToken"]=Token(card);Assert.Equal(HttpStatusCode.Redirect,(await owner.PostAsync("/HopDong/ThemNguoi/8",new FormUrlEncodedContent(add))).StatusCode);
        using var check=Context();var stay=await check.NguoiOGheps.SingleAsync();Assert.Equal(3,stay.KhachThueId);Assert.Equal(new DateOnly(2026,10,7),stay.NgayVao);Assert.Equal(2,await check.KhachThues.CountAsync());
    }
}
