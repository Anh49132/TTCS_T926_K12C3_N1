using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    // Hand-calculated: round each old/new price segment before summing.
    [Theory]
    [InlineData(2026, 10, 15, 451613, 1096774)]
    [InlineData(2026, 9, 15, 466667, 1066667)]
    [InlineData(2027, 2, 15, 500000, 1000000)]
    [InlineData(2028, 2, 15, 482759, 1034483)]
    [InlineData(2026, 10, 1, 0, 2000000)]
    [InlineData(2026, 10, 31, 967742, 64516)]
    [InlineData(2026, 10, 2, 32258, 1935484)]
    [InlineData(2027, 4, 30, 966667, 66667)]
    [InlineData(2027, 1, 16, 483871, 1032258)]
    [InlineData(2027, 2, 28, 964286, 71429)]
    public async Task RenewedRentSplitsAtInclusiveBoundaryAndRoundsEachLine(
        int year, int month, int renewalDay, long oldAmount, long newAmount)
    {
        using var db = Context();
        var fixture = await MonthlyFixture(db);
        var renewal = new DateOnly(year, month, renewalDay);
        var oldEnd = renewal.AddDays(-1).ToString("yyyy-MM-dd");
        var newStart = renewal.ToString("yyyy-MM-dd");
        var first = new DateOnly(year, month, 1).ToString("yyyy-MM-dd");
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE chi_so_dien_nuoc SET tu_ngay={first}, den_ngay={last} WHERE hop_dong_id={fixture.A};
            UPDATE ky_hop_dong SET ngay_bat_dau='2020-01-01', ngay_ket_thuc={oldEnd}, gia_thue=1000000
            WHERE hop_dong_id={fixture.A} AND so_thu_tu=1;
            INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao)
            VALUES({fixture.A},2,{newStart},'2030-12-31',60,2000000,1,'2026-01-01');
            """);
        var preview = await new HoaDonDichVuService(db, new DichVuService(db)).XemThangAsync(1, 1, year, month);
        var row = Assert.Single(preview.DuKien, x => x.HoaDon.HopDongId == fixture.A);
        var lines = row.HoaDon.ChiTiet.Where(x => x.LoaiKhoan == "TIEN_PHONG").ToArray();
        Assert.Equal(oldAmount + newAmount, lines.Sum(x => x.ThanhTien));
        Assert.Equal(renewalDay == 1 ? 1 : 2, lines.Length);
        Assert.Equal(newAmount, lines[^1].ThanhTien);
        if (renewalDay != 1) Assert.Equal(oldAmount, lines[0].ThanhTien);
        Assert.Equal(DateTime.DaysInMonth(year, month), lines.Sum(x => x.SoNgayTinhTien ?? DateTime.DaysInMonth(year, month)));
    }
}
