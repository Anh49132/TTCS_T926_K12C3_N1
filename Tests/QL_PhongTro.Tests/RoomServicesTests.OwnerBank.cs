using SkiaSharp;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private OwnerBankImageStore BankImages => new(path + ".bank-qr");
    private OwnerBankService BankService(AppDbContext db) => new(db, BankImages, CollectionClock());
    private static Task<byte[]> BankPng() => Task.FromResult(TestImages.Encode(2, 2, SKEncodedImageFormat.Png));
    private static IFormFile BankFile(byte[] bytes, string filename = "qr.png") =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "AnhQr", filename);
    private static OwnerBankInput BankInput(string? version = null, IFormFile? file = null) => new()
    { TenNganHang = "Ngân hàng kiểm thử", SoTaiKhoan = "00123456789", PhienBan = version, AnhQr = file };

    [Fact]
    public async Task OwnerBankSaveReplaceRemoveAndConcurrencyKeepOnePrivateConfiguration()
    {
        using var db = Context(); var service = BankService(db); var bytes = await BankPng();
        var created = await service.SaveAsync(1, BankInput(file: BankFile(bytes)), default);
        Assert.Equal("00123456789", created.SoTaiKhoan); Assert.Equal("image/png", created.QrContentType);
        Assert.Equal(CollectionClock().UtcNow, created.NgayTao); Assert.True(File.Exists(BankImages.FilePath(created.QrKey!)));
        var oldKey = created.QrKey!; var version = created.PhienBan;
        var replaced = await service.SaveAsync(1, BankInput(version, BankFile(bytes)), default);
        Assert.NotEqual(oldKey, replaced.QrKey); Assert.False(File.Exists(BankImages.FilePath(oldKey)));
        Assert.True(File.Exists(BankImages.FilePath(replaced.QrKey!))); Assert.Single(await db.TaiKhoanNganHangs.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(1, BankInput(version), default));
        var remove = BankInput(replaced.PhienBan); remove.XoaQr = true; oldKey = replaced.QrKey!;
        var removed = await service.SaveAsync(1, remove, default);
        Assert.Null(removed.QrKey); Assert.Null(removed.QrContentType); Assert.False(File.Exists(BankImages.FilePath(oldKey)));
    }

    [Theory]
    [InlineData("", "123", "TenNganHang")]
    [InlineData("  ", "123", "TenNganHang")]
    [InlineData("Bank", "", "SoTaiKhoan")]
    [InlineData("Bank", "  ", "SoTaiKhoan")]
    [InlineData("Bank", "123<script>", "SoTaiKhoan")]
    public async Task OwnerBankRequiredFieldsAreValidated(string bank, string number, string field)
    {
        using var db = Context(); var input = BankInput(); input.TenNganHang = bank; input.SoTaiKhoan = number;
        var error = await Assert.ThrowsAsync<OwnerBankValidationException>(() => BankService(db).SaveAsync(1, input, default));
        Assert.Contains(field, error.Errors.Keys); Assert.Empty(await db.TaiKhoanNganHangs.ToListAsync());
    }

    [Theory]
    [InlineData("file.gif", 50)]
    [InlineData("file.png", 5242881)]
    [InlineData("file.png", 30)]
    [InlineData("file.png", 0)]
    public async Task OwnerBankRejectsInvalidUploadsWithoutChangingSavedConfiguration(string filename, int length)
    {
        string version, key;
        using (var db = Context())
        {
            var saved = await BankService(db).SaveAsync(1, BankInput(file: BankFile(await BankPng())), default);
            version = saved.PhienBan; key = saved.QrKey!;
        }
        using (var db = Context())
        {
            var error = await Assert.ThrowsAsync<OwnerBankValidationException>(() => BankService(db).SaveAsync(1, BankInput(version, BankFile(new byte[length], filename)), default));
            Assert.Contains("AnhQr", error.Errors.Keys);
        }
        using var verify = Context(); var actual = await verify.TaiKhoanNganHangs.SingleAsync();
        Assert.Equal(key, actual.QrKey); Assert.Equal(version, actual.PhienBan); Assert.True(File.Exists(BankImages.FilePath(key)));
    }

    [Fact]
    public async Task OwnerBankUploadAndDatabaseFailuresPreservePreviousQr()
    {
        string version, key;
        var bytes = await BankPng();
        using (var db = Context())
        {
            var row = await BankService(db).SaveAsync(1, BankInput(file: BankFile(bytes)), default); version = row.PhienBan; key = row.QrKey!;
        }
        var blocked = Path.Combine(folder, "blocked-storage"); await File.WriteAllTextAsync(blocked, "not a directory");
        using (var db = Context())
            await Assert.ThrowsAnyAsync<IOException>(() => new OwnerBankService(db, new(blocked), CollectionClock()).SaveAsync(1, BankInput(version, BankFile(bytes)), default));
        using (var db = Context())
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER bank_test_failure BEFORE UPDATE ON tai_khoan_ngan_hang BEGIN SELECT RAISE(ABORT,'synthetic failure'); END");
            await Assert.ThrowsAsync<DbUpdateException>(() => BankService(db).SaveAsync(1, BankInput(version, BankFile(bytes)), default));
        }
        using var verify = Context(); var saved = await verify.TaiKhoanNganHangs.SingleAsync();
        Assert.Equal(version, saved.PhienBan); Assert.Equal(key, saved.QrKey);
        Assert.Single(Directory.GetFiles(path + ".bank-qr"));
    }

    [Fact]
    public async Task OwnerBankInvoiceAccessFollowsOwnerSignerAndResidentDatesWithoutChangingInvoice()
    {
        int invoiceId, contractId;
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db); contractId = f.A;
            invoiceId = await AddPublishedTenantInvoiceAsync(db, f.A, "BANK-ACCESS", false);
            await BankService(db).SaveAsync(1, BankInput(file: BankFile(await BankPng())), default);
            var second = BankInput(); second.TenNganHang = "Other bank"; second.SoTaiKhoan = "999";
            await BankService(db).SaveAsync(2, second, default);
            var before = JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().SingleAsync(x => x.Id == invoiceId));
            var row = await BankService(db).ForInvoiceAsync(3, invoiceId, default);
            Assert.Equal(1, row!.ChuNhaId); Assert.Equal("00123456789", row.SoTaiKhoan);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => BankService(db).ForInvoiceAsync(4, invoiceId, default));
            db.NguoiOGheps.Add(new() { HopDongId = contractId, KhachThueId = 2, NgayVao = new(2026, 9, 1), NgayRa = new(2026, 9, 30) });
            await db.SaveChangesAsync(); Assert.Equal(1, (await BankService(db).ForInvoiceAsync(4, invoiceId, default))!.ChuNhaId);
            await db.Database.ExecuteSqlRawAsync("UPDATE nguoi_o_ghep SET ngay_vao='2026-10-01',ngay_ra=NULL");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => BankService(db).ForInvoiceAsync(4, invoiceId, default));
            Assert.Equal(before, JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().SingleAsync(x => x.Id == invoiceId)));
            Assert.Empty(await db.ThanhToans.ToListAsync());
        }
    }

    [Fact]
    public async Task OwnerBankHttpUsesCsrfOwnIdentityPrivateQrAndDoesNotLeakAcrossInvoices()
    {
        int invoiceId;
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db); invoiceId = await AddPublishedTenantInvoiceAsync(db, f.A, "BANK-HTTP", false);
        }
        using var web = BillingWeb(CollectionClock());
        using var anonymous = web.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/ngan-hang/cua-toi")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/ngan-hang/hoa-don/{invoiceId}")).StatusCode);
        using var owner = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(owner, "owner");
        var html = await owner.GetStringAsync("/HoSo/CuaToi"); Assert.Contains("data-bank-settings", html);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/ngan-hang/cua-toi", new MultipartFormDataContent())).StatusCode);
        using var payload = new MultipartFormDataContent();
        payload.Add(new StringContent(FormValue(html, "__RequestVerificationToken")), "__RequestVerificationToken");
        payload.Add(new StringContent("Bank HTTP"), "TenNganHang"); payload.Add(new StringContent("000123"), "SoTaiKhoan");
        payload.Add(new StringContent("2"), "ChuNhaId"); // Overposting cannot select another owner.
        payload.Add(new ByteArrayContent(await BankPng()), "AnhQr", "qr.png");
        var response = await owner.PostAsync("/api/ngan-hang/cua-toi", payload); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("qrKey", json); Assert.DoesNotContain(path, json);
        using var tenant = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(tenant, "tenant");
        var read = await tenant.GetAsync($"/api/ngan-hang/hoa-don/{invoiceId}"); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Contains("000123", await read.Content.ReadAsStringAsync()); Assert.Contains("no-store", read.Headers.CacheControl!.ToString());
        var qr = await tenant.GetAsync($"/api/ngan-hang/hoa-don/{invoiceId}/qr"); Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        Assert.Equal("image/png", qr.Content.Headers.ContentType!.MediaType); Assert.Contains("no-store", qr.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync("/api/ngan-hang/cua-toi")).StatusCode);
        using var otherTenant = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(otherTenant, "tenantb");
        foreach (var suffix in new[] { "", "/qr" })
        {
            var denied = await otherTenant.GetAsync($"/api/ngan-hang/hoa-don/{invoiceId}{suffix}");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.DoesNotContain("000123", await denied.Content.ReadAsStringAsync());
        }
        using var other = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(other, "other");
        var otherConfig = await other.GetStringAsync("/api/ngan-hang/cua-toi"); Assert.Contains("\"bank\":null", otherConfig);
        using var verify = Context(); Assert.Equal(1, (await verify.TaiKhoanNganHangs.SingleAsync()).ChuNhaId);
        Assert.Empty(await verify.ThanhToans.ToListAsync());
    }

    [Fact]
    public async Task OwnerBankMissingConfigurationIsOptionalAndManagerCannotConfigure()
    {
        int invoiceId;
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db); invoiceId = await AddPublishedTenantInvoiceAsync(db, f.A, "BANK-MISSING", false);
            Assert.Null(await BankService(db).ForInvoiceAsync(3, invoiceId, default));
            await db.Database.ExecuteSqlRawAsync("UPDATE tai_khoan SET vai_tro='QUAN_LY' WHERE id=2");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => BankService(db).SaveAsync(2, BankInput(), default));
        }
        using var web = BillingWeb(CollectionClock());
        using var manager = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(manager, "other");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/ngan-hang/cua-toi")).StatusCode);
        using var tenant = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(tenant, "tenant");
        Assert.Contains("\"bank\":null", await tenant.GetStringAsync($"/api/ngan-hang/hoa-don/{invoiceId}"));
        Assert.Equal(HttpStatusCode.NotFound, (await tenant.GetAsync($"/api/ngan-hang/hoa-don/{invoiceId}/qr")).StatusCode);
        var page = await tenant.GetStringAsync($"/ThongBao/HoaDon/{invoiceId}"); Assert.Contains("data-invoice-bank", page);
        var oldPage = await tenant.GetAsync("/ThongBao/ChiTiet?maHoaDon=BANK-MISSING"); Assert.Equal(HttpStatusCode.Redirect, oldPage.StatusCode); Assert.Equal($"/ThongBao/HoaDon/{invoiceId}", oldPage.Headers.Location!.ToString());
    }

    [Fact]
    public async Task OwnerBankViewingQrAndPendingReportOnlyChangeBalanceAfterOwnerConfirmation()
    {
        int id;
        using var db = Context();
        var f = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db, f.A, "BANK-PAYMENT", false);
        await BankService(db).SaveAsync(1, BankInput(file: BankFile(await BankPng())), default);
        await BankService(db).ForInvoiceAsync(3, id, default);
        var reports = new BaoCaoThanhToanService(db, new(path + ".payment-proofs"), CollectionClock());
        await reports.GuiAsync(3, id, 250000, new(2026, 10, 13), "CHUYEN_KHOAN", BankFile(await BankPng()));
        var pending = await db.ThanhToans.SingleAsync(); Assert.Equal("CHO_XAC_NHAN", pending.TrangThai);
        var receipts = new GhiThuHoaDonService(db, CollectionClock());
        var before = await receipts.ChiTietAsync(1, id); Assert.Equal(0, before.TongDaThu); Assert.Equal(1000000, before.ConPhaiTra);
        var after = await receipts.XacNhanBaoCaoAsync(1, pending.Id, Guid.NewGuid().ToString("N"));
        Assert.Equal(250000, after.TongDaThu); Assert.Equal(750000, after.ConPhaiTra); Assert.Equal("THANH_TOAN_MOT_PHAN", after.TrangThai);
    }

    [Fact]
    public void OwnerBankUpgradeFrom37PreservesExistingRowsAndIsRepeatableWithBackup()
    {
        var copy = Path.Combine(folder, "bank-upgrade-copy.sqlite");
        using (var source = Open())
        using (var target = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={copy};Pooling=False"))
        { target.Open(); source.BackupDatabase(target); }
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={copy};Pooling=False"))
        {
            c.Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = "DROP TABLE tai_khoan_ngan_hang; DELETE FROM app_schema_version WHERE version>=38"; cmd.ExecuteNonQuery();
        }
        static string Snapshot(string file)
        {
            using var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={file};Mode=ReadOnly;Pooling=False"); c.Open();
            using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT IN ('app_schema_version','tai_khoan_ngan_hang','sqlite_sequence') ORDER BY name";
            var tables = new List<string>(); using (var r = cmd.ExecuteReader()) while (r.Read()) tables.Add(r.GetString(0));
            var snapshot = new List<string>();
            foreach (var table in tables)
            {
                cmd.CommandText = "SELECT * FROM \"" + table.Replace("\"", "\"\"") + "\" ORDER BY rowid";
                using var r = cmd.ExecuteReader();
                while (r.Read()) snapshot.Add(table + JsonSerializer.Serialize(Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? null : r.GetValue(i)).ToArray()));
            }
            return JsonSerializer.Serialize(snapshot);
        }
        var before = Snapshot(copy);
        DatabaseUpdates.Update(copy, Path.Combine(app, "Data", "permissions.seed.json"));
        DatabaseUpdates.Check(copy); Assert.Equal(before, Snapshot(copy));
        Assert.NotEmpty(Directory.GetFiles(folder, "bank-upgrade-copy.sqlite.before-update-*.bak"));
        DatabaseUpdates.Update(copy, Path.Combine(app, "Data", "permissions.seed.json")); Assert.Equal(before, Snapshot(copy));
    }
}
