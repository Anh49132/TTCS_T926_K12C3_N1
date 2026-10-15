using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class BaoHongTests
{
    private static string OverdueToken(string html, string name) => WebUtility.HtmlDecode(
        Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

    [Fact]
    public async Task OverdueHttpRendersOnlyEligibleReportsAndRefreshesAfterTimeAndStatusChange()
    {
        using var f = new Fixture(); Managers(f);
        var password = Guid.NewGuid().ToString("N") + "Aa1!";
        f.Execute($"UPDATE tai_khoan SET mat_khau='{BCrypt.Net.BCrypt.HashPassword(password, 4)}'");
        int urgent, recent, exact;
        using (var db = f.Context())
        {
            var sender = new BaoHongService(db, f.Time);
            urgent = (await sender.GuiAsync(2, Input(level: "GAP"))).Id;
            await sender.GuiAsync(2, Input());
            foreach (var state in new[] { "DANG_XU_LY", "HOAN_THANH", "TU_CHOI" })
            {
                var id = (await sender.GuiAsync(2, Input(level: "GAP"))).Id;
                await Management(db, f).CapNhatAsync(5, id, Update(state == "HOAN_THANH" ? "DANG_XU_LY" : state));
                if (state == "HOAN_THANH") await Management(db, f).CapNhatAsync(5, id, Update(state, 1));
            }
            f.Time.UtcNow = f.Time.UtcNow.AddHours(2);
            exact = (await sender.GuiAsync(2, Input(level: "GAP"))).Id;
            f.Time.UtcNow = f.Time.UtcNow.AddHours(1);
            recent = (await sender.GuiAsync(2, Input(level: "GAP"))).Id;
        }
        f.Time.UtcNow = f.Time.UtcNow.AddHours(23);
        var root = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(root, "QL_PhongTro", "Data"))) root = Directory.GetParent(root)!.FullName;
        using var web = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(Path.Combine(root, "QL_PhongTro"));
            builder.UseSetting("DatabasePath", f.Path);
            builder.UseSetting("RoomImagesPath", f.Path + ".room-images");
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(x => x.ClearProviders());
            builder.ConfigureServices(x =>
            {
                x.AddDataProtection().UseEphemeralDataProtectionProvider();
                x.RemoveAll<ITimeProvider>(); x.AddSingleton<ITimeProvider>(f.Time);
            });
        });
        async Task<HttpClient> Login(string email)
        {
            var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var form = await client.GetStringAsync("/Account/Login");
            var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TaiKhoanDangNhap"] = email, ["MatKhau"] = password,
                ["__RequestVerificationToken"] = OverdueToken(form, "__RequestVerificationToken")
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); return client;
        }
        using var client = await Login("manager@test.local");
        var audits = f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong");
        var list = WebUtility.HtmlDecode(await client.GetStringAsync("/QuanLyBaoHong"));
        Assert.Single(Regex.Matches(list, "class=\"repair-overdue-label\""));
        Assert.Contains("Gấp — Quá 24 giờ", list);
        Assert.Contains("26 giờ 0 phút", list);
        Assert.Contains("repairs-card-overdue", list);
        var url = $"/QuanLyBaoHong/Details/{urgent}";
        var detailResponse = await client.GetAsync(url);
        Assert.True(detailResponse.Headers.CacheControl!.NoStore);
        var detail = WebUtility.HtmlDecode(await detailResponse.Content.ReadAsStringAsync());
        Assert.Contains("Gấp — Quá 24 giờ", detail); Assert.Contains("26 giờ 0 phút", detail);
        Assert.DoesNotContain("class=\"repair-overdue-label\"", await client.GetStringAsync($"/QuanLyBaoHong/Details/{exact}"));
        Assert.DoesNotContain("class=\"repair-overdue-label\"", await client.GetStringAsync($"/QuanLyBaoHong/Details/{recent}"));
        Assert.Equal(audits, f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong"));
        f.Time.UtcNow = f.Time.UtcNow.AddMinutes(1);
        Assert.Contains("class=\"repair-overdue-label\"", await client.GetStringAsync($"/QuanLyBaoHong/Details/{exact}"));
        Assert.Equal(2, Regex.Matches(await client.GetStringAsync("/QuanLyBaoHong"), "class=\"repair-overdue-label\"").Count);
        using var other = await Login("manager2@test.local");
        Assert.DoesNotContain("repair-overdue-label", await other.GetStringAsync("/QuanLyBaoHong"));
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(url)).StatusCode);
        using var tenant = await Login("tenant@test.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync(url)).StatusCode);
        var post = new Dictionary<string, string>
        {
            ["CapNhat.PhienBan"] = OverdueToken(detail, "CapNhat.PhienBan"),
            ["CapNhat.KhoaChongTrung"] = OverdueToken(detail, "CapNhat.KhoaChongTrung"),
            ["CapNhat.TrangThai"] = "DANG_XU_LY", ["CapNhat.GhiChu"] = "Bắt đầu xử lý báo hỏng quá hạn"
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/QuanLyBaoHong/CapNhat/{urgent}", new FormUrlEncodedContent(post))).StatusCode);
        post["__RequestVerificationToken"] = OverdueToken(detail, "__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/QuanLyBaoHong/CapNhat/{urgent}", new FormUrlEncodedContent(post))).StatusCode);
        Assert.DoesNotContain("repair-overdue-label", await client.GetStringAsync(url));
        list = await client.GetStringAsync("/QuanLyBaoHong");
        Assert.Single(Regex.Matches(list, "class=\"repair-overdue-label\""));
    }
}
