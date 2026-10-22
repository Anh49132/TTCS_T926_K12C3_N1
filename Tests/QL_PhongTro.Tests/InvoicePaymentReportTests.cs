using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.5)]
    public async Task TenantPaymentReportRejectsZeroAndNegativeAmounts(decimal amount)
    {
        using var db = Context();
        var fixture = await BillingAsync(db);
        await LinkTenantsAsync(db, fixture.B);
        var invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "PAYMENT-REPORT-INVALID", false);

        var exception = await Assert.ThrowsAsync<GhiThuValidationException>(() =>
            new BaoCaoThanhToanService(db).GuiAsync(3, invoiceId, amount, new(2026, 10, 13), "TIEN_MAT"));

        Assert.Contains("SoTien", exception.Errors.Keys);
        Assert.Empty(await db.ThanhToans.ToListAsync());
    }

    [Fact]
    public async Task TenantPaymentReportRejectsAmountAboveRemaining()
    {
        using var db = Context();
        var fixture = await BillingAsync(db);
        await LinkTenantsAsync(db, fixture.B);
        var invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "PAYMENT-REPORT-OVER", false);
        var total = await db.HoaDons.Where(x => x.Id == invoiceId).Select(x => x.TongTien).SingleAsync();

        var exception = await Assert.ThrowsAsync<GhiThuValidationException>(() =>
            new BaoCaoThanhToanService(db).GuiAsync(3, invoiceId, total + 1, new(2026, 10, 13), "TIEN_MAT"));

        Assert.Contains("SoTien", exception.Errors.Keys);
        Assert.Contains(total.ToString("N0"), exception.Errors["SoTien"][0]);
        Assert.Empty(await db.ThanhToans.ToListAsync());
    }

    [Fact]
    public async Task BankTransferRequiresProofButCashPaymentDoesNot()
    {
        using var db = Context();
        var fixture = await BillingAsync(db);
        await LinkTenantsAsync(db, fixture.B);
        var invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "PAYMENT-REPORT-PROOF-REQUIRED", false);
        var reports = new BaoCaoThanhToanService(db);

        var missingProof = await Assert.ThrowsAsync<GhiThuValidationException>(() =>
            reports.GuiAsync(3, invoiceId, 250_000, new(2026, 10, 12), "CHUYEN_KHOAN"));
        Assert.Contains("AnhChungTu", missingProof.Errors.Keys);
        Assert.Empty(await db.ThanhToans.ToListAsync());

        await reports.GuiAsync(3, invoiceId, 250_000, new(2026, 10, 12), "TIEN_MAT");
        var cashReport = await db.ThanhToans.SingleAsync(x => x.HoaDonId == invoiceId);
        Assert.Null(cashReport.AnhChungTuKey);
    }

    [Fact]
    public async Task TenantCanSubmitValidPaymentReportAndCannotEditWhilePending()
    {
        int invoiceId;
        using (var db = Context())
        {
            var fixture = await BillingAsync(db);
            await LinkTenantsAsync(db, fixture.B);
            invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "PAYMENT-REPORT-LOCK", false);
            await new BaoCaoThanhToanService(db).GuiAsync(3, invoiceId, 250_000, new(2026, 10, 12), "TIEN_MAT");

            var report = await db.ThanhToans.SingleAsync(x => x.HoaDonId == invoiceId);
            Assert.Equal(250_000, report.SoTien);
            Assert.Equal(new DateOnly(2026, 10, 12), report.NgayThanhToan);
            Assert.Equal("TIEN_MAT", report.HinhThucBaoCao);
            Assert.Equal("CHO_XAC_NHAN", report.TrangThai);
            var repeat = await Assert.ThrowsAsync<GhiThuValidationException>(() =>
                new BaoCaoThanhToanService(db).GuiAsync(3, invoiceId, 1, new(2026, 10, 13), "TIEN_MAT"));
            Assert.Contains("đang chờ xác nhận", repeat.Errors[""][0]);
        }

        using var factory = BillingWeb(new MockTimeProvider { UtcNow = new(2026, 10, 12, 17, 30, 0, DateTimeKind.Utc) });
        using var tenant = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(tenant, "tenant");
        var response = await tenant.GetAsync($"/ThongBao/HoaDon/{invoiceId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Chờ xác nhận", html);
        Assert.Contains("Báo cáo đã gửi không thể chỉnh sửa", html);
        Assert.DoesNotContain("name=\"soTien\"", html);
        Assert.DoesNotContain("Gửi báo cáo thanh toán", html);
    }

    [Fact]
    public async Task TenantCanCancelPendingReportAndCanSubmitAgain()
    {
        int invoiceId;
        using (var db = Context())
        {
            var fixture = await BillingAsync(db);
            await LinkTenantsAsync(db, fixture.B);
            invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "PAYMENT-REPORT-CANCEL", false);
            await new BaoCaoThanhToanService(db).GuiAsync(3, invoiceId, 250_000, new(2026, 10, 12), "TIEN_MAT");
        }

        using var factory = BillingWeb(new MockTimeProvider { UtcNow = new(2026, 10, 12, 17, 30, 0, DateTimeKind.Utc) });
        using var tenant = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(tenant, "tenant");
        var pendingPage = WebUtility.HtmlDecode(await tenant.GetStringAsync($"/ThongBao/HoaDon/{invoiceId}"));
        Assert.Contains("Chờ xác nhận", pendingPage);
        Assert.Contains("Hủy báo thanh toán", pendingPage);
        Assert.DoesNotContain("name=\"soTien\"", pendingPage);

        var cancelled = await Post(tenant, "/ThongBao/HuyBaoCaoThanhToan",
            new() { ["hoaDonId"] = invoiceId.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        Assert.Equal($"/ThongBao/HoaDon/{invoiceId}", cancelled.Headers.Location!.ToString());
        var returnedPage = WebUtility.HtmlDecode(await tenant.GetStringAsync(cancelled.Headers.Location));
        Assert.Contains("Đã hủy báo cáo thanh toán", returnedPage);

        using (var verify = Context())
        {
            var report = await verify.ThanhToans.SingleAsync(x => x.HoaDonId == invoiceId);
            Assert.Equal("DA_HUY", report.TrangThai);
            Assert.Equal("DA_PHAT_HANH", (await verify.HoaDons.SingleAsync(x => x.Id == invoiceId)).TrangThai);
        }
        using (var data = JsonDocument.Parse(await tenant.GetStringAsync(
                   "/ThongBao/ChiTietDuLieu?maHoaDon=PAYMENT-REPORT-CANCEL")))
        {
            Assert.False(data.RootElement.GetProperty("choXacNhan").GetBoolean());
            Assert.True(data.RootElement.GetProperty("choPhepBaoCaoThanhToan").GetBoolean());
        }
        var resubmitted = await Post(tenant, "/ThongBao/BaoCaoThanhToan", new()
        {
            ["hoaDonId"] = invoiceId.ToString(),
            ["soTien"] = "100000",
            ["ngayThanhToan"] = "2026-10-13",
            ["hinhThucThanhToan"] = "TIEN_MAT"
        });
        Assert.Equal(HttpStatusCode.Redirect, resubmitted.StatusCode);
        using (var verify = Context())
        {
            Assert.Equal(2, await verify.ThanhToans.CountAsync(x => x.HoaDonId == invoiceId));
            Assert.Equal(1, await verify.ThanhToans.CountAsync(x => x.HoaDonId == invoiceId && x.TrangThai == "DA_HUY"));
            Assert.Equal(1, await verify.ThanhToans.CountAsync(x => x.HoaDonId == invoiceId && x.TrangThai == "CHO_XAC_NHAN"));
        }
    }

    [Fact]
    public async Task TenantCannotCancelReportAfterOwnerConfirmation()
    {
        int invoiceId;
        using (var db = Context())
        {
            var fixture = await BillingAsync(db);
            await LinkTenantsAsync(db, fixture.B);
            invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "PAYMENT-REPORT-CONFIRMED", false);
            await new BaoCaoThanhToanService(db).GuiAsync(3, invoiceId, 250_000, new(2026, 10, 12), "TIEN_MAT");
            await db.ThanhToans.Where(x => x.HoaDonId == invoiceId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.TrangThai, "DA_XAC_NHAN"));
        }

        using var factory = BillingWeb(new MockTimeProvider { UtcNow = new(2026, 10, 12, 17, 30, 0, DateTimeKind.Utc) });
        using var tenant = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(tenant, "tenant");
        var confirmedPage = WebUtility.HtmlDecode(await tenant.GetStringAsync($"/ThongBao/HoaDon/{invoiceId}"));
        Assert.DoesNotContain("Hủy báo thanh toán", confirmedPage);

        var rejected = await Post(tenant, "/ThongBao/HuyBaoCaoThanhToan",
            new() { ["hoaDonId"] = invoiceId.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        var resultPage = WebUtility.HtmlDecode(await tenant.GetStringAsync(rejected.Headers.Location));
        Assert.Contains("không thể hủy", resultPage, StringComparison.OrdinalIgnoreCase);
        using var verify = Context();
        Assert.Equal("DA_XAC_NHAN", (await verify.ThanhToans.SingleAsync(x => x.HoaDonId == invoiceId)).TrangThai);
    }

    [Fact]
    public async Task TenantPaymentReportHttpRejectsInvalidAmountsAndReturnsPendingStatus()
    {
        int invoiceId;
        long total;
        using (var db = Context())
        {
            var fixture = await BillingAsync(db);
            await LinkTenantsAsync(db, fixture.B);
            invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "PAYMENT-REPORT-HTTP", false);
            total = await db.HoaDons.Where(x => x.Id == invoiceId).Select(x => x.TongTien).SingleAsync();
        }

        using var factory = BillingWeb(new MockTimeProvider { UtcNow = new(2026, 10, 12, 17, 30, 0, DateTimeKind.Utc) });
        using var tenant = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(tenant, "tenant");
        var form = new Dictionary<string, string>
        {
            ["hoaDonId"] = invoiceId.ToString(),
            ["soTien"] = "0",
            ["ngayThanhToan"] = "2026-10-13",
            ["hinhThucThanhToan"] = "TIEN_MAT"
        };

        foreach (var invalid in new[] { "0", "-1", (total + 1).ToString() })
        {
            var rejected = await Post(tenant, "/ThongBao/BaoCaoThanhToan", new(form) { ["soTien"] = invalid });
            Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        }
        using (var verify = Context())
            Assert.Empty(await verify.ThanhToans.ToListAsync());

        var accepted = await Post(tenant, "/ThongBao/BaoCaoThanhToan", new(form) { ["soTien"] = "250000", ["hinhThucThanhToan"] = "TIEN_MAT" });
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        using (var verify = Context())
        {
            var report = await verify.ThanhToans.SingleAsync(x => x.HoaDonId == invoiceId);
            Assert.Equal("CHO_XAC_NHAN", report.TrangThai);
        }

        var list = await tenant.GetStringAsync("/ThongBao/DanhSachDuLieu");
        Assert.Contains("CHO_XAC_NHAN", list);
        var detailData = await tenant.GetStringAsync("/ThongBao/ChiTietDuLieu?maHoaDon=PAYMENT-REPORT-HTTP");
        using (var data = JsonDocument.Parse(detailData))
        {
            Assert.True(data.RootElement.GetProperty("choXacNhan").GetBoolean());
            Assert.Equal(250_000, data.RootElement.GetProperty("soTienBaoCaoChoXacNhan").GetInt64());
            Assert.False(data.RootElement.GetProperty("choPhepBaoCaoThanhToan").GetBoolean());
        }
        var oldPage = await tenant.GetAsync("/ThongBao/ChiTiet?maHoaDon=PAYMENT-REPORT-HTTP");
        Assert.Equal(HttpStatusCode.Redirect, oldPage.StatusCode);
        var pageResponse = await tenant.GetAsync($"/ThongBao/HoaDon/{invoiceId}");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var page = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());
        var statusPosition = page.IndexOf("id-status", StringComparison.Ordinal);
        var statusMarkup = statusPosition >= 0 ? page.Substring(statusPosition, Math.Min(180, page.Length - statusPosition)) : page;
        Assert.Contains("Chờ xác nhận", statusMarkup);
        Assert.Contains("Báo cáo đã gửi không thể chỉnh sửa", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"soTien\"", page);
    }

    [Fact]
    public async Task Version36AddsProofMetadataWithoutChangingExistingPaymentRows()
    {
        int invoiceId;
        using (var db = Context())
        {
            var fixture = await BillingAsync(db);
            await LinkTenantsAsync(db, fixture.B);
            invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "PAYMENT-REPORT-MIGRATION", false);
            db.ThanhToans.Add(new()
            {
                HoaDonId = invoiceId,
                SoTien = 123_456,
                NgayThanhToan = new(2026, 10, 1),
                TrangThai = "DA_XAC_NHAN"
            });
            await db.SaveChangesAsync();
        }

        using (var connection = Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                DROP INDEX ux_thanh_toan_anh_chung_tu_key;
                ALTER TABLE thanh_toan DROP COLUMN anh_chung_tu_key;
                ALTER TABLE thanh_toan DROP COLUMN anh_chung_tu_content_type;
                ALTER TABLE thanh_toan DROP COLUMN anh_chung_tu_so_byte;
                """;
            command.ExecuteNonQuery();
            command.CommandText = "DELETE FROM app_schema_version WHERE version>=36";
            command.ExecuteNonQuery();
            command.CommandText = "SELECT MAX(version) FROM app_schema_version";
            Assert.Equal(35L, command.ExecuteScalar());
            command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('thanh_toan') WHERE name='anh_chung_tu_key'";
            Assert.Equal(0L, command.ExecuteScalar());
        }

        DatabaseUpdates.Update(path, Path.Combine(app, "Data", "permissions.seed.json"));
        DatabaseUpdates.Check(path);

        using var verify = Context();
        var payment = await verify.ThanhToans.SingleAsync(x => x.HoaDonId == invoiceId);
        Assert.Equal(123_456, payment.SoTien);
        Assert.Equal("DA_XAC_NHAN", payment.TrangThai);
        Assert.Null(payment.AnhChungTuKey);
        Assert.Null(payment.AnhChungTuContentType);
        Assert.Null(payment.AnhChungTuSoByte);
    }
}
