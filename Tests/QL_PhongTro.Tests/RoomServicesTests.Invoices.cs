using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private sealed record BillingFixture(int A, int B, int Catalog, int Service);

    private async Task<BillingFixture> BillingAsync(AppDbContext db)
    {
        var catalog = await AddService(db, "Parking", 100000);
        var a = await AddRoom(db, "DEMO-A");
        var b = await AddRoom(db, "DEMO-B");
        var service = (await db.DichVuToaNhas.FindAsync(catalog))!.DichVuId;
        // LocalDatabaseInitializer already installs the versioned contract schema.
        RentalRequestSchema.Initialize(path);
        DichVuSchemaInitializer.InitializeInvoices(path);
        await db.Database.ExecuteSqlRawAsync("UPDATE cau_hinh_dich_vu SET tu_ngay='2026-01-01'");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(1,'Synthetic tenant','2026-01-01')");
        foreach (var room in new[] { a, b })
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES({room.Id},{"HD-" + room.MaPhong},{room.Id},1,'DANG_HIEU_LUC',1,'2026-01-01')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES({room.Id},1,'2026-01-01','2028-12-31',36,1000000,1,'2026-01-01')");
        }
        return new(a.Id, b.Id, catalog, service);
    }

    private static async Task<LapHoaDonDichVuViewModel> BillInput(AppDbContext db, int room, int service, DateOnly date)
    {
        var price = await Rooms(db).LayGiaHoaDonAsync(1, room, service, date);
        return new()
        {
            ToaNhaId = 1,
            HopDongId = room,
            NgayApDung = date,
            SoNguoi = 1,
            Dong = price is null ? [] : [new DongDichVuInput { Chon = true, DichVuId = service, CauHinhId = price.CauHinhId, DonGiaDaXem = price.DonGia }]
        };
    }

    private static Task<int> Issue(AppDbContext db, LapHoaDonDichVuViewModel model) => new HoaDonDichVuService(db, new DichVuService(db)).PhatHanhAsync(1, model);

    [Fact]
    public async Task RemovalBillsWholeMonthThenExcludesLaterPeriodsWhileOldSnapshotsAndOtherRoomStayUnchanged()
    {
        using var db = Context();
        var f = await BillingAsync(db);
        var previous = await Issue(db, await BillInput(db, f.A, f.Service, new(2026, 9, 30)));
        var before = JsonSerializer.Serialize(await db.ChiTietHoaDons.AsNoTracking().Where(x => x.HoaDonId == previous).ToListAsync());
        var clock = new MockTimeProvider { UtcNow = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc) };
        var rooms = new DichVuPhongService(db, new DichVuService(db), clock);
        var stale = await BillInput(db, f.A, f.Service, new(2026, 11, 30));
        await rooms.DatDichVuAsync(1, f.A, f.Catalog, false);
        await rooms.DatDichVuAsync(1, f.A, f.Catalog, false);
        var removal = await db.NgungDichVuPhongs.SingleAsync();
        Assert.Equal(clock.UtcNow, removal.YeuCauLucUtc);
        Assert.Equal(new DateOnly(2026, 11, 1), removal.NgungTuKy);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Issue(db, stale));
        Assert.False(await db.HoaDons.AnyAsync(x => x.HopDongId == f.A && x.Thang == 11));
        // Backdated issue after the stop still charges October, independently of today's state.
        clock.UtcNow = new DateTime(2026, 12, 15, 0, 0, 0, DateTimeKind.Utc);
        foreach (var month in new[] { 10, 11, 12 })
        {
            var invoiceA = await Issue(db, await BillInput(db, f.A, f.Service, new(2026, month, 15)));
            var invoiceB = await Issue(db, await BillInput(db, f.B, f.Service, new(2026, month, 15)));
            Assert.Equal(month == 10 ? 1100000 : 1000000, (await db.HoaDons.FindAsync(invoiceA))!.TongTien);
            Assert.Equal(1100000, (await db.HoaDons.FindAsync(invoiceB))!.TongTien);
            Assert.Equal(month == 10 ? 1 : 0, await db.ChiTietHoaDons.CountAsync(x => x.HoaDonId == invoiceA && x.DichVuId == f.Service));
        }
        // Mutating current catalog and room price cannot affect old invoice snapshots.
        (await db.DichVus.FindAsync(f.Service))!.TenDichVu = "Renamed service";
        await db.SaveChangesAsync();
        await rooms.DatDonGiaRiengAsync(1, f.A, f.Catalog, 999999);
        Assert.Equal(before, JsonSerializer.Serialize(await db.ChiTietHoaDons.AsNoTracking().Where(x => x.HoaDonId == previous).ToListAsync()));
        Assert.Equal(1100000, (await db.HoaDons.FindAsync(previous))!.TongTien);
        using var factory = BillingWeb();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(client, "owner");
        var html = await client.GetStringAsync($"/HoaDonDichVu/Details/{previous}");
        Assert.Contains("Parking", html);
        Assert.Contains("100.000", html);
        Assert.DoesNotContain("Renamed service", html);
        Assert.DoesNotContain("999.999", html);
    }

    private WebApplicationFactory<Program> BillingWeb(ITimeProvider? testClock = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseContentRoot(app);
        builder.UseEnvironment("Development");
        builder.UseSetting("DatabasePath", path);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            if (testClock is not null)
            {
                services.RemoveAll<ITimeProvider>();
                services.AddSingleton(testClock);
            }
        });
    });

    [Theory]
    [InlineData("2026-10-31T16:59:59Z", "2026-11-01")]
    [InlineData("2026-10-31T17:00:00Z", "2026-12-01")]
    [InlineData("2026-12-31T16:59:59Z", "2027-01-01")]
    [InlineData("2028-02-29T12:00:00Z", "2028-03-01")]
    public void StopBoundaryUsesVietnamCalendar(string utc, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), DichVuPhongService.KyNgung(DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal)));

    [Fact]
    public async Task ReenableKeepsStoppedHistoryAndCancellingPendingRemovalDoesNotExcludeAnyMonth()
    {
        using var db = Context();
        var f = await BillingAsync(db);
        var clock = new MockTimeProvider { UtcNow = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc) };
        var rooms = new DichVuPhongService(db, new DichVuService(db), clock);
        await rooms.DatDichVuAsync(1, f.A, f.Catalog, false);
        await rooms.DatDichVuAsync(1, f.A, f.Catalog, true);
        Assert.NotNull(await rooms.LayGiaHoaDonAsync(1, f.A, f.Service, new(2026, 11, 1)));
        await rooms.DatDichVuAsync(1, f.A, f.Catalog, false);
        clock.UtcNow = new DateTime(2026, 12, 15, 0, 0, 0, DateTimeKind.Utc);
        await rooms.DatDichVuAsync(1, f.A, f.Catalog, true);
        Assert.Null(await rooms.LayGiaHoaDonAsync(1, f.A, f.Service, new(2026, 11, 1)));
        Assert.NotNull(await rooms.LayGiaHoaDonAsync(1, f.A, f.Service, new(2026, 12, 1)));
        Assert.Equal(2, await db.NgungDichVuPhongs.CountAsync());
    }

    [Fact]
    public async Task V7CopyUpgradePreservesIssuedInvoicesAndPrivatePrices()
    {
        using var db = Context();
        var f = await BillingAsync(db);
        await Rooms(db).DatDonGiaRiengAsync(1, f.A, f.Catalog, 75000);
        var issued = await Issue(db, await BillInput(db, f.A, f.Service, new(2026, 9, 1)));
        Assert.Equal(1075000, (await db.HoaDons.FindAsync(issued))!.TongTien);
        var copyPath = Path.Combine(folder, "v7-copy.sqlite");
        using (var source = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False"))
        using (var copy = new SqliteConnection($"Data Source={copyPath};Pooling=False"))
        {
            source.Open(); copy.Open(); source.BackupDatabase(copy);
            SchemaTestFixture.RemoveRoomExtensions(copyPath);
            using var command = copy.CreateCommand();
            command.CommandText = "DROP TABLE anh_phong; DROP TABLE tin_dang; DROP TABLE ngung_dich_vu_phong; DELETE FROM app_schema_version WHERE version >= 8";
            command.ExecuteNonQuery();
        }
        var before = Snapshot(copyPath);
        Assert.Throws<InvalidOperationException>(() => DatabaseUpdates.Check(copyPath));
        DatabaseUpdates.Update(copyPath, Path.Combine(app, "Data", "permissions.seed.json"));
        DatabaseUpdates.Check(copyPath);
        Assert.Equal(before, Snapshot(copyPath));
        Assert.NotEmpty(Directory.GetFiles(folder, "v7-copy.sqlite.before-update-*.bak"));
        DatabaseUpdates.Update(copyPath, Path.Combine(app, "Data", "permissions.seed.json"));
        Assert.Equal(before, Snapshot(copyPath));

        static string Snapshot(string database)
        {
            using var c = new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");
            c.Open();
            var rows = new List<object[]>();
            foreach (var table in new[] { "hoa_don", "chi_tiet_hoa_don", "dich_vu_phong", "cau_hinh_dich_vu", "nhat_ky_hoat_dong" })
            {
                using var command = c.CreateCommand();
                command.CommandText = "SELECT * FROM " + table + " ORDER BY id";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var row = new object[reader.FieldCount]; reader.GetValues(row); rows.Add(row);
                }
            }
            return JsonSerializer.Serialize(rows);
        }
    }

    [Fact]
    public async Task HttpFormFiltersByContractAndPeriodAndIssuesRentOnlyAfterStop()
    {
        using var db = Context();
        var f = await BillingAsync(db);
        using var factory = BillingWeb();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(client, "owner");
        var result = await Post(client, "/DichVuPhong/Set", new()
        { ["phongId"] = f.A.ToString(), ["dichVuToaNhaId"] = f.Catalog.ToString(), ["enabled"] = "false" });
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var stop = (await db.NgungDichVuPhongs.AsNoTracking().SingleAsync()).NgungTuKy;
        var a = await client.GetStringAsync($"/HoaDonDichVu?toaNhaId=1&hopDongId={f.A}&ngayApDung={stop:yyyy-MM-dd}");
        var b = await client.GetStringAsync($"/HoaDonDichVu?toaNhaId=1&hopDongId={f.B}&ngayApDung={stop:yyyy-MM-dd}");
        Assert.DoesNotContain("Dong[0].DichVuId", a);
        Assert.Contains("Parking", b);
        var response = await Post(client, "/HoaDonDichVu/Issue", new()
        { ["ToaNhaId"] = "1", ["HopDongId"] = f.A.ToString(), ["NgayApDung"] = stop.ToString("yyyy-MM-dd"), ["SoNguoi"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("1.000.000", await client.GetStringAsync(response.Headers.Location));
        Assert.Single(await db.ChiTietHoaDons.ToListAsync());
    }
}
