using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private async Task<(int A, int B, int Dien, int Nuoc)> MonthlyFixture(AppDbContext db)
    {
        var f = await BillingAsync(db);
        // Baseline is rent + meters only; supplemental-service tests assign their own services.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dich_vu_phong WHERE dich_vu_toa_nha_id={f.Catalog}");
        var electric = await AddService(db, "Điện kỳ", 4000, type: CachTinhDichVu.TheoChiSo);
        var water = await AddService(db, "Nước kỳ", 16000, type: CachTinhDichVu.TheoChiSo);
        var dien = (await db.DichVuToaNhas.FindAsync(electric))!.DichVuId;
        var nuoc = (await db.DichVuToaNhas.FindAsync(water))!.DichVuId;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dich_vu SET ma_dich_vu='DIEN' WHERE id={dien}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dich_vu SET ma_dich_vu='NUOC' WHERE id={nuoc}");
        await db.Database.ExecuteSqlRawAsync("UPDATE cau_hinh_dich_vu SET tu_ngay='2026-01-01'; UPDATE phong_tro SET trang_thai='DANG_THUE'; UPDATE hop_dong SET ngay_chot_hang_thang=31;");
        foreach (var room in new[] { f.A, f.B })
        {
            await Rooms(db).DatDichVuAsync(1, room, electric, true);
            await Rooms(db).DatDichVuAsync(1, room, water, true);
            db.ChiSoDienNuocs.AddRange(
                new ChiSoDienNuoc { HopDongId = room, DichVuId = dien, TuNgay = new(2026,10,1), DenNgay = new(2026,10,31), ChiSoDau = 100, ChiSoCuoi = 150, NguoiNhapId = 1, NgayNhap = DateTime.UtcNow },
                new ChiSoDienNuoc { HopDongId = room, DichVuId = nuoc, TuNgay = new(2026,10,1), DenNgay = new(2026,10,31), ChiSoDau = 20, ChiSoCuoi = 25, NguoiNhapId = 1, NgayNhap = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        return (f.A, f.B, dien, nuoc);
    }

    [Fact]
    public async Task MonthlyIssuesMultipleRoomsAndFreezesReadingsAndPrices()
    {
        using var db = Context(); var f = await MonthlyFixture(db);
        var service = new HoaDonDichVuService(db, new DichVuService(db), new MockTimeProvider { UtcNow = new(2026,11,7,0,0,0,DateTimeKind.Utc) });
        Assert.Equal(2, await service.PhatHanhThangAsync(1,1,2026,10));
        var invoices = await db.HoaDons.Include(x=>x.ChiTiet).ToListAsync();
        Assert.All(invoices, x => { Assert.Equal(1280000, x.TongTien); Assert.Equal("DA_PHAT_HANH", x.TrangThai); Assert.Equal(new DateOnly(2026,11,14), x.HanThanhToan); Assert.Equal(3,x.ChiTiet.Count); });
        Assert.All(await db.ChiSoDienNuocs.ToListAsync(), x=>Assert.True(x.DaKhoa));
        var progress = Assert.Single((await new TienDoChiSoService(db).XemAsync(1, 2026, 10, default)).ToaNhas);
        Assert.True(progress.KyDaKhoa);
        using var factory = BillingWeb();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(client, "owner");
        Assert.Contains("Đã khóa", await client.GetStringAsync("/TienDoChiSo?nam=2026&thang=10"));
        Assert.Equal(0, await service.PhatHanhThangAsync(1,1,2026,10));
    }

    [Fact]
    public async Task MonthlySkipsRoomWithMissingWaterAndUsesCutoffPrice()
    {
        using var db = Context(); var f = await MonthlyFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM chi_so_dien_nuoc WHERE hop_dong_id={f.B} AND dich_vu_id={f.Nuoc}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE cau_hinh_dich_vu SET den_ngay='2026-10-30' WHERE dich_vu_id={f.Dien}");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO cau_hinh_dich_vu(toa_nha_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,dang_ap_dung,da_chot_gia,nguoi_tao_id,ngay_tao) VALUES(1,{f.Dien},'THEO_CHI_SO','kWh',5000,'2026-10-31',1,1,1,'2026-10-01')");
        Assert.Equal(1,await new HoaDonDichVuService(db,new DichVuService(db)).PhatHanhThangAsync(1,1,2026,10));
        var invoice = await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync();
        Assert.Equal(f.A,invoice.HopDongId); Assert.Equal(1330000,invoice.TongTien);
        Assert.Equal(5000,invoice.ChiTiet.Single(x=>x.DichVuId==f.Dien).DonGia);
    }

    [Fact]
    public async Task MonthlyPageRendersAndRejectsCrossOwnerAndMissingCsrf()
    {
        using var db = Context(); await MonthlyFixture(db);
        using var factory = BillingWeb();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(client,"owner");
        var response = await client.GetAsync("/HoaDonDichVu/Monthly?toaNhaId=1&nam=2026&thang=10");
        Assert.Equal(System.Net.HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("1.280.000",await response.Content.ReadAsStringAsync());
        Assert.Equal(System.Net.HttpStatusCode.Forbidden,(await client.GetAsync("/HoaDonDichVu/Monthly?toaNhaId=2&nam=2026&thang=10")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest,(await client.PostAsync("/HoaDonDichVu/IssueMonthly",new FormUrlEncodedContent(new Dictionary<string,string>{{"toaNhaId","1"},{"nam","2026"},{"thang","10"}}))).StatusCode);
        await Login(client,"tenant");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden,(await client.GetAsync("/HoaDonDichVu/Monthly?toaNhaId=1")).StatusCode);
    }

    [Fact]
    public async Task MonthlyRejectsOtherOwnerAndRollsBackOnAuditFailure()
    {
        using var db = Context(); await MonthlyFixture(db);
        var service=new HoaDonDichVuService(db,new DichVuService(db));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.PhatHanhThangAsync(2,1,2026,10));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_monthly_audit BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'synthetic failure'); END;");
        await Assert.ThrowsAnyAsync<Exception>(()=>service.PhatHanhThangAsync(1,1,2026,10));
        using var verify=Context(); Assert.False(await verify.HoaDons.AnyAsync()); Assert.False(await verify.ChiSoDienNuocs.AnyAsync(x=>x.DaKhoa));
    }

    [Theory]
    [InlineData(0,4000,0)] [InlineData(1,4000,4000)] [InlineData(50,4000,200000)]
    [InlineData(5,16000,80000)] [InlineData(0.125,4000,500)] [InlineData(0.001,500,1)]
    [InlineData(0.001,499,0)] [InlineData(10.125,16000,162000)] [InlineData(100,5000,500000)] [InlineData(2.5,3500,8750)]
    public void MonthlyMoneyRoundsEachLine(decimal quantity,long price,long expected) => Assert.Equal(expected,HoaDonDichVuService.ThanhTien(quantity,price));
}
