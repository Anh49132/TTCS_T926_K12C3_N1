using System.Net;
using System.Text.Json;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task TenantInvoiceListIsPrivateSortedPagedAndKeepsFiltersForDetails()
    {
        using var db = Context();
        var fixture = await BillingAsync(db);
        await LinkTenantsAsync(db, fixture.B);

        var invoices = new Dictionary<string, int>();
        for (var month = 1; month <= 12; month++)
        {
            var code = $"LIST-2025-{month:00}";
            var dueDate = month switch
            {
                1 or 2 or >= 6 => new DateOnly(2026, 10, 11),
                3 => new DateOnly(2026, 10, 1),
                _ => new DateOnly(2026, 10, 9)
            };
            invoices.Add(code, await AddPublishedTenantInvoiceAsync(
                db, fixture.A, code, false, month, 2025, dueDate));
        }
        var foreignCode = "LIST-FOREIGN";
        await AddPublishedTenantInvoiceAsync(db, fixture.B, foreignCode, false, 1, 2026, new(2026, 10, 11));
        db.ThanhToans.AddRange(
            new() { HoaDonId = invoices["LIST-2025-02"], SoTien = 250_000, NgayThanhToan = new(2026, 10, 1), TrangThai = "DA_XAC_NHAN" },
            new() { HoaDonId = invoices["LIST-2025-03"], SoTien = 1_000_000, NgayThanhToan = new(2026, 10, 1), TrangThai = "DA_XAC_NHAN" },
            new() { HoaDonId = invoices["LIST-2025-05"], SoTien = 250_000, NgayThanhToan = new(2026, 10, 1), TrangThai = "DA_XAC_NHAN" });
        await db.SaveChangesAsync();

        using var factory = BillingWeb(new MockTimeProvider { UtcNow = new(2026, 10, 10, 16, 30, 0, DateTimeKind.Utc) });
        using var tenant = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(tenant, "tenant");

        async Task<JsonElement> GetList(string query = "")
        {
            var response = await tenant.GetAsync("/ThongBao/DanhSachDuLieu" + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.Clone();
        }

        var all = await GetList();
        Assert.Equal(12, all.GetProperty("tongSoHoaDon").GetInt32());
        Assert.Equal(12, all.GetProperty("soKetQua").GetInt32());
        Assert.Equal(10, all.GetProperty("hoaDons").GetArrayLength());
        Assert.Equal("LIST-2025-12", all.GetProperty("hoaDons")[0].GetProperty("maHoaDon").GetString());
        Assert.Equal("LIST-2025-03", all.GetProperty("hoaDons")[9].GetProperty("maHoaDon").GetString());
        Assert.DoesNotContain(all.GetProperty("hoaDons").EnumerateArray(),
            x => x.GetProperty("maHoaDon").GetString() == foreignCode);

        var secondPage = await GetList("?page=2");
        Assert.Equal(2, secondPage.GetProperty("hoaDons").GetArrayLength());
        Assert.Equal("LIST-2025-02", secondPage.GetProperty("hoaDons")[0].GetProperty("maHoaDon").GetString());
        Assert.Equal("LIST-2025-01", secondPage.GetProperty("hoaDons")[1].GetProperty("maHoaDon").GetString());

        var oneMonth = await GetList("?ky=2025-04");
        Assert.Single(oneMonth.GetProperty("hoaDons").EnumerateArray());
        Assert.Equal("LIST-2025-04", oneMonth.GetProperty("hoaDons")[0].GetProperty("maHoaDon").GetString());

        var unpaid = await GetList("?trangThai=CHUA_THANH_TOAN");
        var unpaidRows = unpaid.GetProperty("hoaDons").EnumerateArray().ToArray();
        Assert.Equal(8, unpaidRows.Length);
        Assert.All(unpaidRows, x =>
        {
            Assert.Equal("CHUA_THANH_TOAN", x.GetProperty("trangThai").GetString());
            Assert.Equal("Chưa thanh toán", x.GetProperty("tenTrangThai").GetString());
        });
        var partial = await GetList("?trangThai=THANH_TOAN_MOT_PHAN");
        var partialRow = Assert.Single(partial.GetProperty("hoaDons").EnumerateArray());
        Assert.Equal("LIST-2025-02", partialRow.GetProperty("maHoaDon").GetString());
        Assert.Equal("Thanh toán một phần", partialRow.GetProperty("tenTrangThai").GetString());
        Assert.Equal(750_000, partialRow.GetProperty("soConPhaiTra").GetInt64());
        var paid = await GetList("?trangThai=DA_THANH_TOAN");
        var paidRow = Assert.Single(paid.GetProperty("hoaDons").EnumerateArray());
        Assert.Equal("LIST-2025-03", paidRow.GetProperty("maHoaDon").GetString());
        Assert.Equal("Đã thanh toán", paidRow.GetProperty("tenTrangThai").GetString());
        Assert.Equal(0, paidRow.GetProperty("soConPhaiTra").GetInt64());

        var overdue = await GetList("?trangThai=QUA_HAN");
        var overdueRows = overdue.GetProperty("hoaDons").EnumerateArray().ToArray();
        Assert.Equal(2, overdueRows.Length);
        Assert.All(overdueRows, x => Assert.Equal("QUA_HAN", x.GetProperty("trangThai").GetString()));
        var overduePartial = overdueRows.Single(x => x.GetProperty("maHoaDon").GetString() == "LIST-2025-05");
        Assert.True(overduePartial.GetProperty("quaHan").GetBoolean());
        Assert.Equal("Quá hạn", overduePartial.GetProperty("tenTrangThai").GetString());

        var combined = await GetList("?ky=2025-05&trangThai=QUA_HAN");
        var combinedRow = Assert.Single(combined.GetProperty("hoaDons").EnumerateArray());
        Assert.Equal("LIST-2025-05", combinedRow.GetProperty("maHoaDon").GetString());
        var noMatch = await GetList("?ky=2025-03&trangThai=QUA_HAN");
        Assert.Equal(12, noMatch.GetProperty("tongSoHoaDon").GetInt32());
        Assert.Equal(0, noMatch.GetProperty("soKetQua").GetInt32());
        Assert.Empty(noMatch.GetProperty("hoaDons").EnumerateArray());

        var detailData = await tenant.GetAsync("/ThongBao/ChiTietDuLieu?maHoaDon=LIST-2025-05");
        using var detail = JsonDocument.Parse(await detailData.Content.ReadAsStringAsync());
        Assert.Equal(combinedRow.GetProperty("quaHan").GetBoolean(), detail.RootElement.GetProperty("quaHan").GetBoolean());
        Assert.Equal(combinedRow.GetProperty("soNgayTre").GetInt32(), detail.RootElement.GetProperty("soNgayTre").GetInt32());

        var page = await tenant.GetAsync("/ThongBao/DanhSach?ky=2025-05&trangThai=QUA_HAN");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Đang tải danh sách hóa đơn", html);
        Assert.Contains("role=\"alert\" hidden", html);
        Assert.Contains("Không có hóa đơn nào khớp bộ lọc.", html);
        Assert.Contains("type=\"month\"", html);
        Assert.Contains("Đặt lại", html);

        var detailPage = await tenant.GetAsync(
            "/ThongBao/ChiTiet?maHoaDon=LIST-2025-05&ky=2025-05&trangThai=QUA_HAN&page=2");
        Assert.Equal(HttpStatusCode.Redirect, detailPage.StatusCode);
        Assert.StartsWith("/ThongBao/HoaDon/", detailPage.Headers.Location!.ToString());

    }

    [Fact]
    public async Task TenantInvoiceListRejectsInvalidFiltersAndDistinguishesNoInvoices()
    {
        using var db = Context();
        var fixture = await BillingAsync(db);
        await LinkTenantsAsync(db, fixture.B);

        using var factory = BillingWeb();
        using var tenant = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(tenant, "tenant");

        foreach (var (query, message) in new[]
        {
            ("?ky=2026-13", "Kỳ hóa đơn phải đúng định dạng yyyy-MM."),
            ("?ky=2026-1", "Kỳ hóa đơn phải đúng định dạng yyyy-MM."),
            ("?trangThai=KHONG_TON_TAI", "Trạng thái thanh toán không hợp lệ.")
        })
        {
            var response = await tenant.GetAsync("/ThongBao/DanhSachDuLieu" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(message, await response.Content.ReadAsStringAsync());
        }

        var data = await tenant.GetAsync("/ThongBao/DanhSachDuLieu");
        Assert.Equal(HttpStatusCode.OK, data.StatusCode);
        using var document = JsonDocument.Parse(await data.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("tongSoHoaDon").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("hoaDons").EnumerateArray());

        var page = await tenant.GetAsync("/ThongBao/DanhSach");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Bạn chưa có hóa đơn nào.", html);
        Assert.Contains("Không có hóa đơn nào khớp bộ lọc.", html);
    }
}
