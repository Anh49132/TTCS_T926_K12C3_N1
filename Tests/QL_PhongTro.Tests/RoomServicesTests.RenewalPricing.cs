using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task MonthlyRentUsesRenewedPriceOnlyForCoveredDaysAndKeepsOldInvoicesUnchanged()
    {
        using var db = Context();
        var fixture = await MonthlyFixture(db);
        var service = new HoaDonDichVuService(db, new DichVuService(db));

        var septemberStart = new DateOnly(2026, 9, 1);
        var septemberEnd = new DateOnly(2026, 9, 30);
        db.ChiSoDienNuocs.AddRange(
            new ChiSoDienNuoc { HopDongId = fixture.A, DichVuId = fixture.Dien, TuNgay = septemberStart, DenNgay = septemberEnd, ChiSoDau = 50, ChiSoCuoi = 100, NguoiNhapId = 1, NgayNhap = DateTime.UtcNow },
            new ChiSoDienNuoc { HopDongId = fixture.A, DichVuId = fixture.Nuoc, TuNgay = septemberStart, DenNgay = septemberEnd, ChiSoDau = 15, ChiSoCuoi = 20, NguoiNhapId = 1, NgayNhap = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.Equal(1, await service.PhatHanhThangAsync(1, 1, 2026, 9, fixture.A));
        var septemberId = await db.HoaDons.Where(x => x.HopDongId == fixture.A && x.Thang == 9).Select(x => x.Id).SingleAsync();
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE ky_hop_dong SET ngay_ket_thuc='2026-10-14'
            WHERE hop_dong_id={fixture.A} AND so_thu_tu=1;
            INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao)
            VALUES({fixture.A},2,'2026-10-15','2028-12-31',26,2000000,1,'2026-10-15');
            """);

        var octoberPreview = await service.XemThangAsync(1, 1, 2026, 10);
        var previewRow = Assert.Single(octoberPreview.DuKien, x => x.HoaDon.HopDongId == fixture.A);
        var previewRent = previewRow.HoaDon.ChiTiet.Where(x => x.LoaiKhoan == "TIEN_PHONG").ToList();
        Assert.Equal(new long[] { 1_000_000, 2_000_000 }, previewRent.Select(x => x.DonGia));
        Assert.Equal(new int?[] { 14, 17 }, previewRent.Select(x => x.SoNgayTinhTien));
        Assert.Equal(new int?[] { 31, 31 }, previewRent.Select(x => x.SoNgayTrongThang));
        Assert.Equal(new long[] { 451_613, 1_096_774 }, previewRent.Select(x => x.ThanhTien));
        Assert.Equal(1_548_387, previewRent.Sum(x => x.ThanhTien));

        Assert.Equal(1, await service.PhatHanhThangAsync(1, 1, 2026, 10, fixture.A));
        db.ChangeTracker.Clear();
        var octoberInvoice = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.HopDongId == fixture.A && x.Thang == 10);
        Assert.Equal(new long[] { 1_000_000, 2_000_000 },
            octoberInvoice.ChiTiet.Where(x => x.LoaiKhoan == "TIEN_PHONG").Select(x => x.DonGia));
        Assert.Equal(1_828_387, octoberInvoice.TongTien);

        var novemberStart = new DateOnly(2026, 11, 1);
        var novemberEnd = new DateOnly(2026, 11, 30);
        db.ChiSoDienNuocs.AddRange(
            new ChiSoDienNuoc { HopDongId = fixture.A, DichVuId = fixture.Dien, TuNgay = novemberStart, DenNgay = novemberEnd, ChiSoDau = 150, ChiSoCuoi = 200, NguoiNhapId = 1, NgayNhap = DateTime.UtcNow },
            new ChiSoDienNuoc { HopDongId = fixture.A, DichVuId = fixture.Nuoc, TuNgay = novemberStart, DenNgay = novemberEnd, ChiSoDau = 25, ChiSoCuoi = 30, NguoiNhapId = 1, NgayNhap = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var novemberPreview = await service.XemThangAsync(1, 1, 2026, 11);
        var novemberRow = Assert.Single(novemberPreview.DuKien, x => x.HoaDon.HopDongId == fixture.A);
        var novemberRent = Assert.Single(novemberRow.HoaDon.ChiTiet, x => x.LoaiKhoan == "TIEN_PHONG");
        Assert.Equal(2_000_000, novemberRent.DonGia);
        Assert.Equal(2_000_000, novemberRent.ThanhTien);
        Assert.Equal(1, await service.PhatHanhThangAsync(1, 1, 2026, 11, fixture.A));

        db.ChangeTracker.Clear();
        var septemberInvoice = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == septemberId);
        var septemberRent = Assert.Single(septemberInvoice.ChiTiet, x => x.LoaiKhoan == "TIEN_PHONG");
        Assert.Equal(1_000_000, septemberRent.DonGia);
        Assert.Equal(1_000_000, septemberRent.ThanhTien);
        var novemberInvoice = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.HopDongId == fixture.A && x.Thang == 11);
        Assert.Equal(2_000_000, Assert.Single(novemberInvoice.ChiTiet, x => x.LoaiKhoan == "TIEN_PHONG").DonGia);
    }
}
