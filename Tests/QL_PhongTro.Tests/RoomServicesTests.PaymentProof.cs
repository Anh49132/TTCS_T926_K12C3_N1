using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_PhongTro.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task PaymentProofUploadEnforcesSizeAndViewLinksArePrivateAndExpire()
    {
        int invoiceId;
        using (var db = Context())
        {
            var fixture = await BillingAsync(db);
            await LinkTenantsAsync(db, fixture.B);
            invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "PAYMENT-PROOF-HTTP", false);
        }

        var clock = new MockTimeProvider { UtcNow = new(2026, 10, 12, 17, 30, 0, DateTimeKind.Utc) };
        using var factory = BillingWeb(clock);
        using var tenant = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(tenant, "tenant");

        static string AntiforgeryToken(string html) =>
            WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")
                .Groups[1].Value);

        async Task<MultipartFormDataContent> FormAsync(byte[] bytes, string filename)
        {
            var html = await tenant.GetStringAsync($"/ThongBao/HoaDon/{invoiceId}");
            Assert.Contains("enctype=\"multipart/form-data\"", html);
            Assert.Contains("name=\"anhChungTu\"", html);
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(AntiforgeryToken(html)), "__RequestVerificationToken");
            form.Add(new StringContent(invoiceId.ToString()), "hoaDonId");
            form.Add(new StringContent("250000"), "soTien");
            form.Add(new StringContent("2026-10-13"), "ngayThanhToan");
            form.Add(new StringContent("CHUYEN_KHOAN"), "hinhThucThanhToan");
            var image = new ByteArrayContent(bytes);
            image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(image, "anhChungTu", filename);
            return form;
        }

        using (var tooLarge = await FormAsync(new byte[5 * 1024 * 1024 + 1], "proof.png"))
        {
            var rejected = await tenant.PostAsync("/ThongBao/BaoCaoThanhToan", tooLarge);
            Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
            var error = await tenant.GetStringAsync(rejected.Headers.Location);
            Assert.Contains("5 MiB", WebUtility.HtmlDecode(error));
        }
        using (var verify = Context())
            Assert.Empty(await verify.ThanhToans.ToListAsync());

        byte[] png;
        using (var image = new Image<Rgba32>(1, 1))
        using (var stream = new MemoryStream())
        {
            await image.SaveAsPngAsync(stream);
            png = stream.ToArray();
        }
        byte[] jpeg;
        using (var image = new Image<Rgba32>(1, 1))
        using (var stream = new MemoryStream())
        {
            await image.SaveAsJpegAsync(stream);
            jpeg = stream.ToArray();
        }
        var imageStore = new PaymentProofImageStore(Path.Combine(folder, "proof-validation"));
        foreach (var filename in new[] { "evidence.jpg", "evidence.jpeg" })
        {
            using var stream = new MemoryStream(jpeg);
            var file = new FormFile(stream, 0, jpeg.Length, "anhChungTu", filename);
            Assert.Equal("image/jpeg", (await imageStore.PrepareAsync(file, default))!.ContentType);
        }
        using (var valid = await FormAsync(png, "evidence.png"))
        {
            var accepted = await tenant.PostAsync("/ThongBao/BaoCaoThanhToan", valid);
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        }

        int paymentId;
        using (var verify = Context())
        {
            var report = await verify.ThanhToans.SingleAsync(x => x.HoaDonId == invoiceId);
            Assert.Equal("image/png", report.AnhChungTuContentType);
            Assert.Equal(png.Length, report.AnhChungTuSoByte);
            paymentId = report.Id;
        }

        using var owner = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(owner, "owner");
        var ownerPage = await owner.GetStringAsync($"/HoaDonDichVu/Details/{invoiceId}");
        Assert.Contains("Mở ảnh lớn", ownerPage);
        var ownerLinkResponse = await owner.GetAsync($"/HoaDonDichVu/PaymentProofLink?paymentId={paymentId}");
        Assert.Equal(HttpStatusCode.OK, ownerLinkResponse.StatusCode);
        var ownerLinkJson = await ownerLinkResponse.Content.ReadAsStringAsync();
        var ownerProofUrl = System.Text.Json.JsonDocument.Parse(ownerLinkJson).RootElement.GetProperty("url").GetString()!;
        var proof = await owner.GetAsync(ownerProofUrl);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        Assert.Equal("image/png", proof.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await proof.Content.ReadAsByteArrayAsync());
        Assert.Contains("no-store", proof.Headers.CacheControl!.ToString());
        Assert.Contains("no-cache", proof.Headers.CacheControl!.ToString());

        using var otherOwner = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(otherOwner, "other");
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherOwner.GetAsync($"/HoaDonDichVu/PaymentProofLink?paymentId={paymentId}")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QL_PhongTro.Data.AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("UPDATE tai_khoan SET vai_tro='ADMIN' WHERE id=2");
        }
        using var admin = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(admin, "other");
        var adminPage = await admin.GetAsync($"/HoaDonDichVu/Details/{invoiceId}");
        Assert.Equal(HttpStatusCode.OK, adminPage.StatusCode);
        Assert.Contains("Mở ảnh lớn", await adminPage.Content.ReadAsStringAsync());
        var adminLinkResponse = await admin.GetAsync($"/HoaDonDichVu/PaymentProofLink?paymentId={paymentId}");
        Assert.Equal(HttpStatusCode.OK, adminLinkResponse.StatusCode);
        var adminLink = System.Text.Json.JsonDocument.Parse(await adminLinkResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("url").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(adminLink)).StatusCode);

        var deniedTenant = await tenant.GetAsync(ownerProofUrl);
        Assert.Equal(HttpStatusCode.Forbidden, deniedTenant.StatusCode);

        clock.UtcNow = clock.UtcNow.AddMinutes(16);
        Assert.Equal(HttpStatusCode.Gone, (await owner.GetAsync(ownerProofUrl)).StatusCode);
    }
}
