using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public partial class AuthTests
{
    private Task<(int statusCode, System.Text.Json.JsonElement body)> Attempt(string password = "wrong") =>
        PostJsonAsync("/api/auth/login", new { TaiKhoanDangNhap = "test@example.com", MatKhau = password });

    [Fact]
    public async Task MvcFifthFailureReportsLockFlagsAndSharesApiWindow()
    {
        CreateUser();
        var html = await _client.GetStringAsync("/Account/Login");
        var token = System.Text.RegularExpressions.Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        for (var i = 0; i < 5; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["TaiKhoanDangNhap"] = "test@example.com", ["MatKhau"] = "wrong",
                    ["__RequestVerificationToken"] = System.Net.WebUtility.HtmlDecode(token)
                })
            };
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            using var response = await _client.SendAsync(request);
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(i == 4, json.RootElement.GetProperty("locked").GetBoolean());
            if (i == 4) Assert.Equal(15, json.RootElement.GetProperty("remainingMinutes").GetInt32());
        }
        Assert.Equal(401, (await Attempt("Test123456")).statusCode);
    }

    [Fact]
    public async Task FifthWrongPasswordImmediatelyReportsFifteenMinutes()
    {
        CreateUser();
        for (var i = 0; i < 4; i++) Assert.Equal(401, (await Attempt()).statusCode);
        var fifth = await Attempt();
        Assert.Equal(401, fifth.statusCode);
        Assert.Contains("15 phút", fifth.body.GetProperty("message").GetString());
        Assert.Equal(5, (await GetUserAsync(1))!.FailedLoginCount);
    }

    [Fact]
    public async Task FailuresOutsideFifteenMinutesDoNotAccumulate()
    {
        var user = CreateUser();
        for (var i = 0; i < 4; i++) await Attempt();
        _mockTimeProvider.UtcNow = _mockTimeProvider.UtcNow.AddMinutes(15);
        await Attempt();
        var saved = await GetUserAsync(user.Id);
        Assert.Equal(1, saved!.FailedLoginCount);
        Assert.Null(saved.LockedUntil);
        Assert.Equal(200, (await Attempt("Test123456")).statusCode);
    }

    [Fact]
    public async Task RollingWindowIncludesFailuresAcrossItsFirstExpiredAttempt()
    {
        var user = CreateUser();
        await Attempt();
        _mockTimeProvider.UtcNow = _mockTimeProvider.UtcNow.AddMinutes(10);
        for (var i = 0; i < 3; i++) await Attempt();
        _mockTimeProvider.UtcNow = _mockTimeProvider.UtcNow.AddMinutes(6);
        await Attempt();
        Assert.Null((await GetUserAsync(user.Id))!.LockedUntil);
        Assert.Contains("15 phút", (await Attempt()).body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ExpiredLockIsNotExtendedByPollingOrNextFailedAttempt()
    {
        var user = CreateUser();
        for (var i = 0; i < 5; i++) await Attempt();
        var until = (await GetUserAsync(user.Id))!.LockedUntil;
        using var scope = _factory.Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ICredentialValidationService>();
        _mockTimeProvider.UtcNow = _mockTimeProvider.UtcNow.AddMinutes(1);
        Assert.Equal((true, 14), await validator.CheckLockoutAsync(user.Email));
        Assert.Equal(until, (await GetUserAsync(user.Id))!.LockedUntil);
        _mockTimeProvider.UtcNow = _mockTimeProvider.UtcNow.AddMinutes(14);
        Assert.Equal((false, 0), await validator.CheckLockoutAsync(user.Email));
        await Attempt();
        var saved = await GetUserAsync(user.Id);
        Assert.Null(saved!.LockedUntil);
        Assert.Equal(1, saved.FailedLoginCount);
    }

    [Fact]
    public async Task SuccessfulLoginClearsRecentFailures()
    {
        var user = CreateUser();
        for (var i = 0; i < 4; i++) await Attempt();
        Assert.Equal(200, (await Attempt("Test123456")).statusCode);
        await Attempt();
        var saved = await GetUserAsync(user.Id);
        Assert.Equal(1, saved!.FailedLoginCount);
        Assert.Null(saved.LockedUntil);
    }

    [Fact]
    public async Task ConcurrentFailuresAreCountedWithoutLostUpdates()
    {
        var user = CreateUser();
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Attempt()));
        Assert.All(results, result => Assert.Equal(401, result.statusCode));
        Assert.Equal(5, (await GetUserAsync(user.Id))!.FailedLoginCount);
        Assert.Equal(401, (await Attempt("Test123456")).statusCode);
    }
}
