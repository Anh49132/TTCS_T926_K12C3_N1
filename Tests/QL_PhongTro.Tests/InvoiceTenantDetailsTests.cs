using System.Net;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task TenantCanOpenOnlyTheirPublishedInvoiceByCodeAndSeeAllOrderedLines()
    {
        using var db = Context();
        var fixture = await BillingAsync(db);
        await LinkTenantsAsync(db, fixture.B);
        var invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "HD-DEMO-202609-0001", true);
        await AddPublishedTenantInvoiceAsync(db, fixture.B, "HD-DEMO-202609-0002", false);

        using var factory = BillingWeb(new MockTimeProvider { UtcNow = new(2026, 10, 10, 16, 30, 0, DateTimeKind.Utc) });
        using var tenant = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(tenant, "tenant");

        var page = await tenant.GetAsync("/ThongBao/ChiTiet?maHoaDon=HD-DEMO-202609-0001");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal($"/ThongBao/HoaDon/{invoiceId}", page.Headers.Location!.ToString());
        var canonical = await tenant.GetAsync(page.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, canonical.StatusCode);
        var html = await canonical.Content.ReadAsStringAsync();
        Assert.DoesNotContain("tenant-invoice-detail.js", html);
        Assert.Contains("data-invoice-bank", html);

        var response = await tenant.GetAsync("/ThongBao/ChiTietDuLieu?maHoaDon=HD-DEMO-202609-0001");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = data.RootElement;
        Assert.Equal("09/2026", $"{root.GetProperty("thang").GetInt32():00}/{root.GetProperty("nam").GetInt32()}");
        Assert.Equal("DEMO-A", root.GetProperty("maPhong").GetString());
        Assert.Equal(1_181_188, root.GetProperty("tongCong").GetInt64());
        Assert.Equal(0, root.GetProperty("soDaThanhToan").GetInt64());
        Assert.Equal(1_181_188, root.GetProperty("soConPhaiTra").GetInt64());
        Assert.Equal("07/10/2026", root.GetProperty("hanThanhToan").GetString());
        Assert.True(root.GetProperty("quaHan").GetBoolean());
        Assert.Equal(3, root.GetProperty("soNgayTre").GetInt32());
        var lines = root.GetProperty("chiTiet").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Tiền phòng", "Điện", "Nước", "Internet" },
            lines.Select(x => x.GetProperty("tenKhoan").GetString()).ToArray());

        var electricity = lines[1];
        Assert.Equal("100,125", electricity.GetProperty("chiSoDau").GetString());
        Assert.Equal("120,25", electricity.GetProperty("chiSoCuoi").GetString());
        Assert.Equal("20,125", electricity.GetProperty("soLuongTieuThu").GetString());
        Assert.Equal("3.500", electricity.GetProperty("donGia").GetString());
        Assert.Equal("70.438", electricity.GetProperty("thanhTien").GetString());
        Assert.Equal(70438m, decimal.Round(20.125m * 3500m, 0, MidpointRounding.AwayFromZero));
        var water = lines[2];
        Assert.Equal("10,1", water.GetProperty("chiSoDau").GetString());
        Assert.Equal("12,25", water.GetProperty("chiSoCuoi").GetString());
        Assert.Equal("2,15", water.GetProperty("soLuongTieuThu").GetString());
        Assert.Equal("10.750", water.GetProperty("thanhTien").GetString());
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        Assert.Equal(2.15m, decimal.Parse(water.GetProperty("chiSoCuoi").GetString()!, vi)
            - decimal.Parse(water.GetProperty("chiSoDau").GetString()!, vi));
        Assert.Equal(10750m, decimal.Round(2.15m * 5000m, 0, MidpointRounding.AwayFromZero));
        var internet = lines[3];
        Assert.Equal("1", internet.GetProperty("soLuongTieuThu").GetString());
        Assert.Equal("100.000", internet.GetProperty("donGia").GetString());
        Assert.Equal("100.000", internet.GetProperty("thanhTien").GetString());
        Assert.Equal(100000m, decimal.Round(1m * 100000m, 0, MidpointRounding.AwayFromZero));
        Assert.Null(lines[0].GetProperty("chiSoDau").GetString());
        Assert.Null(lines[0].GetProperty("chiSoCuoi").GetString());

        var notificationDetail = await tenant.GetAsync($"/ThongBao/HoaDon/{invoiceId}");
        Assert.Equal(HttpStatusCode.OK, notificationDetail.StatusCode);
        var notificationHtml = await notificationDetail.Content.ReadAsStringAsync();
        Assert.Contains("Tổng cộng", notificationHtml);
        Assert.Contains("Đã thu xác nhận", notificationHtml);
        Assert.Contains("Còn phải trả", notificationHtml);
        Assert.Contains("Hạn thanh toán", notificationHtml);
        Assert.Contains("Quá hạn 3 ngày", notificationHtml);

        var foreign = await tenant.GetAsync("/ThongBao/ChiTietDuLieu?maHoaDon=HD-DEMO-202609-0002");
        var missing = await tenant.GetAsync("/ThongBao/ChiTietDuLieu?maHoaDon=HD-DOES-NOT-EXIST");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await foreign.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
        Assert.Contains("Không tìm thấy hóa đơn", await foreign.Content.ReadAsStringAsync());

        using var owner = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(owner, "owner");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.GetAsync("/ThongBao/ChiTietDuLieu?maHoaDon=HD-DEMO-202609-0001")).StatusCode);
        Assert.Equal(invoiceId, await db.HoaDons.Where(x => x.MaHoaDon == "HD-DEMO-202609-0001")
            .Select(x => x.Id).SingleAsync());
    }

    private async Task LinkTenantsAsync(QL_PhongTro.Data.AppDbContext db, int secondContractId)
    {
        db.TaiKhoans.Add(new QL_PhongTro.Models.TaiKhoan
        {
            HoTen = "Tenant B",
            Email = "tenantb@example.test",
            SoDienThoai = "0900000004",
            MatKhau = BCrypt.Net.BCrypt.HashPassword(password, 4),
            VaiTro = "KHACH_THUE",
            DangHoatDong = true,
            EmailConfirmed = true,
            NgayTao = DateTime.UtcNow,
            NgayCapNhat = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var firstProfile = await db.KhachThues.SingleAsync(x => x.Id == 1);
        firstProfile.TaiKhoanId = 3;
        db.KhachThues.Add(new QL_PhongTro.Models.KhachThue
        {
            Id = 2,
            TaiKhoanId = 4,
            HoTen = "Tenant B",
            NgayTao = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hop_dong SET trang_thai='NHAP' WHERE id={secondContractId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hop_dong SET khach_dung_ten_id=2 WHERE id={secondContractId}");
    }

    private async Task<int> AddPublishedTenantInvoiceAsync(
        QL_PhongTro.Data.AppDbContext db,
        int contractId,
        string code,
        bool withAllLineTypes,
        int month = 9,
        int year = 2026,
        DateOnly? dueDate = null,
        bool includeDiscount = false)
    {
        var lines = new List<QL_PhongTro.Models.ChiTietHoaDon>
        {
            new()
            {
                SoThuTu = 1,
                LoaiKhoan = "TIEN_PHONG",
                TenKhoan = "Tiền phòng",
                DonViTinh = "tháng",
                SoLuong = 1,
                DonGia = 1_000_000,
                ThanhTien = 1_000_000
            }
        };
        if (withAllLineTypes)
        {
            lines.AddRange([
                new()
                {
                    SoThuTu = 2,
                    LoaiKhoan = "DICH_VU",
                    TenKhoan = "Điện",
                    CachTinhApDung = QL_PhongTro.Models.CachTinhDichVu.TheoChiSo,
                    DonViTinh = "kWh",
                    SoLuong = 20.125m,
                    DonGia = 3500,
                    ChiSoDau = 100.125m,
                    ChiSoCuoi = 120.25m,
                    ThanhTien = 70438
                },
                new()
                {
                    SoThuTu = 3,
                    LoaiKhoan = "DICH_VU",
                    TenKhoan = "Nước",
                    CachTinhApDung = QL_PhongTro.Models.CachTinhDichVu.TheoChiSo,
                    DonViTinh = "m³",
                    SoLuong = 2.15m,
                    DonGia = 5000,
                    ChiSoDau = 10.1m,
                    ChiSoCuoi = 12.25m,
                    ThanhTien = 10750
                },
                new()
                {
                    SoThuTu = 4,
                    LoaiKhoan = "DICH_VU",
                    TenKhoan = "Internet",
                    CachTinhApDung = QL_PhongTro.Models.CachTinhDichVu.CoDinh,
                    DonViTinh = "phòng",
                    SoLuong = 1,
                    DonGia = 100000,
                    ThanhTien = 100000
                }
            ]);
        }
        if (includeDiscount)
        {
            lines.Add(new()
            {
                SoThuTu = lines.Count + 1,
                LoaiKhoan = "GIAM_TRU",
                TenKhoan = "Giảm trừ minh họa",
                GhiChu = "Dữ liệu kiểm thử",
                SoLuong = 1,
                DonGia = 200_000,
                ThanhTien = 200_000
            });
        }
        var total = QL_PhongTro.Services.HoaDonDichVuService.TongCong(lines);
        var bill = new QL_PhongTro.Models.HoaDon
        {
            MaHoaDon = code,
            HopDongId = contractId,
            Nam = year,
            Thang = month,
            TuNgay = new DateOnly(year, month, 1),
            DenNgay = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            NgayChot = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            SoNguoiTinhPhi = 1,
            NgayLap = DateTime.UtcNow,
            HanThanhToan = dueDate ?? new DateOnly(year, month, DateTime.DaysInMonth(year, month)).AddDays(7),
            TongTien = total,
            TrangThai = "NHAP",
            NguoiLapId = 1,
            PhienBan = 0,
            ChiTiet = lines
        };
        db.HoaDons.Add(bill);
        await db.SaveChangesAsync();
        await new QL_PhongTro.Services.HoaDonDichVuService(db, new QL_PhongTro.Services.DichVuService(db))
            .PhatHanhNhapAsync(1, new()
            {
                Id = bill.Id,
                PhienBan = bill.PhienBan,
                NgayPhatHanh = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
                HanThanhToan = dueDate ?? new DateOnly(year, month, DateTime.DaysInMonth(year, month)).AddDays(7),
                XacNhan = true
            });
        return bill.Id;
    }

}
