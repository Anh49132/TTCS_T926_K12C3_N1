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
    [Fact] public async Task RoommateHttpDemoCsrfAndOwnership()
    {
        Existing(start:"2026-10-01");
        using(var c=Open()){using var cmd=c.CreateCommand();cmd.CommandText="UPDATE tai_khoan SET mat_khau=$hash";cmd.Parameters.AddWithValue("$hash",BCrypt.Net.BCrypt.HashPassword("RoommateTest!2026",4));cmd.ExecuteNonQuery();}
        using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(builder=>{
            builder.UseContentRoot(Path.GetDirectoryName(Path.GetDirectoryName(seed))!);
            builder.UseEnvironment("Development");builder.UseSetting("DatabasePath",path);
            builder.ConfigureLogging(x=>x.ClearProviders());builder.ConfigureServices(x=>{x.AddDataProtection().UseEphemeralDataProtectionProvider();x.AddSingleton<ITimeProvider>(new FormClock());});
        });
        static string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        async Task<HttpClient> Login(string email){var client=factory.CreateClient(new(){AllowAutoRedirect=false});var page=await client.GetStringAsync("/Account/Login");Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"TaiKhoanDangNhap",email},{"MatKhau","RoommateTest!2026"},{"__RequestVerificationToken",Token(page)}}))).StatusCode);return client;}
        using var owner=await Login("owner@s301.test");
        var addRedirect=await owner.GetAsync("/HopDong/ThemKhachThue?hopDongId=8");
        Assert.Equal(HttpStatusCode.Redirect, addRedirect.StatusCode);
        Assert.Equal("/HopDong/Details/8?tab=people", addRedirect.Headers.Location!.OriginalString);
        var addResponse=await owner.GetAsync(addRedirect.Headers.Location);
        var addPage=WebUtility.HtmlDecode(await addResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        Assert.Contains("Thông tin người ở ghép",addPage);Assert.Contains("Input.SoGiayTo",addPage);
        Assert.Contains("/HopDong/ThemNguoi/8",addPage);
        var overview=WebUtility.HtmlDecode(await owner.GetStringAsync("/HopDong/Details/8"));Assert.Contains("Chi tiết hợp đồng thuê",overview);Assert.Contains("tab=people",overview);Assert.Contains("Chỉ số bàn giao đầu kỳ",overview);
        var page=await owner.GetStringAsync("/HopDong/Details/8?tab=people");
        var readable=WebUtility.HtmlDecode(page);Assert.Contains("Người đứng tên duy nhất",readable);Assert.Contains("1/2 người",readable);Assert.Contains("Input.SoGiayTo",page);
        var fields=new Dictionary<string,string>{{"Input.HoTen","Bạn cùng phòng"},{"Input.SoDienThoai","0907654321"},{"Input.SoGiayTo","123456789012"},{"Input.NgayVao","2026-10-07"},{"Input.PhienBanPhong","0"}};
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync("/HopDong/ThemNguoi/8",new FormUrlEncodedContent(fields))).StatusCode);
        fields["__RequestVerificationToken"]=Token(page);
        Assert.Equal(HttpStatusCode.Redirect,(await owner.PostAsync("/HopDong/ThemNguoi/8",new FormUrlEncodedContent(fields))).StatusCode);
        page=await owner.GetStringAsync("/HopDong/Details/8?tab=people");readable=WebUtility.HtmlDecode(page);Assert.Contains("2/2 người",readable);Assert.Contains("Bạn cùng phòng",readable);Assert.Contains("nhận lại tiền cọc",readable);
        fields["__RequestVerificationToken"]=Token(page);fields["Input.PhienBanPhong"]="1";fields["Input.SoGiayTo"]="987654321";
        var rejected=await owner.PostAsync("/HopDong/ThemNguoi/8",new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.OK,rejected.StatusCode);Assert.Contains("tối đa 2 người",WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync()));
        using var other=await Login("other@s301.test");Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync("/HopDong/Details/8")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync("/HopDong/ThemKhachThue?hopDongId=8")).StatusCode);
        fields["__RequestVerificationToken"]=Token(await other.GetStringAsync("/Account/ChangePassword"));
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsync("/HopDong/ThemNguoi/8",new FormUrlEncodedContent(fields))).StatusCode);
        using var check=Context();Assert.Single(await check.NguoiOGheps.ToListAsync());
    }
}
