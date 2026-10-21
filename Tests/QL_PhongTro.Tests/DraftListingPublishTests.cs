using System.Diagnostics;
using System.Net;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    private static Task<HttpResponseMessage> PublishDraft(HttpClient client, int id, string token, string endpoint = "Dang") =>
        client.PostAsync("/TinDang/" + endpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = id.ToString(), ["__RequestVerificationToken"] = token,
            ["trangThai"] = "DANG_HIEN_THI", ["trangThaiDaXem"] = "NHAP"
        }));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishDraft_OneClickPreservesContentImagesAndDatesOnRepeat(bool photo)
    {
        var id = CreatePublicListing("TRONG", "NHAP", photo);
        Execute("UPDATE tin_dang SET noi_dung=NULL,ngay_dang=NULL,ngay_het_han=NULL WHERE id=$id", ("$id", id));
        using var owner = await Login("CHU_NHA");
        var html = await owner.GetStringAsync("/TinDang/QuanLy");
        Assert.Contains("lm-publish-button", html);
        var token = AntiForgery(html);
        var before = Convert.ToInt64(Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong"));
        var watch = Stopwatch.StartNew();
        Assert.Equal(HttpStatusCode.Redirect, (await PublishDraft(owner, id, token)).StatusCode);
        watch.Stop(); Assert.True(watch.Elapsed < TimeSpan.FromMinutes(1));
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(DBNull.Value, Scalar("SELECT noi_dung FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM tin_dang"));
        Assert.Equal(photo ? 1L : 0L, Scalar("SELECT COUNT(*) FROM anh_phong"));
        var published = Scalar("SELECT ngay_dang FROM tin_dang WHERE id=$id", ("$id", id));
        var expiry = Scalar("SELECT ngay_het_han FROM tin_dang WHERE id=$id", ("$id", id));
        Assert.Equal(TimeSpan.FromDays(30), DateTime.Parse(expiry!.ToString()!) - DateTime.Parse(published!.ToString()!));
        Assert.Equal(before + 1, Convert.ToInt64(Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong")));
        Assert.Equal(HttpStatusCode.Redirect, (await PublishDraft(owner, id, token)).StatusCode);
        Assert.Equal(published, Scalar("SELECT ngay_dang FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(expiry, Scalar("SELECT ngay_het_han FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(before + 1, Convert.ToInt64(Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong")));
        html = await owner.GetStringAsync("/TinDang/QuanLy");
        Assert.DoesNotContain("lm-publish-button", html);
        using var guest = Client();
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/tin-dang/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("UPDATE tin_dang SET tieu_de='   '", "Tiêu đề")]
    [InlineData("UPDATE tin_dang SET tieu_de=replace(hex(zeroblob(101)),'0','a')", "Tiêu đề")]
    [InlineData("UPDATE tin_dang SET noi_dung=hex(zeroblob(2001))", "Mô tả")]
    [InlineData("UPDATE phong_tro SET gia_thue=0", "Giá thuê")]
    [InlineData("UPDATE phong_tro SET dien_tich=0", "Diện tích")]
    [InlineData("UPDATE phong_tro SET so_nguoi_toi_da=0", "Sức chứa")]
    [InlineData("UPDATE toa_nha SET dia_chi=' '", "Địa chỉ")]
    [InlineData("UPDATE toa_nha SET dang_hoat_dong=0", "hoạt động")]
    [InlineData("UPDATE phong_tro SET trang_thai='DANG_THUE'", "trống")]
    [InlineData("UPDATE phong_tro SET trang_thai='DA_DAT_COC'", "trống")]
    [InlineData("UPDATE phong_tro SET trang_thai='NGUNG_CHO_THUE'", "trống")]
    public async Task PublishDraft_InvalidDataKeepsDraftAndExplains(string sql, string message)
    {
        var id = CreatePublicListing("TRONG", "NHAP", false);
        Execute("UPDATE tin_dang SET ngay_dang=NULL,ngay_het_han=NULL");
        using var owner = await Login("CHU_NHA");
        var token = AntiForgery(await owner.GetStringAsync("/TinDang/QuanLy"));
        // Inject legacy/corrupt values only in this disposable database to exercise backend guards.
        Execute("PRAGMA ignore_check_constraints=ON; " + sql);
        var count = Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong");
        Assert.Equal(HttpStatusCode.Redirect, (await PublishDraft(owner, id, token)).StatusCode);
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(DBNull.Value, Scalar("SELECT ngay_dang FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(count, Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong"));
        Assert.Contains(message, WebUtility.HtmlDecode(await owner.GetStringAsync("/TinDang/QuanLy")));
    }

    [Theory]
    [InlineData("Dang")]
    [InlineData("DoiTrangThai")]
    public async Task PublishDraft_ExistingPublicListingIsNotOverwritten(string endpoint)
    {
        var id = CreatePublicListing("TRONG", "NHAP", false);
        var (room, _) = GetListingRoomAndBuilding(id);
        Execute("INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao) VALUES($room,$owner,'Tin công khai cũ','DANG_HIEN_THI','2026-01-01')",
            ("$room", room), ("$owner", accounts["CHU_NHA"]));
        using var owner = await Login("CHU_NHA");
        var token = AntiForgery(await owner.GetStringAsync("/TinDang/QuanLy"));
        Assert.Equal(HttpStatusCode.Redirect, (await PublishDraft(owner, id, token, endpoint)).StatusCode);
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM tin_dang WHERE trang_thai='DANG_HIEN_THI'"));
    }

    [Theory]
    [InlineData("TAM_AN")]
    [InlineData("DA_CHO_THUE")]
    public async Task PublishDraft_StaleButtonCannotPublishDifferentState(string state)
    {
        var id = CreatePublicListing("TRONG", state, false);
        using var owner = await Login("CHU_NHA");
        var token = AntiForgery(await owner.GetStringAsync("/TinDang/QuanLy"));
        await PublishDraft(owner, id, token);
        Assert.Equal(state, Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
    }

    [Theory]
    [InlineData("KHACH_THUE", false)]
    [InlineData("ADMIN", false)]
    [InlineData("QUAN_LY", false)]
    [InlineData("CHU_NHA", true)]
    public async Task PublishDraft_RoleAndOwnershipChecked(string role, bool foreignOwner)
    {
        var id = CreatePublicListing("TRONG", "NHAP", false);
        using var client = await Login(role);
        // Obtain a valid CSRF pair from the login form, available to every role.
        var token = AntiForgery(await client.GetStringAsync("/Account/Login"));
        if (foreignOwner) Execute("UPDATE toa_nha SET chu_nha_id=$other", ("$other", accounts["ADMIN"]));
        Assert.Equal(HttpStatusCode.Forbidden, (await PublishDraft(client, id, token)).StatusCode);
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task PublishDraft_AssignedManagerCanPublishButCsrfAndReadOnlyCannot()
    {
        var id = CreatePublicListing("TRONG", "NHAP", false);
        Execute("UPDATE toa_nha SET quan_ly_id=$manager", ("$manager", accounts["QUAN_LY"]));
        using var manager = await Login("QUAN_LY");
        var token = AntiForgery(await manager.GetStringAsync("/TinDang/QuanLy"));
        Assert.Equal(HttpStatusCode.BadRequest, (await PublishDraft(manager, id, "")).StatusCode);
        Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='QUAN_LY' AND ModuleCode='TIN_DANG'");
        Assert.Equal(HttpStatusCode.Forbidden, (await PublishDraft(manager, id, token)).StatusCode);
        Execute("UPDATE role_permission SET AccessLevel='WRITE' WHERE RoleCode='QUAN_LY' AND ModuleCode='TIN_DANG'");
        Assert.Equal(HttpStatusCode.Redirect, (await PublishDraft(manager, id, token)).StatusCode);
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task PublishDraft_ConcurrentRequestsPublishAndAuditOnlyOnce()
    {
        var id = CreatePublicListing("TRONG", "NHAP", false);
        using var owner = await Login("CHU_NHA");
        var token = AntiForgery(await owner.GetStringAsync("/TinDang/QuanLy"));
        var count = Convert.ToInt64(Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong"));
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PublishDraft(owner, id, token)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM tin_dang"));
        Assert.Equal(count + 1, Convert.ToInt64(Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong")));
    }

    [Fact]
    public async Task PublishDraft_AuditFailureRollsBackPublicationAndDates()
    {
        var id = CreatePublicListing("TRONG", "NHAP", false);
        Execute("UPDATE tin_dang SET ngay_dang=NULL,ngay_het_han=NULL");
        using var owner = await Login("CHU_NHA");
        var token = AntiForgery(await owner.GetStringAsync("/TinDang/QuanLy"));
        Execute("CREATE TRIGGER publish_audit_failure BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='tin_dang' BEGIN SELECT RAISE(ABORT,'Synthetic audit failure'); END");
        Assert.Equal(HttpStatusCode.Redirect, (await PublishDraft(owner, id, token)).StatusCode);
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(DBNull.Value, Scalar("SELECT ngay_dang FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal(DBNull.Value, Scalar("SELECT ngay_het_han FROM tin_dang WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task PublishDraft_EditAndStatusMenuCannotBypassRequiredFields()
    {
        var id = CreatePublicListing("TRONG", "NHAP", false);
        var (room, _) = GetListingRoomAndBuilding(id);
        using var owner = await Login("CHU_NHA");
        var token = AntiForgery(await owner.GetStringAsync($"/TinDang/Sua/{id}"));
        Execute("PRAGMA ignore_check_constraints=ON; UPDATE phong_tro SET gia_thue=0");
        var edited = await owner.PostAsync("/TinDang/Tao", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["PhongId"] = room.ToString(), ["TinDangId"] = id.ToString(),
            ["intent"] = "DANG_HIEN_THI", ["TieuDe"] = "Tiêu đề đã sửa"
        }));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Contains("Giá thuê", WebUtility.HtmlDecode(await edited.Content.ReadAsStringAsync()));
        await PublishDraft(owner, id, token, "DoiTrangThai");
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal("Phòng gần trung tâm, có cửa sổ", Scalar("SELECT tieu_de FROM tin_dang WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task PublishDraft_AnonymousMissingAndGetAreRejected()
    {
        using var guest = Client();
        Assert.Equal(HttpStatusCode.Redirect, (await PublishDraft(guest, 1, "")).StatusCode);
        using var owner = await Login("CHU_NHA");
        var token = AntiForgery(await owner.GetStringAsync("/TinDang/QuanLy"));
        Assert.Equal(HttpStatusCode.NotFound, (await PublishDraft(owner, int.MaxValue, token)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await owner.GetAsync("/TinDang/Dang/1")).StatusCode);
    }

    [Fact]
    public async Task PublishDraft_CompetingDraftsCannotBothBecomePublic()
    {
        var first = CreatePublicListing("TRONG", "NHAP", false);
        var (room, _) = GetListingRoomAndBuilding(first);
        Execute("INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao) VALUES($room,$owner,'Tin thứ hai','NHAP','2026-01-01')",
            ("$room", room), ("$owner", accounts["CHU_NHA"]));
        var second = Convert.ToInt32(Scalar("SELECT MAX(id) FROM tin_dang"));
        using var owner = await Login("CHU_NHA");
        var token = AntiForgery(await owner.GetStringAsync("/TinDang/QuanLy"));
        var results = await Task.WhenAll(PublishDraft(owner, first, token), PublishDraft(owner, second, token));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM tin_dang WHERE trang_thai='DANG_HIEN_THI'"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM tin_dang WHERE trang_thai='NHAP'"));
    }

    [Fact]
    public async Task PublishDraft_NormalEditorStillPublishesEditedContent()
    {
        var id = CreatePublicListing("TRONG", "NHAP", false);
        var (room, _) = GetListingRoomAndBuilding(id);
        using var owner = await Login("CHU_NHA");
        var token = AntiForgery(await owner.GetStringAsync($"/TinDang/Sua/{id}"));
        var response = await owner.PostAsync("/TinDang/Tao", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["PhongId"] = room.ToString(), ["TinDangId"] = id.ToString(),
            ["intent"] = "DANG_HIEN_THI", ["TieuDe"] = "Tiêu đề mới", ["NoiDung"] = "Mô tả mới"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal("Tiêu đề mới", Scalar("SELECT tieu_de FROM tin_dang WHERE id=$id", ("$id", id)));
        Assert.Equal("Mô tả mới", Scalar("SELECT noi_dung FROM tin_dang WHERE id=$id", ("$id", id)));
    }
}
