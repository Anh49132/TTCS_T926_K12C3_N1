using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task TenantInvoiceSummaryReflectsPaymentsDiscountsAndVietnamDueDates()
    {
        using var db = Context();
        var fixture = await BillingAsync(db);
        await LinkTenantsAsync(db, fixture.B);
        var cases = new[]
        {
            ("SAMPLE-UNPAID", 1, new DateOnly(2026, 10, 1), false, false),
            ("SAMPLE-PARTIAL", 2, new DateOnly(2026, 10, 9), false, false),
            ("SAMPLE-PAID", 3, new DateOnly(2026, 10, 1), false, false),
            ("SAMPLE-OVERPAID", 4, new DateOnly(2026, 10, 1), false, false),
            ("SAMPLE-LATE-ONE", 5, new DateOnly(2026, 10, 9), false, false),
            ("SAMPLE-DUE-TODAY", 6, new DateOnly(2026, 10, 10), false, false),
            ("SAMPLE-NOT-DUE", 7, new DateOnly(2026, 10, 11), false, false),
            ("SAMPLE-DISCOUNT", 8, new DateOnly(2026, 10, 11), true, true),
            ("SAMPLE-YEAR-END", 9, new DateOnly(2026, 12, 31), false, false)
        };
        var invoiceIds = new Dictionary<string, int>();
        foreach (var (code, month, due, detailed, hasDiscount) in cases)
        {
            invoiceIds.Add(code, await AddPublishedTenantInvoiceAsync(
                db, fixture.A, code, detailed, month, 2026, due, hasDiscount));
        }

        var payments = db.ThanhToans;
        payments.AddRange(
            new() { HoaDonId = invoiceIds["SAMPLE-PARTIAL"], SoTien = 250_000, NgayThanhToan = new(2026, 10, 2), TrangThai = "DA_XAC_NHAN" },
            new() { HoaDonId = invoiceIds["SAMPLE-PARTIAL"], SoTien = 250_000, NgayThanhToan = new(2026, 10, 5), TrangThai = "DA_XAC_NHAN" },
            new() { HoaDonId = invoiceIds["SAMPLE-PARTIAL"], SoTien = 100_000, NgayThanhToan = new(2026, 10, 6), TrangThai = "CHO_XAC_NHAN" },
            new() { HoaDonId = invoiceIds["SAMPLE-PAID"], SoTien = 1_000_000, NgayThanhToan = new(2026, 10, 1), TrangThai = "DA_XAC_NHAN" },
            new() { HoaDonId = invoiceIds["SAMPLE-OVERPAID"], SoTien = 600_000, NgayThanhToan = new(2026, 10, 1), TrangThai = "DA_XAC_NHAN" },
            new() { HoaDonId = invoiceIds["SAMPLE-OVERPAID"], SoTien = 500_000, NgayThanhToan = new(2026, 10, 2), TrangThai = "DA_XAC_NHAN" });
        await db.SaveChangesAsync();

        var clock = new MockTimeProvider { UtcNow = new(2026, 10, 10, 16, 30, 0, DateTimeKind.Utc) };
        using var factory = BillingWeb(clock);
        using var tenant = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(tenant, "tenant");

        async Task<JsonElement> GetInvoice(string code)
        {
            var response = await tenant.GetAsync($"/ThongBao/ChiTietDuLieu?maHoaDon={code}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.Clone();
        }

        var unpaid = await GetInvoice("SAMPLE-UNPAID");
        Assert.Equal(1_000_000, unpaid.GetProperty("tongCong").GetInt64());
        Assert.Equal(0, unpaid.GetProperty("soDaThanhToan").GetInt64());
        Assert.Equal(1_000_000, unpaid.GetProperty("soConPhaiTra").GetInt64());
        Assert.True(unpaid.GetProperty("quaHan").GetBoolean());
        Assert.Equal(9, unpaid.GetProperty("soNgayTre").GetInt32());

        var partial = await GetInvoice("SAMPLE-PARTIAL");
        Assert.Equal(500_000, partial.GetProperty("soDaThanhToan").GetInt64());
        Assert.Equal(500_000, partial.GetProperty("soConPhaiTra").GetInt64());
        Assert.True(partial.GetProperty("quaHan").GetBoolean());
        Assert.Equal(1, partial.GetProperty("soNgayTre").GetInt32());

        var paid = await GetInvoice("SAMPLE-PAID");
        Assert.Equal(1_000_000, paid.GetProperty("soDaThanhToan").GetInt64());
        Assert.Equal(0, paid.GetProperty("soConPhaiTra").GetInt64());
        Assert.False(paid.GetProperty("quaHan").GetBoolean());
        Assert.Equal(0, paid.GetProperty("soNgayTre").GetInt32());

        var overpaid = await GetInvoice("SAMPLE-OVERPAID");
        Assert.Equal(1_100_000, overpaid.GetProperty("soDaThanhToan").GetInt64());
        Assert.Equal(0, overpaid.GetProperty("soConPhaiTra").GetInt64());
        Assert.False(overpaid.GetProperty("quaHan").GetBoolean());

        var dueToday = await GetInvoice("SAMPLE-DUE-TODAY");
        Assert.Equal("10/10/2026", dueToday.GetProperty("hanThanhToan").GetString());
        Assert.False(dueToday.GetProperty("quaHan").GetBoolean());
        Assert.False((await GetInvoice("SAMPLE-NOT-DUE")).GetProperty("quaHan").GetBoolean());

        var discount = await GetInvoice("SAMPLE-DISCOUNT");
        Assert.Equal(981_188, discount.GetProperty("tongCong").GetInt64());
        var discountLine = discount.GetProperty("chiTiet").EnumerateArray().Single(x =>
            x.GetProperty("loaiKhoan").GetString() == "GIAM_TRU");
        Assert.Equal("200.000", discountLine.GetProperty("thanhTien").GetString());
        var oldPage = await tenant.GetAsync("/ThongBao/ChiTiet?maHoaDon=SAMPLE-LATE-ONE");
        Assert.Equal(HttpStatusCode.Redirect, oldPage.StatusCode);
        Assert.StartsWith("/ThongBao/HoaDon/", oldPage.Headers.Location!.ToString());

        clock.UtcNow = new(2026, 10, 9, 17, 30, 0, DateTimeKind.Utc); // 00:30 Vietnam on the due date.
        var onDueDate = await GetInvoice("SAMPLE-DUE-TODAY");
        Assert.False(onDueDate.GetProperty("quaHan").GetBoolean());

        clock.UtcNow = new(2026, 10, 10, 23, 30, 0, DateTimeKind.Utc); // 06:30 Vietnam on the next day.
        var afterDueDate = await GetInvoice("SAMPLE-DUE-TODAY");
        Assert.True(afterDueDate.GetProperty("quaHan").GetBoolean());
        Assert.Equal(1, afterDueDate.GetProperty("soNgayTre").GetInt32());

        clock.UtcNow = new(2027, 1, 1, 18, 30, 0, DateTimeKind.Utc); // 02:30 Vietnam on January 2.
        var yearEnd = await GetInvoice("SAMPLE-YEAR-END");
        Assert.Equal(2, yearEnd.GetProperty("soNgayTre").GetInt32());
    }
}
