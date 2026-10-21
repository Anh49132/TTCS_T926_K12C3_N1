using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private static async Task SeedUtilitiesAsync(AppDbContext db, bool priced)
    {
        var electricity = new DichVu { MaDichVu = "DIEN", TenDichVu = "Điện" };
        var water = new DichVu { MaDichVu = "NUOC", TenDichVu = "Nước" };
        db.DichVuToaNhas.AddRange(
            new DichVuToaNha { ToaNhaId = 1, DichVu = electricity },
            new DichVuToaNha { ToaNhaId = 1, DichVu = water });
        db.CauHinhDichVus.AddRange(
            new CauHinhDichVu
            {
                ToaNhaId = 1, DichVu = electricity, CachTinh = CachTinhDichVu.TheoChiSo,
                DonViTinh = "kWh", DonGia = priced ? 3500 : 0, DaChotGia = priced,
                TuNgay = new DateOnly(2026, 10, 1), NguoiTaoId = 1, NgayTao = DateTime.UtcNow
            },
            new CauHinhDichVu
            {
                ToaNhaId = 1, DichVu = water, CachTinh = CachTinhDichVu.TheoChiSo,
                DonViTinh = "m³", DonGia = priced ? 15000 : 0, DaChotGia = priced,
                TuNgay = new DateOnly(2026, 10, 1), NguoiTaoId = 1, NgayTao = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UtilityConfigCanBeCreatedBeforeSuggestedCatalogInitialization()
    {
        using var db = Context();
        var clock = new MockTimeProvider { UtcNow = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc) };
        var service = new DichVuService(db, time: clock);
        var model = await service.LayCauHinhDienNuocAsync(1, 1);
        model.Dien.DonGiaChiSo = 4000;
        model.Nuoc.CachTinh = CachTinhDichVu.TheoNguoi;
        model.Nuoc.TienMotNguoi = 80000;

        await service.LuuCauHinhDienNuocAsync(1, model);

        var rows = await db.CauHinhDichVus.AsNoTracking().Include(x => x.DichVu).OrderBy(x => x.DichVu.MaDichVu).ToListAsync();
        Assert.Collection(rows,
            electricity =>
            {
                Assert.Equal("DIEN", electricity.DichVu.MaDichVu);
                Assert.Equal(new DateOnly(2026, 11, 1), electricity.TuNgay);
                Assert.Equal(4000, electricity.DonGia);
            },
            water =>
            {
                Assert.Equal("NUOC", water.DichVu.MaDichVu);
                Assert.Equal(new DateOnly(2026, 11, 1), water.TuNgay);
                Assert.Equal(80000, water.DonGia);
            });
        Assert.Equal(2, await db.DichVuToaNhas.CountAsync());
        Assert.All(await db.DichVuToaNhas.ToListAsync(), x => Assert.False(x.ApDungMacDinh));
    }

    [Fact]
    public async Task FirstUtilityPricesAreScheduledForNextPeriod()
    {
        using var db = Context();
        await SeedUtilitiesAsync(db, priced: false);
        var clock = new MockTimeProvider { UtcNow = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc) };
        var service = new DichVuService(db, time: clock);
        var model = await service.LayCauHinhDienNuocAsync(1, 1);
        model.Dien.CachTinh = CachTinhDichVu.TheoNguoi;
        model.Dien.TienMotNguoi = 120000;
        model.Nuoc.DonGiaChiSo = 16000;

        await service.LuuCauHinhDienNuocAsync(1, model);

        var rows = await db.CauHinhDichVus.AsNoTracking().Include(x => x.DichVu)
            .Where(x => x.ToaNhaId == 1 && (x.DichVu.MaDichVu == "DIEN" || x.DichVu.MaDichVu == "NUOC"))
            .OrderBy(x => x.DichVu.MaDichVu).ThenBy(x => x.TuNgay).ToListAsync();
        Assert.Equal(4, rows.Count);
        Assert.All(rows.Where(x => x.TuNgay == new DateOnly(2026, 10, 1)), x =>
        {
            Assert.False(x.DaChotGia);
            Assert.Equal(new DateOnly(2026, 10, 31), x.DenNgay);
        });
        var nextElectricity = rows.Single(x => x.DichVu.MaDichVu == "DIEN" && x.TuNgay == new DateOnly(2026, 11, 1));
        Assert.Equal(CachTinhDichVu.TheoNguoi, nextElectricity.CachTinh);
        Assert.Equal("người/tháng", nextElectricity.DonViTinh);
        Assert.Equal(120000, nextElectricity.DonGia);
        var nextWater = rows.Single(x => x.DichVu.MaDichVu == "NUOC" && x.TuNgay == new DateOnly(2026, 11, 1));
        Assert.Equal(CachTinhDichVu.TheoChiSo, nextWater.CachTinh);
        Assert.Equal("m³", nextWater.DonViTinh);
        Assert.Equal(16000, nextWater.DonGia);
        Assert.Null(await service.LayDonGiaAsync(1, 1, nextElectricity.DichVuId, new DateOnly(2026, 10, 31)));
        Assert.Equal(120000, (await service.LayDonGiaAsync(1, 1, nextElectricity.DichVuId, new DateOnly(2026, 11, 1)))!.DonGia);
    }

    [Fact]
    public async Task ChangingUtilityMethodKeepsCurrentPeriodUnchanged()
    {
        using var db = Context();
        await SeedUtilitiesAsync(db, priced: true);
        var clock = new MockTimeProvider { UtcNow = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc) };
        var service = new DichVuService(db, time: clock);
        var model = await service.LayCauHinhDienNuocAsync(1, 1);
        model.Dien.CachTinh = CachTinhDichVu.TheoNguoi;
        model.Dien.TienMotNguoi = 90000;

        await service.LuuCauHinhDienNuocAsync(1, model);

        var electricity = await db.DichVus.SingleAsync(x => x.MaDichVu == "DIEN");
        var october = await service.LayDonGiaAsync(1, 1, electricity.Id, new DateOnly(2026, 10, 31));
        var november = await service.LayDonGiaAsync(1, 1, electricity.Id, new DateOnly(2026, 11, 1));
        Assert.Equal(CachTinhDichVu.TheoChiSo, october!.CachTinh);
        Assert.Equal(3500, october.DonGia);
        Assert.Equal(CachTinhDichVu.TheoNguoi, november!.CachTinh);
        Assert.Equal(90000, november.DonGia);
    }

    [Theory]
    [InlineData(CachTinhDichVu.TheoNguoi, 4200L, 90000L)]
    public async Task ChangingUtilityMethodPreservesEntirePrivateRoomConfiguration(string method, long ownRate, long buildingRate)
    {
        using var db = Context();
        await SeedUtilitiesAsync(db, priced: true);
        var clock = new MockTimeProvider { UtcNow = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc) };
        var service = new DichVuService(db, time: clock);
        var room = await AddRoom(db, "PRIVATE-RATE");
        var catalog = await db.DichVuToaNhas.SingleAsync(x => x.DichVu.MaDichVu == "DIEN");
        var rooms = new DichVuPhongService(db, service, clock);
        await rooms.DatDichVuAsync(1, room.Id, catalog.Id, true, ownRate);
        var model = await service.LayCauHinhDienNuocAsync(1, 1);
        model.Dien.CachTinh = method;
        model.Dien.TienMotNguoi = buildingRate;
        await service.LuuCauHinhDienNuocAsync(1, model);
        var october = await rooms.LayGiaHoaDonAsync(1, room.Id, catalog.DichVuId, new(2026, 10, 31));
        var november = await rooms.LayGiaHoaDonAsync(1, room.Id, catalog.DichVuId, new(2026, 11, 1));
        Assert.Equal(CachTinhDichVu.TheoChiSo, october!.CachTinh);
        Assert.Equal(ownRate, october.DonGia);
        Assert.Equal(CachTinhDichVu.TheoChiSo, november!.CachTinh);
        Assert.Equal("kWh", november.DonViTinh);
        Assert.Equal(ownRate, november.DonGia);
        Assert.Equal(buildingRate, (await service.LayDonGiaAsync(1, 1, catalog.DichVuId, new(2026, 11, 1)))!.DonGia);
        Assert.Contains(await db.CauHinhDichVus.AsNoTracking().ToListAsync(), x =>
            x.PhongId == room.Id && x.DichVuId == catalog.DichVuId && x.TuNgay == new DateOnly(2026, 11, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task MissingOrNonPositiveSelectedUtilityPriceIsRejected(long? price)
    {
        using var db = Context();
        await SeedUtilitiesAsync(db, priced: true);
        var clock = new MockTimeProvider { UtcNow = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc) };
        var service = new DichVuService(db, time: clock);
        var model = await service.LayCauHinhDienNuocAsync(1, 1);
        model.Dien.CachTinh = CachTinhDichVu.TheoNguoi;
        model.Dien.TienMotNguoi = price;

        await Assert.ThrowsAsync<ValidationException>(() => service.LuuCauHinhDienNuocAsync(1, model));
        Assert.Equal(2, await db.CauHinhDichVus.CountAsync());
        Assert.DoesNotContain(await db.CauHinhDichVus.ToListAsync(), x => x.TuNgay == new DateOnly(2026, 11, 1));
    }
}
