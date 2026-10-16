using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task TenantInvoiceRedesignOverviewFiltersAndDirectDetailRespectOwnershipWithoutNotifications()
    {
        int partial, paid, unpaid, foreign;
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db);
            partial = await AddPublishedTenantInvoiceAsync(db, f.A, "NEW-LIST-PARTIAL", false, 1);
            paid = await AddPublishedTenantInvoiceAsync(db, f.A, "NEW-LIST-PAID", false, 2);
            unpaid = await AddPublishedTenantInvoiceAsync(db, f.A, "NEW-LIST-UNPAID", false, 3);
            foreign = await AddPublishedTenantInvoiceAsync(db, f.B, "NEW-LIST-FOREIGN", false, 1);
            db.ThanhToans.Add(new() { HoaDonId = partial, SoTien = 250000, NgayThanhToan = new(2026, 10, 1) });
            db.ThanhToans.Add(new() { HoaDonId = partial, SoTien = 100000, NgayThanhToan = new(2026, 10, 1), TrangThai = "CHO_XAC_NHAN" });
            db.ThanhToans.Add(new() { HoaDonId = paid, SoTien = 1000000, NgayThanhToan = new(2026, 10, 1) });
            db.ThongBaoHoaDons.RemoveRange(await db.ThongBaoHoaDons.ToListAsync());
            await db.SaveChangesAsync();
        }
        using var web = BillingWeb(CollectionClock());
        using var tenant = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(tenant, "tenant");
        using var all = JsonDocument.Parse(await tenant.GetStringAsync("/ThongBao/DanhSachDuLieu?pageSize=20"));
        var root = all.RootElement; var overview = root.GetProperty("tongQuan");
        Assert.Equal(3, overview.GetProperty("tongSo").GetInt32());
        Assert.Equal(2, overview.GetProperty("canThanhToan").GetInt32());
        Assert.Equal(1750000, overview.GetProperty("tongConPhaiTra").GetInt64());
        Assert.Equal(2, overview.GetProperty("quaHan").GetInt32());
        Assert.Equal(1, overview.GetProperty("daThanhToan").GetInt32());
        Assert.DoesNotContain("NEW-LIST-FOREIGN", root.ToString());
        var row = root.GetProperty("hoaDons").EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == partial);
        Assert.Equal("DEMO-A", row.GetProperty("maPhong").GetString());
        Assert.Equal(250000, row.GetProperty("soDaThanhToan").GetInt64());
        Assert.Equal(750000, row.GetProperty("soConPhaiTra").GetInt64());
        Assert.Equal(100000, row.GetProperty("soTienChoXacNhan").GetInt64());
        using var filtered = JsonDocument.Parse(await tenant.GetStringAsync("/ThongBao/DanhSachDuLieu?tinhTrang=THANH_TOAN_MOT_PHAN&baoThanhToan=CHO_XAC_NHAN&timKiem=partial"));
        Assert.Equal(partial, Assert.Single(filtered.RootElement.GetProperty("hoaDons").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(3, filtered.RootElement.GetProperty("tongQuan").GetProperty("tongSo").GetInt32());
        using var settled = JsonDocument.Parse(await tenant.GetStringAsync("/ThongBao/DanhSachDuLieu?nhom=DA_THANH_TOAN"));
        Assert.Equal(paid, Assert.Single(settled.RootElement.GetProperty("hoaDons").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await tenant.GetAsync($"/ThongBao/HoaDon/{unpaid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync($"/ThongBao/HoaDon/{foreign}")).StatusCode);
        var old = await tenant.GetAsync("/ThongBao/ChiTiet?maHoaDon=NEW-LIST-UNPAID");
        Assert.Equal(HttpStatusCode.Redirect, old.StatusCode); Assert.Equal($"/ThongBao/HoaDon/{unpaid}", old.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync("/ThongBao/ChiTiet?maHoaDon=NEW-LIST-FOREIGN")).StatusCode);
        foreach (var query in new[] { "?pageSize=7", "?baoThanhToan=INVALID", "?nhom=INVALID", "?tinhTrang=INVALID" })
            Assert.Equal(HttpStatusCode.BadRequest, (await tenant.GetAsync("/ThongBao/DanhSachDuLieu" + query)).StatusCode);
    }
}
