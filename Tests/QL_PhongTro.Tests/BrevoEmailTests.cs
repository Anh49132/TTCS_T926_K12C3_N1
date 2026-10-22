using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class BrevoEmailTests
{
    private static PasswordResetOptions Settings() => new() { Provider = "Brevo", From = "sender@example.test", PublicBaseUrl = "https://nhatot.example.test" };

    [Fact]
    public async Task AllBusinessSendersUseBrevoAndPreserveVietnameseTextAndLinks()
    {
        using var handler = new Capture();
        using var client = new HttpClient(handler);
        var transport = new BrevoEmailTransport(client, Options.Create(new BrevoOptions { ApiKey = "fake-test-key" }));
        var sender = new PasswordEmailSender(Options.Create(Settings()), new EnvironmentStub(), transport);
        await sender.SendAsync("tenant@example.test", "https://nhatot.example.test/Account/ResetPassword?token=fake");
        Assert.Equal(EmailDeliveryMode.Brevo, (await sender.SendConfirmationAsync("tenant@example.test", "Khách", "123456")).Mode);
        await sender.SendTemporaryAsync("tenant@example.test", "Khách", "CHU_NHA", "fake-password");
        await sender.SendConfirmedAsync("tenant@example.test", "Khách", "YC-TEST", new(2026, 10, 22, 14, 30, 0), 1);
        await sender.SendRescheduledAsync("tenant@example.test", "Khách", "YC-TEST", new(2026, 10, 22, 15, 30, 0), 1);
        await sender.SendNewRequestAsync("tenant@example.test", "Chủ nhà", "YC-TEST", "Khách", "101", "XEM_PHONG", new(2026,10,22), 1);
        await sender.SendIssuedAsync("tenant@example.test", "Hóa đơn", "Tiền phòng", "/ThongBao/HoaDon/1");
        await sender.SendRepairAsync("tenant@example.test", "Báo hỏng", "Đã xử lý", "/BaoHong/Details/1");
        await sender.SendPaymentAsync("tenant@example.test", "Thanh toán", "Đã thu", "/ThongBao/HoaDon/1");
        var invoices = new InvoiceEmailSender(Options.Create(Settings()), new EnvironmentStub(), transport);
        await invoices.SendInvoiceIssuedAsync("tenant@example.test", "Khách", "HD-1", 10, 2026, 2000000, new(2026,10,29), "https://nhatot.example.test/ThongBao/HoaDon/1");
        await invoices.SendDueSoonAsync("tenant@example.test", "Khách", "HD-1", 10, 2026, 2000000, 1000000, new(2026,10,29), "https://nhatot.example.test/ThongBao/HoaDon/1");
        await invoices.SendOverdueAsync("tenant@example.test", "Khách", "HD-1", 10, 2026, 2000000, 1000000, new(2026,10,29), "https://nhatot.example.test/ThongBao/HoaDon/1");
        Assert.Equal(12, handler.Bodies.Count);
        Assert.Contains("123456", handler.Bodies[1]);
        Assert.Contains("https://nhatot.example.test/ThongBao/HoaDon/1", handler.Bodies[6]);
    }

    [Theory]
    [InlineData(401)] [InlineData(403)] [InlineData(429)] [InlineData(500)] [InlineData(302)]
    public async Task ProviderFailureIsSanitizedAndNeverRetriedOrReportedAsSent(int status)
    {
        using var handler = new Capture { Status = (HttpStatusCode)status };
        using var client = new HttpClient(handler);
        var sender = new PasswordEmailSender(Options.Create(Settings()), new EnvironmentStub(),
            new(client, Options.Create(new BrevoOptions { ApiKey = "fake-test-key" })));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendConfirmationAsync("tenant@example.test", "Khách", "123456"));
        Assert.DoesNotContain("fake-test-key", error.ToString());
        Assert.DoesNotContain("provider-secret", error.ToString());
        Assert.DoesNotContain("tenant@example.test", error.ToString());
        Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task MissingKeyAndProductionPickupCannotSend()
    {
        using var handler = new Capture();
        using var client = new HttpClient(handler);
        var settings = Settings();
        var sender = new PasswordEmailSender(Options.Create(settings), new EnvironmentStub(), new(client, Options.Create(new BrevoOptions())));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("tenant@example.test", "https://example.test"));
        settings.PickupDirectory = "must-not-be-created";
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("tenant@example.test", "https://example.test"));
        Assert.Empty(handler.Bodies);
    }

    private sealed class Capture : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        public HttpStatusCode Status { get; init; } = HttpStatusCode.Created;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("https://api.brevo.com/v3/smtp/email", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("fake-test-key", Assert.Single(request.Headers.GetValues("api-key")));
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("sender@example.test", json.RootElement.GetProperty("sender").GetProperty("email").GetString());
            Assert.Equal("NhàTốt", json.RootElement.GetProperty("sender").GetProperty("name").GetString());
            Assert.Equal("tenant@example.test", json.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
            Bodies.Add(json.RootElement.GetProperty("textContent").GetString()!);
            return new(Status) { Content = new StringContent(Status == HttpStatusCode.Created ? "{\"messageId\":\"fake-id\"}" : "provider-secret") };
        }
    }
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
