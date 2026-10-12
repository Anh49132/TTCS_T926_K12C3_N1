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
public sealed partial class ContractCreationTests
{
    [Fact] public async Task HistoryHttpFiltersInvalidEmptyAndOwnership()
    {
        HistoryFixture();using(var c=Open()){using var cmd=c.CreateCommand();cmd.CommandText="UPDATE tai_khoan SET mat_khau=$hash";cmd.Parameters.AddWithValue("$hash",BCrypt.Net.BCrypt.HashPassword("HistoryTest!2026",4));cmd.ExecuteNonQuery();}
        using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(builder=>{
            builder.UseContentRoot(Path.GetDirectoryName(Path.GetDirectoryName(seed))!);builder.UseEnvironment("Development");builder.UseSetting("DatabasePath",path);
            builder.ConfigureLogging(x=>x.ClearProviders());builder.ConfigureServices(x=>{x.AddDataProtection().UseEphemeralDataProtectionProvider();x.AddSingleton<ITimeProvider>(new FormClock());});
        });
        async Task<HttpClient> LoginHistory(string email){var client=factory.CreateClient(new(){AllowAutoRedirect=false});var html=await client.GetStringAsync("/Account/Login");var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"TaiKhoanDangNhap",email},{"MatKhau","HistoryTest!2026"},{"__RequestVerificationToken",token}}))).StatusCode);return client;}
        using var owner=await LoginHistory("owner@s301.test");
        var page=WebUtility.HtmlDecode(await owner.GetStringAsync("/HopDong/LichSuNguoiO?phongId=1&tuNgay=2026-01-31&denNgay=2026-01-31"));
        Assert.Contains("Danh sách lịch sử cư trú",page);Assert.Contains("History roommate",page);Assert.Contains("Người đứng tên",page);Assert.Contains("Người ở ghép",page);Assert.Contains("name=\"TuNgay\"",page);Assert.Contains("name=\"DenNgay\"",page);Assert.Contains("HD-2026-0007",page);Assert.Contains("10/01/2026",page);
        page=WebUtility.HtmlDecode(await owner.GetStringAsync("/HopDong/LichSuNguoiO?phongId=1&tuNgay=2026-10-07&denNgay=2026-10-07"));Assert.Contains("Đang ở",page);
        page=WebUtility.HtmlDecode(await owner.GetStringAsync("/HopDong/LichSuNguoiO?phongId=1&tuNgay=2027-01-01&denNgay=2027-01-02"));Assert.Contains("Không có lượt cư trú",page);Assert.DoesNotContain("History roommate",page);
        page=WebUtility.HtmlDecode(await owner.GetStringAsync("/HopDong/LichSuNguoiO?phongId=1&tuNgay=2026-10-07&denNgay=2026-10-06"));Assert.Contains("Đến ngày không được trước từ ngày",page);Assert.DoesNotContain("History roommate",page);
        page=WebUtility.HtmlDecode(await owner.GetStringAsync("/HopDong/LichSuNguoiO?phongId=1&tuNgay=bad&denNgay=2026-10-07"));Assert.DoesNotContain("History roommate",page);
        using var other=await LoginHistory("other@s301.test");Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync("/HopDong/LichSuNguoiO?phongId=1")).StatusCode);
        using var guest=factory.CreateClient(new(){AllowAutoRedirect=false});Assert.Equal(HttpStatusCode.Redirect,(await guest.GetAsync("/HopDong/LichSuNguoiO?phongId=1")).StatusCode);
        Assert.Contains("LichSuNguoiO",await owner.GetStringAsync("/HopDong/Details/9"));Assert.Contains("LichSuNguoiO",await owner.GetStringAsync("/PhongTro/Details/1"));
    }
}
