using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    private int ApprovedCancellationFixture(string type = "XEM_PHONG")
    {
        var id = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "DA_DUYET", "YC-CANCEL-APPROVED");
        Execute("""
            UPDATE phong_tro SET trang_thai='DA_DAT_COC' WHERE id=(SELECT t.phong_id FROM tin_dang t JOIN yeu_cau_thue r ON r.tin_dang_id=t.id WHERE r.id=$id);
            UPDATE tin_dang SET trang_thai='DA_CHO_THUE' WHERE id=(SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$id);
            UPDATE yeu_cau_thue SET loai_yeu_cau=$type WHERE id=$id;
            """, ("$id", id), ("$type", type));
        return id;
    }

    private int CancellationRoom(int id) => Convert.ToInt32(Scalar(
        "SELECT phong_id FROM tin_dang WHERE id=(SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$id)", ("$id", id)));

    private AppDbContext CancellationContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(new SqliteConnectionStringBuilder { DataSource = database, ForeignKeys = true, Pooling = false }.ToString()).Options);

    private Task<bool> CancelApproved(int id, HuyYeuCauDaDuyetInput? input = null, int? actor = null)
    {
        return Run();
        async Task<bool> Run()
        {
            using var db = CancellationContext();
            return await new LichHenService(db, new MockTimeProvider()).HuyDaDuyetAsync(id,
                actor ?? accounts["CHU_NHA"], input ?? CancellationInput(), default);
        }
    }

    private static HuyYeuCauDaDuyetInput CancellationInput() => new()
        { PhienBan = 0, PhienBanPhong = 0, LyDo = "Khách đổi ý sau khi xem phòng", XacNhan = true };

    private async Task<Dictionary<string, string>> CancellationFields(HttpClient owner, int id)
    {
        var page = await owner.GetStringAsync($"/LichHen/ChiTiet/{id}");
        return new() { ["id"] = id.ToString(), ["PhienBan"] = "0", ["PhienBanPhong"] = "0",
            ["LyDo"] = "Khách đổi ý sau khi xem phòng", ["XacNhan"] = "true", ["__RequestVerificationToken"] = AntiForgery(page) };
    }

    [Theory]
    [InlineData("XEM_PHONG")]
    [InlineData("THUE_NGAY")]
    public async Task ApprovedCancellationReleasesRoomRepublishesListingAndRecordsHistory(string type)
    {
        var id = ApprovedCancellationFixture(type); var room = CancellationRoom(id);
        using var owner = await Login("CHU_NHA");
        var page = WebUtility.HtmlDecode(await owner.GetStringAsync($"/LichHen/ChiTiet/{id}"));
        Assert.Contains("Hủy yêu cầu đã duyệt", page);
        var fields = await CancellationFields(owner, id);
        Assert.Equal(HttpStatusCode.Redirect, (await owner.PostAsync("/LichHen/HuyDaDuyet", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal("DA_HUY", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("TRONG", Scalar("SELECT trang_thai FROM phong_tro WHERE id=$room", ("$room", room)));
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE phong_id=$room", ("$room", room)));
        Assert.Equal(1L, Scalar("SELECT phien_ban FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(1L, Scalar("SELECT phien_ban FROM phong_tro WHERE id=$room", ("$room", room)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id AND hanh_dong='HUY_YEU_CAU_DA_DUYET'", ("$id", id)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id AND loai='YEU_CAU_DA_HUY'", ("$id", id)));
        Assert.Equal(2L, Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong WHERE loai_doi_tuong IN ('phong_tro','tin_dang')"));
        Assert.DoesNotContain("Khách đổi ý", Scalar("SELECT COALESCE(group_concat(du_lieu_sau),'') FROM nhat_ky_hoat_dong")?.ToString());
        using var tenant = await Login("KHACH_THUE");
        var tenantPage = WebUtility.HtmlDecode(await tenant.GetStringAsync($"/LichHen/ChiTiet/{id}"));
        Assert.Contains("Khách đổi ý sau khi xem phòng", tenantPage);
        Assert.DoesNotContain("Hủy yêu cầu đã duyệt", tenantPage);
        page = WebUtility.HtmlDecode(await owner.GetStringAsync($"/LichHen/ChiTiet/{id}"));
        Assert.Contains("Đã huỷ", page);
        Assert.DoesNotContain("Hủy yêu cầu đã duyệt", page);
        Assert.False(await CancelApproved(id)); // No repeated history, notification, or room changes.
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Theory]
    [InlineData("KHACH_THUE")]
    [InlineData("QUAN_LY")]
    [InlineData("ADMIN")]
    public async Task ApprovedCancellationDeniesOtherRoles(string role)
    {
        var id = ApprovedCancellationFixture(); using var client = await Login(role);
        var token = AntiForgery(await client.GetStringAsync("/"));
        var fields = new Dictionary<string, string> { ["id"] = id.ToString(), ["PhienBan"] = "0", ["PhienBanPhong"] = "0",
            ["LyDo"] = "Khách đổi ý", ["XacNhan"] = "true", ["__RequestVerificationToken"] = token };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/LichHen/HuyDaDuyet", new FormUrlEncodedContent(fields))).StatusCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CancelApproved(id, actor: accounts[role]));
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task ApprovedCancellationRequiresCsrfAndWritePermission()
    {
        var id = ApprovedCancellationFixture(); using var owner = await Login("CHU_NHA");
        var fields = await CancellationFields(owner, id); var token = fields["__RequestVerificationToken"];
        fields.Remove("__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/LichHen/HuyDaDuyet", new FormUrlEncodedContent(fields))).StatusCode);
        fields["__RequestVerificationToken"] = token;
        Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='CHU_NHA' AND ModuleCode='YEU_CAU_THUE'");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync("/LichHen/HuyDaDuyet", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.DoesNotContain("Hủy yêu cầu đã duyệt", WebUtility.HtmlDecode(await owner.GetStringAsync($"/LichHen/ChiTiet/{id}")));
    }

    [Theory]
    [InlineData("", true, 0, 0)]
    [InlineData("     ", true, 0, 0)]
    [InlineData("Khách không thuê", false, 0, 0)]
    [InlineData("Khách không thuê", true, -1, 0)]
    [InlineData("Khách không thuê", true, 0, -1)]
    public async Task ApprovedCancellationRejectsMissingReasonConfirmationOrStaleVersions(string reason, bool confirmed, int version, int roomVersion)
    {
        var id = ApprovedCancellationFixture();
        await Assert.ThrowsAnyAsync<Exception>(() => CancelApproved(id, new()
            { LyDo = reason, XacNhan = confirmed, PhienBan = version, PhienBanPhong = roomVersion }));
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("DA_DAT_COC", Scalar("SELECT trang_thai FROM phong_tro WHERE id=$room", ("$room", CancellationRoom(id))));
    }

    [Theory]
    [InlineData("NHAP")]
    [InlineData("CHO_HIEU_LUC")]
    [InlineData("DANG_HIEU_LUC")]
    [InlineData("DA_HUY")]
    public async Task ApprovedCancellationDoesNotCancelExistingContract(string status)
    {
        var id = ApprovedCancellationFixture();
        Execute("INSERT INTO hop_dong(ma_hop_dong,phong_id,yeu_cau_thue_id,khach_dung_ten_id,trang_thai) SELECT 'HD-GUARD',t.phong_id,r.id,r.khach_thue_id,$status FROM yeu_cau_thue r JOIN tin_dang t ON t.id=r.tin_dang_id WHERE r.id=$id",
            ("$room", CancellationRoom(id)), ("$id", id), ("$status", status));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CancelApproved(id));
        Assert.Equal(status, Scalar("SELECT trang_thai FROM hop_dong WHERE ma_hop_dong='HD-GUARD'"));
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Theory]
    [InlineData("history")]
    [InlineData("audit")]
    [InlineData("notice")]
    public async Task ApprovedCancellationRollsBackEveryChangeWhenLoggingFails(string step)
    {
        var id = ApprovedCancellationFixture();
        var table = step switch { "history" => "yeu_cau_thue_lich_su", "audit" => "nhat_ky_hoat_dong", _ => "yeu_cau_thue_thong_bao" };
        Execute($"CREATE TRIGGER fail_cancellation BEFORE INSERT ON {table} BEGIN SELECT RAISE(ABORT,'test failure'); END");
        await Assert.ThrowsAsync<SqliteException>(() => CancelApproved(id));
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("DA_DAT_COC", Scalar("SELECT trang_thai FROM phong_tro WHERE id=$room", ("$room", CancellationRoom(id))));
        Assert.Equal("DA_CHO_THUE", Scalar("SELECT trang_thai FROM tin_dang WHERE phong_id=$room", ("$room", CancellationRoom(id))));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong"));
    }

    [Fact]
    public async Task ApprovedCancellationConcurrentPostsWriteOneHistory()
    {
        var id = ApprovedCancellationFixture();
        var results = await Task.WhenAll(Task.Run(() => CancelApproved(id)), Task.Run(() => CancelApproved(id)));
        Assert.Single(results, x => x); Assert.Single(results, x => !x);
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task ApprovedCancellationDeniesAnotherOwner()
    {
        var id = ApprovedCancellationFixture();
        Execute("INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,ngay_tao,ngay_cap_nhat) VALUES('Other owner','other-owner@test.local','0999999988','unused','CHU_NHA',1,'2026-01-01','2026-01-01')");
        var other = Convert.ToInt32(Scalar("SELECT id FROM tai_khoan WHERE email='other-owner@test.local'"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CancelApproved(id, actor: other));
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Theory]
    [InlineData("TRONG")]
    [InlineData("DANG_THUE")]
    [InlineData("NGUNG_CHO_THUE")]
    public async Task ApprovedCancellationDoesNotOverwriteChangedRoomState(string state)
    {
        var id = ApprovedCancellationFixture();
        Execute("UPDATE phong_tro SET trang_thai=$state WHERE id=$room", ("$state", state), ("$room", CancellationRoom(id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CancelApproved(id));
        Assert.Equal(state, Scalar("SELECT trang_thai FROM phong_tro WHERE id=$room", ("$room", CancellationRoom(id))));
    }

    [Fact]
    public async Task ApprovedCancellationFailsClosedForUnknownDepositSchema()
    {
        var id = ApprovedCancellationFixture();
        Execute("CREATE TABLE giao_dich_coc(id INTEGER PRIMARY KEY)");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CancelApproved(id));
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task ApprovedCancellationRenewsExpiredSourceListing()
    {
        var id = ApprovedCancellationFixture();
        Execute("UPDATE tin_dang SET ngay_het_han='2000-01-01' WHERE phong_id=$room", ("$room", CancellationRoom(id)));
        Assert.True(await CancelApproved(id));
        var published = DateTime.Parse(Scalar("SELECT ngay_dang FROM tin_dang WHERE phong_id=$room", ("$room", CancellationRoom(id)))!.ToString()!);
        var expiry = DateTime.Parse(Scalar("SELECT ngay_het_han FROM tin_dang WHERE phong_id=$room", ("$room", CancellationRoom(id)))!.ToString()!, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(published.AddDays(30), expiry);
        Assert.True(expiry > DateTime.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task ApprovedCancellationDoesNotRepublishInactiveBuilding()
    {
        var id = ApprovedCancellationFixture();
        Execute("UPDATE toa_nha SET dang_hoat_dong=0 WHERE id=(SELECT toa_nha_id FROM phong_tro WHERE id=$room)", ("$room", CancellationRoom(id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CancelApproved(id));
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("DA_DAT_COC", Scalar("SELECT trang_thai FROM phong_tro WHERE id=$room", ("$room", CancellationRoom(id))));
    }

    [Fact]
    public async Task ApprovedCancellationDoesNotOverwriteAnotherPublicListing()
    {
        var id = ApprovedCancellationFixture();
        Execute("INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao) SELECT phong_id,nguoi_dang_id,'Other public listing','DANG_HIEN_THI',ngay_tao FROM tin_dang WHERE id=(SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$id)", ("$id", id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CancelApproved(id));
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM tin_dang WHERE phong_id=$room AND trang_thai='DANG_HIEN_THI'", ("$room", CancellationRoom(id))));
    }

    [Fact]
    public async Task ApprovedCancellationRepublishesHiddenSourceListing()
    {
        var id = ApprovedCancellationFixture();
        Execute("UPDATE tin_dang SET trang_thai='TAM_AN' WHERE phong_id=$room", ("$room", CancellationRoom(id)));
        Assert.True(await CancelApproved(id));
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE phong_id=$room", ("$room", CancellationRoom(id))));
    }
}
