using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    private async Task<HttpResponseMessage> ChangeManagedRole(HttpClient client, int id, string original, string role)
    {
        var html = await client.GetStringAsync("/ManagedAccounts/ChangeRole/" + id);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        return await client.PostAsync("/ManagedAccounts/ChangeRole", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = id.ToString(), ["OriginalRole"] = original, ["VaiTro"] = role,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
    }

    [Fact]
    public async Task ManagedRole_ChangesRoleAuditsAndRevokesOldCookieAndRefresh()
    {
        using var tenant = await Login("KHACH_THUE");
        using var admin = await Login("ADMIN");
        var loginResponse = await tenant.PostAsJsonAsync("/api/auth/login", new { TaiKhoanDangNhap = "khach_thue@s104.test", MatKhau = "DemoPass123!" });
        using var loginBody = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var access = loginBody.RootElement.GetProperty("accessToken").GetString();
        var refresh = loginBody.RootElement.GetProperty("refreshToken").GetString();
        var id = accounts["KHACH_THUE"];

        var response = await ChangeManagedRole(admin, id, "KHACH_THUE", "ADMIN");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("ADMIN", Scalar("SELECT vai_tro FROM tai_khoan WHERE id=$id", ("$id", id)));
        Assert.Equal(DBNull.Value, Scalar("SELECT refresh_token_hash FROM tai_khoan WHERE id=$id", ("$id", id)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM account_session_version WHERE account_id=$id", ("$id", id)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='tai_khoan' AND doi_tuong_id=$id AND du_lieu_sau LIKE '%ADMIN%'", ("$id", id)));
        Assert.Equal(HttpStatusCode.Redirect, (await tenant.GetAsync("/HoSo")).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);
        Assert.Equal(HttpStatusCode.Unauthorized, (await tenant.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await tenant.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refresh })).StatusCode);
    }

    [Theory]
    [InlineData("KHACH_THUE")]
    [InlineData("CHU_NHA")]
    [InlineData("QUAN_LY")]
    public async Task ManagedRole_NonAdminForbidden(string role)
    {
        using var client = await Login(role);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/ManagedAccounts/Create")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/ManagedAccounts/Create", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/ManagedAccounts/ChangeRole/" + accounts["ADMIN"])).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/ManagedAccounts/ChangeRole", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
    }

    [Theory]
    [InlineData("KHACH_THUE", "INVALID", "KHACH_THUE")]
    [InlineData("KHACH_THUE", "ADMIN", "QUAN_LY")]
    [InlineData("ADMIN", "CHU_NHA", "ADMIN")]
    public async Task ManagedRole_InvalidStaleOrSelfDoesNotChange(string source, string target, string original)
    {
        using var admin = await Login("ADMIN");
        var response = await ChangeManagedRole(admin, accounts[source], original, target);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(source, Scalar("SELECT vai_tro FROM tai_khoan WHERE id=$id", ("$id", accounts[source])));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM account_session_version"));
    }

    [Theory]
    [InlineData("CHU_NHA")]
    [InlineData("QUAN_LY")]
    public async Task ManagedRole_AssignedBuildingPreventsRoleChange(string role)
    {
        Execute("INSERT INTO toa_nha(chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES ($owner,$manager,'Role test','Test address')", ("$owner",accounts["CHU_NHA"]), ("$manager",accounts["QUAN_LY"]));
        using var admin = await Login("ADMIN");
        Assert.Equal(HttpStatusCode.OK, (await ChangeManagedRole(admin, accounts[role], role, "ADMIN")).StatusCode);
        Assert.Equal(role, Scalar("SELECT vai_tro FROM tai_khoan WHERE id=$id", ("$id",accounts[role])));
    }

    [Fact]
    public async Task ManagedRole_MissingCsrfRejected()
    {
        using var admin = await Login("ADMIN");
        var response = await admin.PostAsync("/ManagedAccounts/ChangeRole", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["Id"] = accounts["KHACH_THUE"].ToString(), ["OriginalRole"] = "KHACH_THUE", ["VaiTro"] = "ADMIN" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ManagedRole_AuditFailureRollsBackRoleAndRevocation()
    {
        using var admin = await Login("ADMIN");
        Execute("CREATE TRIGGER test_role_audit_failure BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test'); END;");
        try { await ChangeManagedRole(admin, accounts["KHACH_THUE"], "KHACH_THUE", "ADMIN"); } catch (Exception) { }
        Assert.Equal("KHACH_THUE", Scalar("SELECT vai_tro FROM tai_khoan WHERE id=$id", ("$id", accounts["KHACH_THUE"])));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM account_session_version"));
    }

    [Fact]
    public async Task ManagedAccount_CreateAdminRequiresPasswordChangeAndSupportsResend()
    {
        using var configured = factory.WithWebHostBuilder(b => b.UseSetting("PasswordReset:PickupDirectory", Path.Combine(temp, "emails")));
        using var admin = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(admin, "ADMIN");
        var html = await admin.GetStringAsync("/ManagedAccounts/Create");
        Assert.Contains("value=\"ADMIN\"", html);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var response = await admin.PostAsync("/ManagedAccounts/Create", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["HoTen"] = "New Admin", ["Email"] = "newadmin@s104.test", ["SoDienThoai"] = "0907654321", ["VaiTro"] = "ADMIN",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var id = Convert.ToInt32(Scalar("SELECT id FROM tai_khoan WHERE email='newadmin@s104.test'"));
        Assert.Equal("ADMIN", Scalar("SELECT vai_tro FROM tai_khoan WHERE id=$id", ("$id",id)));
        Assert.Equal(1L, Scalar("SELECT must_change_password FROM tai_khoan WHERE id=$id", ("$id",id)));
        var oldHash = Scalar("SELECT mat_khau FROM tai_khoan WHERE id=$id", ("$id",id));
        html = await admin.GetStringAsync("/ManagedAccounts");
        token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        response = await admin.PostAsync("/ManagedAccounts/Resend", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["id"] = id.ToString(), ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotEqual(oldHash, Scalar("SELECT mat_khau FROM tai_khoan WHERE id=$id", ("$id",id)));
    }
}
