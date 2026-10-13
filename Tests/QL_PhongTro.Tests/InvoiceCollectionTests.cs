using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private async Task<BillingFixture> CollectionFixtureAsync(QL_PhongTro.Data.AppDbContext db)
    {
        var fixture = await BillingAsync(db);
        await LinkTenantsAsync(db, fixture.B);
        return fixture;
    }

    private static MockTimeProvider CollectionClock() => new() { UtcNow = new(2026, 10, 12, 17, 30, 0, DateTimeKind.Utc) }; // 13/10 VN
    private static GhiThuHoaDonInput Receipt(int invoiceId, decimal? amount = 400_000, DateOnly? date = null,
        string? method = "CHUYEN_KHOAN", string? key = null) => new() { HoaDonId = invoiceId, SoTien = amount,
            NgayThu = date ?? new DateOnly(2026, 10, 13), HinhThuc = method, GhiChu = "Đối chiếu riêng tư",
            KhoaChongTrung = key ?? Guid.NewGuid().ToString("N") };

    [Fact]
    public async Task CollectionPartialThenFullAndReplayKeepInvoiceImmutableAndAuditPrivate()
    {
        using var db = Context(); var fixture = await CollectionFixtureAsync(db);
        var id = await AddPublishedTenantInvoiceAsync(db, fixture.A, "COLLECTION", false);
        var invoiceBefore = JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().SingleAsync(x => x.Id == id));
        var service = new GhiThuHoaDonService(db, CollectionClock());
        var input = Receipt(id);
        var partial = await service.GhiAsync(1, input);
        Assert.Equal(400_000, partial.TongDaThu); Assert.Equal(600_000, partial.ConPhaiTra);
        Assert.Equal("THANH_TOAN_MOT_PHAN", partial.TrangThai); Assert.True(partial.ChoPhepGhiThu);
        var retry = await service.GhiAsync(1, input);
        Assert.Single(retry.LanThus);
        var full = await service.GhiAsync(1, Receipt(id, 600_000, new(2026, 10, 12), "KHAC"));
        Assert.Equal(1_000_000, full.TongDaThu); Assert.Equal(0, full.ConPhaiTra);
        Assert.Equal("DA_THANH_TOAN", full.TrangThai); Assert.False(full.ChoPhepGhiThu);
        Assert.Equal(full.TongDaThu, full.LanThus.Sum(x => x.SoTien));
        Assert.Equal(new DateOnly(2026, 10, 12), full.LanThus[0].NgayThu);
        Assert.All(full.LanThus, x => { Assert.Equal("Owner", x.NguoiXacNhan); Assert.Equal(CollectionClock().UtcNow, x.ThoiDiemGhi); });
        Assert.Equal(invoiceBefore, JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().SingleAsync(x => x.Id == id)));
        var audit = await db.NhatKyHoatDongs.AsNoTracking().Where(x => x.LoaiDoiTuong == "thanh_toan").ToListAsync();
        Assert.Equal(2, audit.Count); Assert.DoesNotContain("Đối chiếu riêng tư", JsonSerializer.Serialize(audit));
        await Assert.ThrowsAsync<GhiThuValidationException>(() => service.GhiAsync(1, Receipt(id, 1)));
    }

    [Theory]
    [InlineData("0", 0, "TIEN_MAT", "SoTien")]
    [InlineData("-1", 0, "TIEN_MAT", "SoTien")]
    [InlineData("1.5", 0, "TIEN_MAT", "SoTien")]
    [InlineData("1000001", 0, "TIEN_MAT", "SoTien")]
    [InlineData(null, 0, "TIEN_MAT", "SoTien")]
    [InlineData("1", 1, "TIEN_MAT", "NgayThu")]
    [InlineData("1", -31, "TIEN_MAT", "NgayThu")]
    [InlineData("1", 0, null, "HinhThuc")]
    [InlineData("1", 0, "INVALID", "HinhThuc")]
    public async Task CollectionRejectsInvalidFieldsWithoutRecording(string? amount, int dayOffset, string? method, string field)
    {
        using var db = Context(); var fixture = await CollectionFixtureAsync(db);
        var id = await AddPublishedTenantInvoiceAsync(db, fixture.A, "INVALID-COLLECTION", false);
        var service = new GhiThuHoaDonService(db, CollectionClock());
        var ex = await Assert.ThrowsAsync<GhiThuValidationException>(() => service.GhiAsync(1, Receipt(id,
            amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), service.HomNay.AddDays(dayOffset), method)));
        Assert.Contains(field, ex.Errors.Keys); Assert.Empty(await db.ThanhToans.ToListAsync());
        Assert.Empty(await db.LanThuHoaDons.ToListAsync());
        if (amount == "1000001") Assert.Contains("1.000.000", ex.Errors[field][0]);
    }

    [Fact]
    public async Task CollectionRequiresDateAcceptsThirtyDayBoundaryAndRejectsForeignOwner()
    {
        using var db = Context(); var fixture = await CollectionFixtureAsync(db);
        var id = await AddPublishedTenantInvoiceAsync(db, fixture.A, "BOUNDARY-COLLECTION", false);
        var service = new GhiThuHoaDonService(db, CollectionClock());
        var noDate = Receipt(id); noDate.NgayThu = null;
        Assert.Contains("NgayThu", (await Assert.ThrowsAsync<GhiThuValidationException>(() => service.GhiAsync(1, noDate))).Errors.Keys);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GhiAsync(2, Receipt(id)));
        Assert.Equal(1_000_000, (await service.GhiAsync(1, Receipt(id, 1_000_000, service.HomNay.AddDays(-30), "TIEN_MAT"))).TongDaThu);
        Assert.Empty((await db.LanThuHoaDons.Where(x => x.NguoiXacNhanId == 2).ToListAsync()));
    }

    [Theory]
    [InlineData("NHAP", "Chưa phát hành")]
    [InlineData("DA_HUY", "Đã hủy")]
    public async Task CollectionRejectsUnissuedAndCanceled(string status, string label)
    {
        using var db = Context(); var fixture = await CollectionFixtureAsync(db);
        // Build a valid draft; do not bypass published-invoice immutability triggers.
        var bill = new QL_PhongTro.Models.HoaDon { MaHoaDon = "BLOCKED-COLLECTION", HopDongId = fixture.A, Nam = 2026, Thang = 10,
            NgayChot = new(2026,10,13), TuNgay = new(2026,10,1), DenNgay = new(2026,10,31), HanThanhToan = new(2026,10,20),
            TongTien = 1_000_000, TrangThai = "NHAP", NgayLap = CollectionClock().UtcNow, NguoiLapId = 1, SoNguoiTinhPhi = 1 };
        db.HoaDons.Add(bill); await db.SaveChangesAsync();
        if (status == "DA_HUY") { bill.TrangThai = "DA_HUY"; await db.SaveChangesAsync(); }
        var ex = await Assert.ThrowsAsync<GhiThuValidationException>(() => new GhiThuHoaDonService(db, CollectionClock()).GhiAsync(1, Receipt(bill.Id)));
        Assert.Contains(label, ex.Errors[""][0]); Assert.Empty(await db.ThanhToans.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CollectionConcurrentRequestsCannotOvercollectOrDuplicate(bool sameKey)
    {
        int id;
        using (var db = Context()) { var fixture = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db, fixture.A, "RACE-COLLECTION", false); }
        var key = Guid.NewGuid().ToString("N");
        using var barrier = new Barrier(2);
        async Task<bool> Run(string requestKey) => await Task.Run(async () => {
            using var context = Context(); barrier.SignalAndWait();
            try { await new GhiThuHoaDonService(context, CollectionClock()).GhiAsync(1, Receipt(id, 700_000, key:requestKey)); return true; }
            catch (GhiThuValidationException) { return false; }
        });
        var results = await Task.WhenAll(Run(key), Run(sameKey ? key : Guid.NewGuid().ToString("N")));
        using var verify = Context();
        Assert.Equal(sameKey ? 2 : 1, results.Count(x => x));
        Assert.Single(await verify.ThanhToans.ToListAsync()); Assert.Single(await verify.LanThuHoaDons.ToListAsync());
        Assert.Equal(700_000, (await new GhiThuHoaDonService(verify, CollectionClock()).ChiTietAsync(1,id)).TongDaThu);
    }

    [Theory]
    [InlineData("audit")]
    [InlineData("metadata")]
    [InlineData("disconnect")]
    public async Task CollectionFailureRollsBackPaymentMetadataAndAudit(string fault)
    {
        int id;
        using (var db = Context()) {
            var fixture = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db, fixture.A, "ROLLBACK-COLLECTION", false);
            if (fault != "disconnect") await db.Database.ExecuteSqlRawAsync(fault == "audit" ? "CREATE TRIGGER collection_test_fault BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'forced test fault'); END;" : "CREATE TRIGGER collection_test_fault BEFORE INSERT ON lan_thu_hoa_don BEGIN SELECT RAISE(ABORT,'forced test fault'); END;");
            using var cancel = new CancellationTokenSource(); if(fault == "disconnect") cancel.Cancel();
            await Assert.ThrowsAnyAsync<Exception>(() => new GhiThuHoaDonService(db, CollectionClock()).GhiAsync(1, Receipt(id),cancel.Token));
        }
        using var verify = Context(); Assert.Empty(await verify.ThanhToans.ToListAsync()); Assert.Empty(await verify.LanThuHoaDons.ToListAsync());
        Assert.Empty(await verify.NhatKyHoatDongs.Where(x => x.LoaiDoiTuong == "thanh_toan").ToListAsync());
        Assert.Equal(1_000_000, (await new GhiThuHoaDonService(verify, CollectionClock()).ChiTietAsync(1,id)).ConPhaiTra);
    }

    [Fact]
    public async Task CollectionHttpEnforcesCsrfRolesOwnershipAndReturnsUpdatedDetails()
    {
        int id;
        using(var db = Context()) { var f = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db,f.A,"HTTP-COLLECTION",false); }
        using var factory = BillingWeb(CollectionClock());
        using var anonymous = factory.CreateClient(new(){AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.Redirect,(await anonymous.PostAsync("/HoaDonDichVu/Collect", new FormUrlEncodedContent([]))).StatusCode);
        using var owner = factory.CreateClient(new(){AllowAutoRedirect=false}); await Login(owner,"owner");
        var values = new Dictionary<string,string> { ["HoaDonId"]=id.ToString(), ["SoTien"]="1000000", ["NgayThu"]="2026-10-13", ["HinhThuc"]="CHUYEN_KHOAN", ["KhoaChongTrung"]=Guid.NewGuid().ToString("N"), ["NguoiXacNhanId"]="2" };
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync("/HoaDonDichVu/Collect",new FormUrlEncodedContent(values))).StatusCode);
        using var other = factory.CreateClient(new(){AllowAutoRedirect=false}); await Login(other,"other");
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(other,"/HoaDonDichVu/Collect",new(values))).StatusCode);
        using var tenant = factory.CreateClient(new(){AllowAutoRedirect=false}); await Login(tenant,"tenant");
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(tenant,"/HoaDonDichVu/Collect",new(values))).StatusCode);
        var html = await owner.GetStringAsync($"/HoaDonDichVu/Details/{id}"); Assert.Contains("ic-dialog",html);
        foreach (var invalid in new[] { "1.5", "1,5", "1.000", "0", "-1", "1000001" })
        {
            var rejected = await Post(owner,"/HoaDonDichVu/Collect",new(values) { ["SoTien"]=invalid });
            Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode);
        }
        var response = await Post(owner,"/HoaDonDichVu/Collect",values); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("DA_THANH_TOAN",json.RootElement.GetProperty("trangThai").GetString());
        Assert.Equal(0,json.RootElement.GetProperty("conPhaiTra").GetInt64());
        using var verify=Context(); Assert.Equal(1,(await verify.LanThuHoaDons.SingleAsync()).NguoiXacNhanId);
    }

    [Fact]
    public async Task CollectionUpgradePreservesLegacyRowsAndRerunsWithIntegrity()
    {
        using(var db=Context()) {
            var f=await CollectionFixtureAsync(db); var id=await AddPublishedTenantInvoiceAsync(db,f.A,"LEGACY-COLLECTION",false);
            db.ThanhToans.Add(new() { HoaDonId=id,SoTien=123_456,NgayThanhToan=new(2026,10,1),TrangThai="DA_XAC_NHAN" });
            db.ThanhToans.Add(new() { HoaDonId=id,SoTien=500_000,NgayThanhToan=new(2026,10,2),TrangThai="CHO_XAC_NHAN" });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE lan_thu_hoa_don; DELETE FROM app_schema_version WHERE version>=26");
        }
        QL_PhongTro.Data.DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));
        QL_PhongTro.Data.DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));
        QL_PhongTro.Data.DatabaseUpdates.Check(path);
        using var verify=Context(); Assert.Equal(2,await verify.ThanhToans.CountAsync()); Assert.Empty(await verify.LanThuHoaDons.ToListAsync());
        var summary=await new GhiThuHoaDonService(verify,CollectionClock()).ChiTietAsync(1,await verify.HoaDons.Select(x=>x.Id).SingleAsync());
        Assert.Equal(123_456,summary.TongDaThu); Assert.Equal(876_544,summary.ConPhaiTra); Assert.Single(summary.LanThus);
        Assert.Contains("dữ liệu cũ",summary.LanThus[0].NguoiXacNhan);
        using var c=Open();using var command=c.CreateCommand();command.CommandText="PRAGMA integrity_check";Assert.Equal("ok",command.ExecuteScalar());
        command.CommandText="PRAGMA foreign_key_check";using var reader=command.ExecuteReader();Assert.False(reader.Read());
    }
}
