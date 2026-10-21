using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "s201-" + Guid.NewGuid().ToString("N"));
    private readonly string path;
    private readonly string app;
    private readonly string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) + "a!";

    public RoomServicesTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "QL_PhongTro", "Data"))) root = root.Parent;
        app = Path.Combine(root!.FullName, "QL_PhongTro");
        path = Path.Combine(folder, "test.sqlite");
        LocalDatabaseInitializer.Create(path, Path.Combine(app, "Data", "permissions.seed.json"));
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
            VALUES(1,'Owner','owner@example.test','0900000001',$hash,'CHU_NHA',1,1,'2026-01-01','2026-01-01'),
                  (2,'Other','other@example.test','0900000002',$hash,'CHU_NHA',1,1,'2026-01-01','2026-01-01'),
                  (3,'Tenant','tenant@example.test','0900000003',$hash,'KHACH_THUE',1,1,'2026-01-01','2026-01-01');
            INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(1,1,'Building A','Test'),(2,2,'Building B','Test');
            """;
        cmd.Parameters.AddWithValue("$hash", BCrypt.Net.BCrypt.HashPassword(password, 4));
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, Pooling = false }.ToString());
        c.Open();
        return c;
    }

    private AppDbContext Context(int actor = 1) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(
        new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, Pooling = false }.ToString()).Options,
        new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim(ClaimTypes.Role, "CHU_NHA")], "test"))
            }
        });

    private static DichVuPhongService Rooms(AppDbContext db) => new(db, new DichVuService(db));

    private static async Task<int> AddService(AppDbContext db, string name, long price, bool isDefault = true, string type = CachTinhDichVu.CoDinh, int building = 1)
    {
        await new DichVuService(db).ThemAsync(building, new TaoDichVuViewModel
        { ToaNhaId = building, TenDichVu = name, DonGia = price, ApDungMacDinh = isDefault, CachTinh = type, DonViTinh = "unit" });
        return await db.DichVuToaNhas.Where(x => x.ToaNhaId == building && x.DichVu.TenDichVu == name).Select(x => x.Id).SingleAsync();
    }

    private static async Task<PhongTro> AddRoom(AppDbContext db, string code)
    {
        var room = new PhongTro { ToaNhaId = 1, MaPhong = code, Tang = 1, DienTich = 20, GiaThue = 1000000, SoNguoiToiDa = 2, TrangThai = "TRONG", NgayTao = DateTime.UtcNow };
        db.PhongTros.Add(room);
        await Rooms(db).GanMacDinhChoPhongMoiAsync([room]);
        await db.SaveChangesAsync();
        return room;
    }

    [Fact]
    public async Task UnassignedServiceHasNoInvoicePriceEvenWhenBuildingHasDefaultPrice()
    {
        using var db = Context();
        var parking = await AddService(db, "Parking", 50000, false);
        var room = await AddRoom(db, "UPSTAIRS");
        var serviceId = (await db.DichVuToaNhas.FindAsync(parking))!.DichVuId;
        Assert.Null(await Rooms(db).LayGiaHoaDonAsync(1, room.Id, serviceId, DichVuService.HomNay()));
        await Rooms(db).DatDichVuAsync(1, room.Id, parking, true, 60000);
        Assert.Equal(60000, (await Rooms(db).LayGiaHoaDonAsync(1, room.Id, serviceId, DichVuService.HomNay()))!.DonGia);
    }

    [Fact]
    public async Task NewRoomsSnapshotDefaultsAndSelectionsAreIndependent()
    {
        using var db = Context();
        var internet = await AddService(db, "Internet", 100000);
        var parking = await AddService(db, "Parking", 50000, false);
        var a = await AddRoom(db, "A");
        var b = await AddRoom(db, "B");
        var rooms = Rooms(db);
        Assert.Equal(new[] { internet }, (await rooms.XemAsync(1, a.Id)).DichVus.Where(x => x.DaChon).Select(x => x.Id));
        await rooms.DatDichVuAsync(1, a.Id, parking, true);
        await rooms.DatDichVuAsync(1, a.Id, parking, true); // idempotent retry
        Assert.Equal(150000m, (await rooms.XemAsync(1, a.Id)).TongCoDinh);
        await rooms.DatDichVuAsync(1, a.Id, internet, false);
        Assert.Equal(150000m, (await rooms.XemAsync(1, a.Id)).TongCoDinh); // still billed for the current month
        Assert.NotNull((await rooms.XemAsync(1, a.Id)).DichVus.Single(x => x.Id == internet).NgungTuKy);
        Assert.Equal(100000m, (await rooms.XemAsync(1, b.Id)).TongCoDinh);
        var late = await AddService(db, "Late default", 7000);
        Assert.DoesNotContain((await rooms.XemAsync(1, b.Id)).DichVus, x => x.Id == late && x.DaChon);
        var parkingService = await db.DichVuToaNhas.FindAsync(parking);
        await rooms.DatMacDinhAsync(1, 1, parkingService!.DichVuId, true);
        Assert.DoesNotContain((await rooms.XemAsync(1, b.Id)).DichVus, x => x.Id == parking && x.DaChon);
        var c = await AddRoom(db, "C");
        Assert.Equal(157000m, (await rooms.XemAsync(1, c.Id)).TongCoDinh);
    }

    [Fact]
    public async Task TotalUsesCurrentCommonPriceAndExcludesVariableAndUnknownPrices()
    {
        using var db = Context();
        await AddService(db, "Fixed", long.MaxValue);
        await AddService(db, "Free", 0);
        await AddService(db, "Fixed 2", 10);
        await AddService(db, "Metered", 3500, type: CachTinhDichVu.TheoChiSo);
        await AddService(db, "Per person", 20000, type: CachTinhDichVu.TheoNguoi);
        var unknown = await AddService(db, "Unpriced", 60000);
        var serviceId = (await db.DichVuToaNhas.FindAsync(unknown))!.DichVuId;
        var price = await db.CauHinhDichVus.SingleAsync(x => x.DichVuId == serviceId);
        price.DaChotGia = false;
        await db.SaveChangesAsync();
        var a = await AddRoom(db, "A");
        var model = await Rooms(db).XemAsync(1, a.Id);
        Assert.Equal((decimal)long.MaxValue + 10m, model.TongCoDinh);
        Assert.Null(model.DichVus.Single(x => x.Id == unknown).Gia);
        price.DaChotGia = true;
        price.DonGia = 80000;
        await db.SaveChangesAsync();
        Assert.Equal((decimal)long.MaxValue + 80010m, (await Rooms(db).XemAsync(1, a.Id)).TongCoDinh);
    }

    [Fact]
    public async Task ForeignOwnerAndCrossBuildingSelectionAreRejectedAndAuditIsAtomic()
    {
        using var db = Context();
        var service = await AddService(db, "Fixed", 100);
        var a = await AddRoom(db, "A");
        using var otherDb = Context(2);
        var foreignService = await AddService(otherDb, "Foreign", 999, building: 2);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Rooms(db).XemAsync(2, a.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Rooms(db).DatDichVuAsync(1, a.Id, foreignService, true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Rooms(db).DatDichVuAsync(2, a.Id, service, false));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_test_audit BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => Rooms(db).DatDichVuAsync(1, a.Id, service, false));
        using var fresh = Context();
        Assert.Single(await fresh.DichVuPhongs.Where(x => x.PhongId == a.Id).ToListAsync());
        Assert.Empty(await fresh.NgungDichVuPhongs.ToListAsync());
    }

    [Fact]
    public async Task MigrationPreservesServicesAndRoomsAndRefusesOverwrite()
    {
        using (var db = Context())
        {
            await AddService(db, "Legacy", 1234);
            await AddRoom(db, "Legacy room");
        }
        using (var c = Open())
        using (var cmd = c.CreateCommand())
        {
            SchemaTestFixture.RemoveRoomExtensions(path);
            cmd.CommandText = "DROP TABLE anh_phong; DROP TABLE tin_dang; DROP TABLE ngung_dich_vu_phong; DROP TABLE dich_vu_phong; DROP TABLE dich_vu_toa_nha; DELETE FROM app_schema_version WHERE version >= 6;";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        var before = SHA256.HashData(File.ReadAllBytes(path));
        Assert.Throws<InvalidOperationException>(() => DatabaseUpdates.Check(path));
        SqliteConnection.ClearAllPools();
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
        DatabaseUpdates.Update(path, Path.Combine(app, "Data", "permissions.seed.json"));
        DatabaseUpdates.Check(path);
        using (var db = Context())
        {
            Assert.Equal(1234, (await db.CauHinhDichVus.SingleAsync()).DonGia);
            Assert.Equal("Legacy room", (await db.PhongTros.SingleAsync()).MaPhong);
            Assert.Empty(await db.DichVuPhongs.ToListAsync());
            Assert.False((await db.DichVuToaNhas.SingleAsync()).ApDungMacDinh);
        }
        SqliteConnection.ClearAllPools();
        var after = SHA256.HashData(File.ReadAllBytes(path));
        DatabaseUpdates.Update(path, Path.Combine(app, "Data", "permissions.seed.json"));
        SqliteConnection.ClearAllPools();
        Assert.Equal(after, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Throws<IOException>(() => LocalDatabaseInitializer.Create(path, "unused"));
        Assert.Equal(after, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.NotEmpty(Directory.GetFiles(folder, "*.before-update-*.bak"));
    }

    [Fact]
    public async Task SuggestedCatalogIsIdempotentAndRoomCreationRollsBackWithAuditFailure()
    {
        using var db = Context();
        var service = new DichVuService(db);
        await service.KhoiTaoMacDinhAsync(1, 1);
        await service.KhoiTaoMacDinhAsync(1, 1);
        Assert.Equal(5, await db.DichVuToaNhas.CountAsync(x => x.ApDungMacDinh));
        var room = await AddRoom(db, "A");
        Assert.Equal(5, await db.DichVuPhongs.CountAsync(x => x.PhongId == room.Id));
        Assert.Equal(0m, (await Rooms(db).XemAsync(1, room.Id)).TongCoDinh);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_test_audit BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => AddRoom(db, "Failed"));
        using var fresh = Context();
        Assert.Single(await fresh.PhongTros.ToListAsync());
        Assert.Equal(5, await fresh.DichVuPhongs.CountAsync());
    }

    [Fact]
    public async Task RemovingBuildingServiceInUseIsBlockedButRoomCanOptOut()
    {
        using var db = Context();
        var catalogId = await AddService(db, "Extra", 10);
        var room = await AddRoom(db, "A");
        var serviceId = (await db.DichVuToaNhas.FindAsync(catalogId))!.DichVuId;
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DichVuService(db).XoaAsync(1, 1, serviceId));
        await Rooms(db).DatDichVuAsync(1, room.Id, catalogId, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DichVuService(db).XoaAsync(1, 1, serviceId));
        Assert.Single(await db.NgungDichVuPhongs.ToListAsync()); // retain historical billing reference
    }

    [Fact]
    public async Task HttpSingleAndBulkRoomsReceiveDefaultsAndRenderDistinctSelections()
    {
        int standard, extra;
        using (var db = Context())
        {
            standard = await AddService(db, "Standard", 100000);
            extra = await AddService(db, "Extra", 40000, false);
        }
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(app);
            builder.UseEnvironment("Development");
            builder.UseSetting("DatabasePath", path);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(client, "owner");
        var single = await Post(client, "/PhongTro/Create", new()
        {
            ["ToaNhaId"] = "1",
            ["MaPhong"] = "A",
            ["Tang"] = "1",
            ["DienTich"] = "20",
            ["GiaThueDisplay"] = "1000000",
            ["SoNguoiToiDa"] = "2",
            ["TrangThai"] = "TRONG"
        });
        Assert.Equal(HttpStatusCode.Redirect, single.StatusCode);
        var bulk = await Post(client, "/PhongTro/CreateBulk", new()
        {
            ["ToaNhaId"] = "1",
            ["SoTang"] = "1",
            ["SoPhongMoiTang"] = "2",
            ["DienTich"] = "20",
            ["GiaThueDisplay"] = "1000000",
            ["SoNguoiToiDa"] = "2",
            ["TrangThai"] = "TRONG"
        });
        Assert.Equal(HttpStatusCode.Redirect, bulk.StatusCode);
        using var context = Context();
        var rooms = await context.PhongTros.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(3, rooms.Count);
        Assert.Equal(3, await context.DichVuPhongs.CountAsync(x => x.DichVuToaNhaId == standard));
        var a = rooms[0].Id;
        var b = rooms[1].Id;
        var standardServiceId = (await context.DichVuToaNhas.FindAsync(standard))!.DichVuId;
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/DichVu/SetDefault", new()
        { ["toaNhaId"] = "1", ["dichVuId"] = standardServiceId.ToString(), ["enabled"] = "false" })).StatusCode);
        Assert.Contains("SetDefault", await client.GetStringAsync("/DichVu?toaNhaId=1"));
        Assert.Equal(3, await context.DichVuPhongs.CountAsync(x => x.DichVuToaNhaId == standard));
        var response = await Post(client, "/DichVuPhong/Set", new() { ["phongId"] = a.ToString(), ["dichVuToaNhaId"] = extra.ToString(), ["enabled"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("data-total=\"140000\"", await client.GetStringAsync($"/DichVuPhong?phongId={a}"));
        var htmlB = await client.GetStringAsync($"/DichVuPhong?phongId={b}");
        Assert.Contains("data-total=\"100000\"", htmlB);
        Assert.DoesNotContain($"data-service-id=\"{extra}\"", htmlB);
        var editHtml = await client.GetStringAsync($"/PhongTro/Edit/{a}");
        Assert.Contains("id=\"room-services\"", editHtml);
        Assert.Contains($"form=\"room-service-price-{standard}-form\"", editHtml);
        Assert.Contains($"id=\"room-service-price-{standard}-form\"", editHtml);
        Assert.Contains("name=\"GhiChuNoiBo\"", editHtml);
        Assert.Contains("name=\"TienNghi\"", editHtml);
        var roomEdit = new Dictionary<string,string> {
            ["ToaNhaId"]="1",["MaPhong"]="A",["Tang"]="1",["DienTich"]="20",
            ["GiaThueDisplay"]="1000000",["SoNguoiToiDa"]="2",["TrangThai"]="TRONG",
            ["GhiChuNoiBo"]="Ghi chú riêng để kiểm thử",["TienNghi"]="DIEU_HOA"
        };
        Assert.Equal(HttpStatusCode.Redirect,(await Post(client,$"/PhongTro/Edit/{a}",roomEdit)).StatusCode);
        using (var fresh = Context()) {
            var saved = await fresh.PhongTros.FindAsync(a);
            Assert.Equal("Ghi chú riêng để kiểm thử",saved!.GhiChuNoiBo);
            Assert.Contains("DIEU_HOA",saved.TienNghi!);
        }
        Assert.Contains("Ghi chú riêng để kiểm thử",WebUtility.HtmlDecode(await client.GetStringAsync($"/PhongTro/Edit/{a}")));
        roomEdit["TienNghi"]="FORGED";
        Assert.Equal(HttpStatusCode.OK,(await Post(client,$"/PhongTro/Edit/{a}",roomEdit)).StatusCode);
        roomEdit["TienNghi"]="DIEU_HOA";roomEdit["GhiChuNoiBo"]=new string('x',501);
        Assert.Equal(HttpStatusCode.OK,(await Post(client,$"/PhongTro/Edit/{a}",roomEdit)).StatusCode);
        var priceResponse = await Post(client, "/DichVuPhong/SetPrice", new()
        { ["phongId"] = a.ToString(), ["dichVuToaNhaId"] = standard.ToString(), ["donGiaRieng"] = "123456", ["returnToRoom"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, priceResponse.StatusCode);
        Assert.Equal($"/PhongTro/Edit/{a}#room-services", priceResponse.Headers.Location?.ToString());
        Assert.Contains("value=\"123456\"", await client.GetStringAsync(priceResponse.Headers.Location));
        Assert.Equal(123456, (await Rooms(context).XemAsync(1, a)).DichVus.Single(x => x.Id == standard).DonGiaRieng);
        Assert.Null((await Rooms(context).XemAsync(1, b)).DichVus.Single(x => x.Id == standard).DonGiaRieng);
        var stopResponse = await Post(client, "/DichVuPhong/Set", new()
        { ["phongId"] = a.ToString(), ["dichVuToaNhaId"] = extra.ToString(), ["enabled"] = "false", ["returnToRoom"] = "true" });
        Assert.Equal($"/PhongTro/Edit/{a}#room-services", stopResponse.Headers.Location?.ToString());
        Assert.NotNull((await Rooms(context).XemAsync(1, a)).DichVus.Single(x => x.Id == extra).NgungTuKy);
        using var foreignOwner = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(foreignOwner, "other");
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(foreignOwner, "/DichVuPhong/SetPrice", new()
        { ["phongId"] = a.ToString(), ["dichVuToaNhaId"] = standard.ToString(), ["donGiaRieng"] = "999", ["returnToRoom"] = "true" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/DichVuPhong/Set", new FormUrlEncodedContent(new Dictionary<string, string> { ["phongId"] = a.ToString() }))).StatusCode);
        using var tenant = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(tenant, "tenant");
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync($"/DichVuPhong?phongId={a}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(tenant, "/DichVuPhong/Set", new() { ["phongId"] = a.ToString(), ["dichVuToaNhaId"] = extra.ToString(), ["enabled"] = "false" })).StatusCode);
    }

    private async Task Login(HttpClient client, string name)
    {
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Account/Login", new()
        { ["TaiKhoanDangNhap"] = name + "@example.test", ["MatKhau"] = password })).StatusCode);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string url, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync("/Account/Login");
        // Authenticated users are redirected from Login; use the room page's form instead.
        if (!html.Contains("__RequestVerificationToken")) html = await client.GetStringAsync("/Account/ChangePassword");
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        return await client.PostAsync(url, new FormUrlEncodedContent(values));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(folder, true);
    }
}
