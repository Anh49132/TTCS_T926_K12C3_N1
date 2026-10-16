using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private BaoCaoThanhToanService WorkflowReports(QL_PhongTro.Data.AppDbContext db) => new(db, new(path + ".payment-proofs"), CollectionClock());

    [Fact]
    public async Task PaymentWorkflowSubmitStoresImmutableMetadataAndOwnerNoticeWithoutReducingDebt()
    {
        int id;
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db, f.A, "WORKFLOW-SEND", false);
            await WorkflowReports(db).GuiCoGhiChuAsync(3, id, 250000, new(2026, 10, 13), "CHUYEN_KHOAN", BankFile(await BankPng()), "Private transfer memo", default);
            var payment = await db.ThanhToans.SingleAsync(); var metadata = await db.BaoCaoThanhToans.SingleAsync();
            Assert.Equal(payment.Id, metadata.ThanhToanId); Assert.Equal(3, metadata.NguoiGuiId);
            Assert.Equal(CollectionClock().UtcNow, metadata.GuiLuc); Assert.Equal("Private transfer memo", metadata.GhiChu); Assert.Equal("qr.png", metadata.TenTep);
            var notice = await db.ThongBaoThanhToans.SingleAsync(); Assert.Equal(1, notice.NguoiNhanId); Assert.Equal("BAO_MOI", notice.Loai); Assert.Null(notice.NgayDoc);
            Assert.Equal($"/HoaDonDichVu/Details/{id}#payment-report-{payment.Id}", notice.DuongDan);
            Assert.DoesNotContain("Private transfer memo", JsonSerializer.Serialize(await db.NhatKyHoatDongs.ToListAsync()));
            var summary = await new GhiThuHoaDonService(db, CollectionClock()).ChiTietAsync(1, id);
            Assert.Equal(0, summary.TongDaThu); Assert.Equal(1000000, summary.ConPhaiTra);
            var history = await PaymentWorkflow.HistoryAsync(db, id); Assert.Single(history); Assert.Equal(metadata.GuiLuc, history[0].Report!.GuiLuc);
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE bao_cao_thanh_toan SET ghi_chu='edited'"));
        }
        using var web = BillingWeb(CollectionClock());
        using var tenant = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(tenant, "tenant");
        var response = await tenant.GetAsync($"/ThongBao/HoaDon/{id}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body.Split('\n')[0]);
        Assert.Contains("data-payment-bell", body); Assert.Contains("payment-report-", body);
    }

    [Fact]
    public async Task PaymentWorkflowRejectPreservesProofNotifiesTenantAndAllowsNewReportThenConfirm()
    {
        int id, first;
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db, f.A, "WORKFLOW-REJECT", false);
            await WorkflowReports(db).GuiAsync(3, id, 250000, new(2026, 10, 13), "CHUYEN_KHOAN", BankFile(await BankPng()));
            var p = await db.ThanhToans.SingleAsync(); first = p.Id; var key = p.AnhChungTuKey;
            var owner = new GhiThuHoaDonService(db, CollectionClock());
            await Assert.ThrowsAsync<GhiThuValidationException>(() => owner.TuChoiBaoCaoAsync(1, first, " "));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owner.TuChoiBaoCaoAsync(2, first, "Other owner"));
            await owner.TuChoiBaoCaoAsync(1, first, "Chưa nhận được tiền");
            await owner.TuChoiBaoCaoAsync(1, first, "Chưa nhận được tiền");
            Assert.Equal("TU_CHOI", p.TrangThai); Assert.Equal(key, p.AnhChungTuKey); Assert.True(File.Exists(new PaymentProofImageStore(path + ".payment-proofs").FilePath(key!)));
            Assert.Equal("Chưa nhận được tiền", (await db.BaoCaoThanhToans.SingleAsync()).LyDoTuChoi);
            Assert.Equal(1, await db.ThongBaoThanhToans.CountAsync(x => x.NguoiNhanId == 3 && x.Loai == "TU_CHOI"));
            Assert.Empty(await db.LanThuHoaDons.ToListAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.XacNhanBaoCaoAsync(1, first, Guid.NewGuid().ToString("N")));
        }
        using (var db = Context())
        {
            await WorkflowReports(db).GuiAsync(3, id, 250000, new(2026, 10, 13), "TIEN_MAT");
            var second = await db.ThanhToans.SingleAsync(x => x.Id != first);
            var owner = new GhiThuHoaDonService(db, CollectionClock()); var key = Guid.NewGuid().ToString("N");
            var summary = await owner.XacNhanBaoCaoAsync(1, second.Id, key);
            Assert.Equal(250000, summary.TongDaThu); Assert.Equal(750000, summary.ConPhaiTra);
            await owner.XacNhanBaoCaoAsync(1, second.Id, key);
            Assert.Single(await db.LanThuHoaDons.ToListAsync());
            Assert.Equal(1, await db.ThongBaoThanhToans.CountAsync(x => x.NguoiNhanId == 3 && x.Loai == "DA_XAC_NHAN"));
            Assert.Equal(2, await db.BaoCaoThanhToans.CountAsync());
        }
    }

    [Fact]
    public async Task PaymentWorkflowCancelUpdatesPendingAndOwnerNoticesButDoesNotCreateReceipt()
    {
        using var db = Context(); var f = await CollectionFixtureAsync(db); var id = await AddPublishedTenantInvoiceAsync(db, f.A, "WORKFLOW-CANCEL", false);
        var service = WorkflowReports(db); await service.GuiAsync(3, id, 200000, new(2026, 10, 13), "TIEN_MAT");
        Assert.True((await service.HuyAsync(3, id)).DaHuy); Assert.False((await service.HuyAsync(3, id)).DaHuy);
        Assert.Equal("DA_HUY", (await db.ThanhToans.AsNoTracking().SingleAsync()).TrangThai);
        Assert.Equal(1, await db.ThongBaoThanhToans.CountAsync(x => x.Loai == "DA_HUY"));
        Assert.NotNull((await db.ThongBaoThanhToans.AsNoTracking().SingleAsync(x => x.Loai == "BAO_MOI")).NgayDoc);
        Assert.Empty(await db.LanThuHoaDons.ToListAsync());
        Assert.Equal(1000000, (await new GhiThuHoaDonService(db, CollectionClock()).ChiTietAsync(1, id)).ConPhaiTra);
    }

    [Fact]
    public async Task PaymentWorkflowNotificationFailureRollsBackSubmissionAndDeletesNewProof()
    {
        int id;
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db, f.A, "WORKFLOW-ROLLBACK", false);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_payment_notice BEFORE INSERT ON thong_bao_thanh_toan BEGIN SELECT RAISE(ABORT,'Synthetic notice failure'); END");
            var bytes = await BankPng();
            await Assert.ThrowsAsync<DbUpdateException>(() => WorkflowReports(db).GuiAsync(3, id, 250000, new(2026, 10, 13), "CHUYEN_KHOAN", BankFile(bytes)));
        }
        using var verify = Context();
        Assert.Empty(await verify.ThanhToans.ToListAsync()); Assert.Empty(await verify.BaoCaoThanhToans.ToListAsync()); Assert.Empty(await verify.ThongBaoThanhToans.ToListAsync());
        if (Directory.Exists(path + ".payment-proofs")) Assert.Empty(Directory.GetFiles(path + ".payment-proofs"));
    }

    [Fact]
    public async Task PaymentWorkflowBellReadIsIndependentFromPendingAndRejectRequiresOwnerWriteCsrf()
    {
        int id, payment, notice;
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db, f.A, "WORKFLOW-BELL", false);
            await WorkflowReports(db).GuiAsync(3, id, 250000, new(2026, 10, 13), "TIEN_MAT");
            payment = (await db.ThanhToans.SingleAsync()).Id; notice = (await db.ThongBaoThanhToans.SingleAsync()).Id;
        }
        using var web = BillingWeb(CollectionClock());
        using var owner = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(owner, "owner");
        using var other = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(other, "other");
        using var tenant = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(tenant, "tenant");
        var list = await owner.GetStringAsync("/api/thong-bao/danh-sach"); Assert.Contains("BAO_MOI", list); Assert.Contains("PAYMENT", list);
        Assert.DoesNotContain("WORKFLOW-BELL", await other.GetStringAsync("/api/thong-bao/danh-sach"));
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/api/thong-bao/thanh-toan/{notice}/da-doc", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(other, $"/api/thong-bao/thanh-toan/{notice}/da-doc", new())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(owner, $"/api/thong-bao/thanh-toan/{notice}/da-doc", new())).StatusCode);
        using (var count = JsonDocument.Parse(await owner.GetStringAsync("/api/thong-bao/chua-doc"))) Assert.Equal(0, count.RootElement.GetProperty("count").GetInt32());
        var management = await owner.GetStringAsync("/HoaDonDichVu/Management?trangThai=CHO_XAC_NHAN"); Assert.Contains("WORKFLOW-BELL", management);
        var fields = new Dictionary<string,string> { ["id"] = id.ToString(), ["paymentId"] = payment.ToString(), ["lyDo"] = "Chưa nhận được tiền" };
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(tenant, "/HoaDonDichVu/RejectPaymentReport", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(other, "/HoaDonDichVu/RejectPaymentReport", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/HoaDonDichVu/RejectPaymentReport", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/HoaDonDichVu/RejectPaymentReport", fields)).StatusCode);
        using var verify = Context(); Assert.Equal("TU_CHOI", (await verify.ThanhToans.SingleAsync()).TrangThai); Assert.Empty(await verify.LanThuHoaDons.ToListAsync());
        var tenantPage = WebUtility.HtmlDecode(await tenant.GetStringAsync($"/ThongBao/HoaDon/{id}")); Assert.Contains("Chưa nhận được tiền", tenantPage); Assert.Contains("name=\"soTien\"", tenantPage);
    }

    [Theory]
    [InlineData("CONFIRM")]
    [InlineData("REJECT")]
    public async Task PaymentWorkflowOutcomeNoticeFailureRollsBackFinancialStateAndReviewMetadata(string action)
    {
        int id, payment, auditCount;
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db, f.A, "WORKFLOW-OUTCOME-FAIL", false);
            await WorkflowReports(db).GuiAsync(3, id, 250000, new(2026, 10, 13), "TIEN_MAT");
            payment = (await db.ThanhToans.SingleAsync()).Id; auditCount = await db.NhatKyHoatDongs.CountAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_payment_result BEFORE INSERT ON thong_bao_thanh_toan WHEN NEW.loai<>'BAO_MOI' BEGIN SELECT RAISE(ABORT,'Synthetic result failure'); END");
            var owner = new GhiThuHoaDonService(db, CollectionClock());
            if (action == "CONFIRM") await Assert.ThrowsAsync<DbUpdateException>(() => owner.XacNhanBaoCaoAsync(1, payment, Guid.NewGuid().ToString("N")));
            else await Assert.ThrowsAsync<DbUpdateException>(() => owner.TuChoiBaoCaoAsync(1, payment, "Private rejection reason"));
        }
        using var verify = Context();
        Assert.Equal("CHO_XAC_NHAN", (await verify.ThanhToans.SingleAsync()).TrangThai);
        var metadata = await verify.BaoCaoThanhToans.SingleAsync(); Assert.Null(metadata.XuLyLuc); Assert.Null(metadata.LyDoTuChoi);
        Assert.Empty(await verify.LanThuHoaDons.ToListAsync()); Assert.Single(await verify.ThongBaoThanhToans.ToListAsync());
        Assert.Equal(auditCount, await verify.NhatKyHoatDongs.CountAsync());
    }

    [Fact]
    public async Task PaymentWorkflowCustomerOwnProofExpiresAndOwnerMustAttestBeforeHttpConfirmation()
    {
        int id, payment; var bytes = await BankPng(); var clock = CollectionClock();
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db); id = await AddPublishedTenantInvoiceAsync(db, f.A, "WORKFLOW-PROOF", false);
            await WorkflowReports(db).GuiAsync(3, id, 250000, new(2026, 10, 13), "CHUYEN_KHOAN", BankFile(bytes));
            payment = (await db.ThanhToans.SingleAsync()).Id;
        }
        using var web = BillingWeb(clock);
        using var tenant = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(tenant, "tenant");
        using var other = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(other, "tenantb");
        using var owner = web.CreateClient(new() { AllowAutoRedirect = false }); await Login(owner, "owner");
        using var anonymous = web.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/chung-tu-thanh-toan/{payment}/link")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/chung-tu-thanh-toan/{payment}/link")).StatusCode);
        using var link = JsonDocument.Parse(await tenant.GetStringAsync($"/api/chung-tu-thanh-toan/{payment}/link"));
        var url = link.RootElement.GetProperty("url").GetString()!;
        var image = await tenant.GetAsync(url); Assert.Equal(bytes, await image.Content.ReadAsByteArrayAsync()); Assert.Contains("no-store", image.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(url)).StatusCode);
        clock.UtcNow = clock.UtcNow.AddMinutes(16);
        Assert.Equal(HttpStatusCode.Gone, (await tenant.GetAsync(url)).StatusCode);
        var fields = new Dictionary<string,string> { ["id"] = id.ToString(), ["paymentId"] = payment.ToString(), ["khoaChongTrung"] = Guid.NewGuid().ToString("N") };
        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/HoaDonDichVu/ConfirmPaymentReport", fields)).StatusCode);
        using (var verify = Context()) { Assert.Equal("CHO_XAC_NHAN", (await verify.ThanhToans.SingleAsync()).TrangThai); Assert.Empty(await verify.LanThuHoaDons.ToListAsync()); }
        fields["daKiemTraTien"] = fields["dungSoTien"] = fields["dungKhach"] = "true";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/HoaDonDichVu/ConfirmPaymentReport", fields)).StatusCode);
        using (var verify = Context()) { Assert.Equal("DA_XAC_NHAN", (await verify.ThanhToans.SingleAsync()).TrangThai); Assert.Single(await verify.LanThuHoaDons.ToListAsync()); Assert.Single(await verify.ThongBaoThanhToans.Where(x => x.Loai == "DA_XAC_NHAN").ToListAsync()); }
    }

    [Fact]
    public async Task PaymentWorkflowLegacyReportDoesNotInventSubmissionDateWhenRejected()
    {
        using var db = Context(); var f = await CollectionFixtureAsync(db); var id = await AddPublishedTenantInvoiceAsync(db, f.A, "WORKFLOW-LEGACY", false);
        db.ThanhToans.Add(new() { HoaDonId = id, SoTien = 250000, NgayThanhToan = new(2026, 10, 1), HinhThucBaoCao = "TIEN_MAT", TrangThai = "CHO_XAC_NHAN" }); await db.SaveChangesAsync();
        var report = await db.ThanhToans.SingleAsync();
        await new GhiThuHoaDonService(db, CollectionClock()).TuChoiBaoCaoAsync(1, report.Id, "Cần kiểm tra lại báo cáo cũ");
        var metadata = await db.BaoCaoThanhToans.SingleAsync(); Assert.Null(metadata.GuiLuc); Assert.Null(metadata.NguoiGuiId);
        Assert.NotNull(metadata.XuLyLuc); Assert.Equal(3, (await db.ThongBaoThanhToans.SingleAsync()).NguoiNhanId);
    }
}
