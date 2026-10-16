using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class BaoHongTests
{
    private static void Managers(Fixture f) => f.Execute("""
        INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
        VALUES(5,'Manager','manager@test.local','0900000005','hash','QUAN_LY',1,1,'2026-01-01','2026-01-01'),
              (6,'Other manager','manager2@test.local','0900000006','hash','QUAN_LY',1,1,'2026-01-01','2026-01-01');
        UPDATE toa_nha SET quan_ly_id=5 WHERE id=1;
        INSERT INTO toa_nha(id,chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES(2,1,6,'Other building','Other address');
        UPDATE phong_tro SET toa_nha_id=2 WHERE id=3;
        """);
    private static QuanLyBaoHongService Management(AppDbContext db, Fixture f) => new(db, f.Time, new BaoHongService(db, f.Time));
    private static BaoHongTrangThaiInput Update(string state, int version = 0) => new() { TrangThai = state, GhiChu = "Đã kiểm tra thiết bị", PhienBan = version };

    [Fact]
    public async Task ManagerUpdatesAndRepliesAppearOnSameTenantDetailAfterContractEnds()
    {
        using var f = new Fixture(); Managers(f); int id;
        using (var db = f.Context())
        {
            var input = Input(); input.Images = [Photo()];
            id = (await new BaoHongService(db, f.Time).GuiAsync(2, input)).Id;
        }
        using (var db = f.Context())
        {
            var service = Management(db, f);
            Assert.Single(await service.DanhSachAsync(5, 1));
            var before = (await service.ChiTietAsync(5, id))!;
            Assert.Single(before.Images); Assert.True(before.ChoPhepXuLy);
            Assert.Equal(new[] { "DANG_XU_LY", "TU_CHOI" }, before.TrangThaiTiepTheo);
            await service.CapNhatAsync(5, id, Update("DANG_XU_LY"));
            f.Time.UtcNow = f.Time.UtcNow.AddMinutes(1);
            await service.PhanHoiAsync(5, id, new() { NoiDung = " Đã hẹn thợ kiểm tra. " });
            f.Time.UtcNow = f.Time.UtcNow.AddMinutes(1);
            await service.CapNhatAsync(5, id, Update("HOAN_THANH", 1));
            f.Time.UtcNow = f.Time.UtcNow.AddMinutes(1);
            await service.PhanHoiAsync(5, id, new() { NoiDung = "Thiết bị đã hoạt động bình thường." });
        }
        f.Execute("UPDATE hop_dong SET trang_thai='DA_KET_THUC' WHERE id=1; UPDATE tai_khoan SET ho_ten='Renamed manager' WHERE id=5");
        using var check = f.Context(); var tenant = new BaoHongService(check, f.Time);
        var detail = (await tenant.ChiTietAsync(2, id))!;
        Assert.Equal("HOAN_THANH", detail.BaoHong.TrangThai); Assert.Equal(2, detail.BaoHong.PhienBan);
        Assert.Equal(4, detail.TraoDois.Count); Assert.Single(detail.Images);
        Assert.All(detail.TraoDois, x => { Assert.Equal(5, x.NguoiGuiId); Assert.Equal("Manager", x.TenNguoiGui); });
        Assert.Equal("Đã hẹn thợ kiểm tra.", detail.TraoDois[1].NoiDung);
        Assert.Equal(detail.TraoDois.OrderBy(x => x.ThoiDiem).Select(x => x.Id), detail.TraoDois.Select(x => x.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tenant.ChiTietAsync(3, id));
        var final = (await Management(check, f).ChiTietAsync(5, id))!;
        Assert.Empty(final.TrangThaiTiepTheo);
        await Management(check, f).PhanHoiAsync(5, id, new() { NoiDung = "Phản hồi bổ sung sau khi trả phòng." });
        Assert.Equal(5, (await tenant.ChiTietAsync(2, id))!.TraoDois.Count);
    }

    [Theory]
    [InlineData("MOI", "HOAN_THANH")]
    [InlineData("MOI", "MOI")]
    [InlineData("MOI", "BAD")]
    [InlineData("DANG_XU_LY", "TU_CHOI")]
    [InlineData("HOAN_THANH", "DANG_XU_LY")]
    [InlineData("TU_CHOI", "MOI")]
    public async Task InvalidTransitionsDoNotWriteHistoryOrChangeState(string current, string target)
    {
        using var f = new Fixture(); Managers(f); int id;
        using (var db = f.Context()) id = (await new BaoHongService(db, f.Time).GuiAsync(2, Input())).Id;
        f.Execute($"UPDATE bao_hong SET trang_thai='{current}' WHERE id={id}");
        using var check = f.Context();
        await Assert.ThrowsAsync<BaoHongValidationException>(() => Management(check, f).CapNhatAsync(5, id, Update(target)));
        Assert.Equal(current, (await check.BaoHongs.AsNoTracking().SingleAsync()).TrangThai);
        Assert.Equal(0, f.Scalar("SELECT COUNT(*) FROM bao_hong_trao_doi"));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" \r\n\t ")]
    public async Task EmptyFeedbackAndStatusNoteAreRejected(string? text)
    {
        using var f = new Fixture(); Managers(f); using var db = f.Context();
        var id = (await new BaoHongService(db, f.Time).GuiAsync(2, Input())).Id;
        var service = Management(db, f);
        await Assert.ThrowsAsync<BaoHongValidationException>(() => service.PhanHoiAsync(5, id, new() { NoiDung = text }));
        var change = Update("DANG_XU_LY"); change.GhiChu = text;
        await Assert.ThrowsAsync<BaoHongValidationException>(() => service.CapNhatAsync(5, id, change));
        Assert.Equal(0, f.Scalar("SELECT COUNT(*) FROM bao_hong_trao_doi"));
    }

    [Fact]
    public async Task ScopeIsRecheckedAndReadOnlyOrRevokedPermissionCannotWrite()
    {
        using var f = new Fixture(); Managers(f); using var db = f.Context();
        var id = (await new BaoHongService(db, f.Time).GuiAsync(2, Input())).Id;
        var service = Management(db, f);
        Assert.Empty(await service.DanhSachAsync(6, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChiTietAsync(6, id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PhanHoiAsync(6, id, new() { NoiDung = "Wrong room" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CapNhatAsync(6, id, Update("DANG_XU_LY")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChiTietAsync(1, id));
        Assert.Null(await service.ChiTietAsync(5, 99999));
        f.Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'");
        Assert.False((await service.ChiTietAsync(5, id))!.ChoPhepXuLy);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PhanHoiAsync(5, id, new() { NoiDung = "Read only" }));
        f.Execute("UPDATE role_permission SET AccessLevel='WRITE' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'; UPDATE toa_nha SET quan_ly_id=6 WHERE id=1");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CapNhatAsync(5, id, Update("DANG_XU_LY")));
        f.Execute("UPDATE toa_nha SET quan_ly_id=5 WHERE id=1; UPDATE tai_khoan SET dang_hoat_dong=0 WHERE id=5");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChiTietAsync(5, id));
        Assert.Equal(0, f.Scalar("SELECT COUNT(*) FROM bao_hong_trao_doi"));
    }

    [Fact]
    public async Task RejectAndFinalFeedbackRetryAreIdempotentAndAuditExcludesText()
    {
        using var f = new Fixture(); Managers(f); using var db = f.Context();
        var id = (await new BaoHongService(db, f.Time).GuiAsync(2, Input())).Id;
        var service = Management(db, f); var update = Update("TU_CHOI");
        var a = await service.CapNhatAsync(5, id, update);
        Assert.Equal(a.Id, (await service.CapNhatAsync(5, id, update)).Id);
        var input = new BaoHongPhanHoiInput { NoiDung = "Nội dung riêng không đưa vào nhật ký <script>alert(1)</script>" };
        var b = await service.PhanHoiAsync(5, id, input);
        Assert.Equal(b.Id, (await service.PhanHoiAsync(5, id, input)).Id);
        input.NoiDung = "Other message";
        await Assert.ThrowsAsync<BaoHongValidationException>(() => service.PhanHoiAsync(5, id, input));
        Assert.Equal(2, f.Scalar("SELECT COUNT(*) FROM bao_hong_trao_doi"));
        using var c = new SqliteConnection($"Data Source={f.Path}"); c.Open(); using var command = c.CreateCommand();
        command.CommandText = "SELECT group_concat(du_lieu_sau) FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='bao_hong'";
        var audit = (string)command.ExecuteScalar()!;
        Assert.DoesNotContain("script", audit); Assert.DoesNotContain("NoiDung", audit); Assert.DoesNotContain("GhiChu", audit);
        command.CommandText = "UPDATE bao_hong_trao_doi SET noi_dung='tampered'";
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText = "DELETE FROM bao_hong_trao_doi";
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public async Task ConcurrentTransitionsRejectStaleVersionAndConcurrentFeedbackKeepsAllMessages()
    {
        using var f = new Fixture(); Managers(f); int id;
        using (var db = f.Context()) id = (await new BaoHongService(db, f.Time).GuiAsync(2, Input())).Id;
        var outcomes = await Task.WhenAll(new[] { "DANG_XU_LY", "TU_CHOI" }.Select(state => Task.Run(async () =>
        {
            using var db = f.Context();
            try { await Management(db, f).CapNhatAsync(5, id, Update(state)); return true; }
            catch (BaoHongConflictException) { return false; }
        })));
        Assert.Single(outcomes, x => x); Assert.Equal(1, f.Scalar("SELECT COUNT(*) FROM bao_hong_trao_doi"));
        var reply = new BaoHongPhanHoiInput { NoiDung = "Retry same feedback" };
        var retried = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        { using var db = f.Context(); return await Management(db, f).PhanHoiAsync(5, id, reply); })));
        Assert.Equal(retried[0].Id, retried[1].Id);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Task.Run(async () =>
        { using var db = f.Context(); await Management(db, f).PhanHoiAsync(5, id, new() { NoiDung = "Message " + i }); })));
        Assert.Equal(6, f.Scalar("SELECT COUNT(*) FROM bao_hong_trao_doi"));
        Assert.Equal(1, f.Scalar("SELECT phien_ban FROM bao_hong"));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AuditFailureRollsBackStatusVersionAndFeedback(bool changeState)
    {
        using var f = new Fixture(); Managers(f); int id;
        using (var db = f.Context()) id = (await new BaoHongService(db, f.Time).GuiAsync(2, Input())).Id;
        f.Execute("CREATE TRIGGER fail_reply_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.hanh_dong IN ('PHAN_HOI','DOI_TRANG_THAI') BEGIN SELECT RAISE(ABORT,'fail'); END");
        using (var db = f.Context())
        {
            var service = Management(db, f);
            if (changeState) await Assert.ThrowsAsync<SqliteException>(() => service.CapNhatAsync(5, id, Update("DANG_XU_LY")));
            else await Assert.ThrowsAsync<SqliteException>(() => service.PhanHoiAsync(5, id, new() { NoiDung = "Not saved" }));
        }
        using var check = f.Context(); var report = await check.BaoHongs.SingleAsync();
        Assert.Equal("MOI", report.TrangThai); Assert.Equal(0, report.PhienBan);
        Assert.Empty(await check.BaoHongTraoDois.ToListAsync());
    }

    [Fact]
    public async Task UpgradeFromV29PreservesReportAndPhotoRowsAndAddsNoInventedHistory()
    {
        using var f = new Fixture(); int id;
        using (var db = f.Context())
        { var input = Input(); input.Images = [Photo()]; id = (await new BaoHongService(db, f.Time).GuiAsync(2, input)).Id; }
        SchemaTestFixture.RemoveTatToanExtensions(f.Path);
        SchemaTestFixture.RemoveChiPhiExtensions(f.Path);
        f.Execute("DROP TABLE bao_hong_trao_doi; ALTER TABLE bao_hong DROP COLUMN phien_ban; DELETE FROM app_schema_version WHERE version>=30");
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(System.IO.Path.Combine(root, "QL_PhongTro", "Data", "permissions.seed.json"))) root = Directory.GetParent(root)!.FullName;
        var seed = System.IO.Path.Combine(root, "QL_PhongTro", "Data", "permissions.seed.json");
        DatabaseUpdates.Update(f.Path, seed); DatabaseUpdates.Update(f.Path, seed); DatabaseUpdates.Check(f.Path);
        using var check = f.Context(); Assert.Equal(0, (await check.BaoHongs.SingleAsync(x => x.Id == id)).PhienBan);
        Assert.Single(await check.AnhBaoHongs.ToListAsync()); Assert.Empty(await check.BaoHongTraoDois.ToListAsync());
        Assert.Equal(39, f.Scalar("SELECT MAX(version) FROM app_schema_version"));
    }
}
