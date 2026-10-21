using System.Net;
using System.Text.RegularExpressions;
using QL_PhongTro.Data;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    private const string VacantRoomsUrl = "/Home/PhongTrongChuaDangTin";
    private static string VacantDashboardCard(string html) => Regex.Match(WebUtility.HtmlDecode(html),
        "<article class=\"owner-work-card owner-vacant-card\">[\\s\\S]*?</article>").Value;
    private static int[] VacantRoomIds(string html) => Regex.Matches(html, "data-vacant-room=\"(\\d+)\"")
        .Select(match => int.Parse(match.Groups[1].Value)).ToArray();

    [Theory]
    [InlineData("TRONG", "NHAP", "future", true)]
    [InlineData("TRONG", "TAM_AN", "future", true)]
    [InlineData("TRONG", "DA_CHO_THUE", "future", true)]
    [InlineData("TRONG", "DANG_HIEN_THI", "future", false)]
    [InlineData("TRONG", "DANG_HIEN_THI", "none", false)]
    [InlineData("TRONG", "DANG_HIEN_THI", "expired", true)]
    [InlineData("DANG_THUE", "NHAP", "future", false)]
    [InlineData("DA_DAT_COC", "NHAP", "future", false)]
    public async Task VacantRooms_EligibilityIsConsistentOnDashboardAndFullList(string roomStatus, string postStatus, string expiry, bool expected)
    {
        RentalRequestSchema.Initialize(database);
        var post = CreatePublicListing(roomStatus, postStatus, false);
        var (room, _) = GetListingRoomAndBuilding(post);
        if (expiry == "none") Execute("UPDATE tin_dang SET ngay_het_han=NULL WHERE id=$id", ("$id", post));
        if (expiry == "expired") Execute("UPDATE tin_dang SET ngay_het_han='2020-01-01' WHERE id=$id", ("$id", post));
        using var owner = await Login("CHU_NHA");
        var page = await owner.GetStringAsync(VacantRoomsUrl);
        var card = VacantDashboardCard(await owner.GetStringAsync("/"));
        Assert.NotEmpty(card);
        Assert.Equal(expected ? new[] { room } : [], VacantRoomIds(page));
        Assert.Equal(expected ? new[] { room } : [], VacantRoomIds(card));
        Assert.Contains($"data-unlisted-count=\"{(expected ? 1 : 0)}\"", page);
        Assert.Contains($"data-unlisted-count=\"{(expected ? 1 : 0)}\"", card);
        if (expected) Assert.Contains($"/TinDang/Sua/{post}", page);
    }

    [Fact]
    public async Task VacantRooms_NoListingHasCreateLinkWithoutChangingDatabase()
    {
        RentalRequestSchema.Initialize(database);
        var post = CreatePublicListing("TRONG", "NHAP", false);
        var (room, _) = GetListingRoomAndBuilding(post);
        Execute("DELETE FROM tin_dang WHERE id=$id", ("$id", post));
        var count = Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong");
        using var owner = await Login("CHU_NHA");
        var html = WebUtility.HtmlDecode(await owner.GetStringAsync(VacantRoomsUrl));
        Assert.Contains($"/TinDang/Tao?phongId={room}", html);
        Assert.Contains("Chưa có tin", html);
        Assert.Equal(new[] { room }, VacantRoomIds(html));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM tin_dang"));
        Assert.Equal(count, Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong"));
    }

    [Fact]
    public async Task VacantRooms_AnyLiveListingExcludesRoomEvenWhenNewestIsDraft()
    {
        RentalRequestSchema.Initialize(database);
        var post = CreatePublicListing("TRONG", "DANG_HIEN_THI", false);
        var (room, _) = GetListingRoomAndBuilding(post);
        Execute("UPDATE tin_dang SET ngay_het_han=NULL WHERE id=$id", ("$id", post));
        Execute("INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao) VALUES($room,$owner,'Bản Nháp mới','NHAP','2026-01-01')",
            ("$room", room), ("$owner", accounts["CHU_NHA"]));
        using var owner = await Login("CHU_NHA");
        Assert.Empty(VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl)));
        Assert.Empty(VacantRoomIds(VacantDashboardCard(await owner.GetStringAsync("/"))));
    }

    [Fact]
    public async Task VacantRooms_LatestDraftIsOpenedWithoutDuplicatingRoom()
    {
        RentalRequestSchema.Initialize(database);
        var old = CreatePublicListing("TRONG", "DA_CHO_THUE", false);
        var (room, _) = GetListingRoomAndBuilding(old);
        Execute("INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao) VALUES($room,$owner,'Nháp cần kiểm tra','NHAP','2026-01-02')",
            ("$room", room), ("$owner", accounts["CHU_NHA"]));
        var latest = Convert.ToInt32(Scalar("SELECT MAX(id) FROM tin_dang"));
        using var owner = await Login("CHU_NHA");
        var html = WebUtility.HtmlDecode(await owner.GetStringAsync(VacantRoomsUrl));
        Assert.Equal(new[] { room }, VacantRoomIds(html));
        Assert.Contains($"/TinDang/Sua/{latest}", html);
        Assert.DoesNotContain($"/TinDang/Sua/{old}\"", html);
        Assert.Contains("Kiểm tra Nháp", html);
    }

    [Fact]
    public async Task VacantRooms_OwnerActiveBuildingScopeAndFilterCannotLeak()
    {
        RentalRequestSchema.Initialize(database);
        var own = CreatePublicListing("TRONG", "NHAP", false);
        var otherOwn = CreatePublicListing("TRONG", "NHAP", false);
        var foreign = CreatePublicListing("TRONG", "NHAP", false);
        var inactive = CreatePublicListing("TRONG", "NHAP", false);
        var (ownRoom, ownBuilding) = GetListingRoomAndBuilding(own);
        var (otherRoom, _) = GetListingRoomAndBuilding(otherOwn);
        var (_, foreignBuilding) = GetListingRoomAndBuilding(foreign);
        var (_, inactiveBuilding) = GetListingRoomAndBuilding(inactive);
        Execute("UPDATE toa_nha SET chu_nha_id=$other WHERE id=$id", ("$other", accounts["ADMIN"]), ("$id", foreignBuilding));
        Execute("UPDATE toa_nha SET dang_hoat_dong=0 WHERE id=$id", ("$id", inactiveBuilding));
        using var owner = await Login("CHU_NHA");
        Assert.Equal(new[] { ownRoom, otherRoom }.Order(), VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl)).Order());
        Assert.Equal(new[] { ownRoom }, VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl + $"?toaNhaId={ownBuilding}")));
        Assert.Equal(new[] { ownRoom }, VacantRoomIds(VacantDashboardCard(await owner.GetStringAsync($"/?toaNhaId={ownBuilding}"))));
        foreach (var id in new[] { foreignBuilding, inactiveBuilding, int.MaxValue, -1 })
            Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(VacantRoomsUrl + $"?toaNhaId={id}")).StatusCode);
    }

    [Theory]
    [InlineData("KHACH_THUE")]
    [InlineData("QUAN_LY")]
    [InlineData("ADMIN")]
    public async Task VacantRooms_OtherRolesCannotOpenOwnerList(string role)
    {
        RentalRequestSchema.Initialize(database);
        CreatePublicListing("TRONG", "NHAP", false);
        using var client = await Login(role);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(VacantRoomsUrl)).StatusCode);
        Assert.Empty(VacantDashboardCard(await client.GetStringAsync("/")));
    }

    [Fact]
    public async Task VacantRooms_AnonymousIsChallengedAndEmptyOwnerHasCorrectMessage()
    {
        RentalRequestSchema.Initialize(database);
        using var guest = Client();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.GetAsync(VacantRoomsUrl)).StatusCode);
        Assert.Empty(VacantDashboardCard(await guest.GetStringAsync("/")));
        using var owner = await Login("CHU_NHA");
        var html = WebUtility.HtmlDecode(await owner.GetStringAsync(VacantRoomsUrl));
        Assert.Contains("Không có phòng trống chưa đăng tin", html);
        Assert.Contains("Chưa có phòng trống", VacantDashboardCard(await owner.GetStringAsync("/")));
    }

    [Theory]
    [InlineData("TIN_DANG")]
    [InlineData("PHONG_TRO")]
    public async Task VacantRooms_RevokedModulePermissionHidesDataAndBlocksEndpoint(string module)
    {
        RentalRequestSchema.Initialize(database);
        CreatePublicListing("TRONG", "NHAP", false);
        using var owner = await Login("CHU_NHA");
        Assert.Single(VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl)));
        Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA' AND ModuleCode=$module", ("$module", module));
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(VacantRoomsUrl)).StatusCode);
        var card = VacantDashboardCard(await owner.GetStringAsync("/"));
        Assert.Empty(VacantRoomIds(card));
        Assert.DoesNotContain("data-unlisted-count", card);
        Assert.Contains("chưa được cấp quyền", card);
    }

    [Fact]
    public async Task VacantRooms_ReadOnlyCanViewButNotEditOrPublish()
    {
        RentalRequestSchema.Initialize(database);
        var post = CreatePublicListing("TRONG", "NHAP", false);
        Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='CHU_NHA' AND ModuleCode='TIN_DANG'");
        using var owner = await Login("CHU_NHA");
        var html = await owner.GetStringAsync(VacantRoomsUrl);
        Assert.Single(VacantRoomIds(html));
        Assert.DoesNotContain($"/TinDang/Sua/{post}", html);
        Assert.Contains("chỉ xem", WebUtility.HtmlDecode(html));
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/TinDang/Sua/{post}")).StatusCode);
    }

    [Fact]
    public async Task VacantRooms_PaginationIncludesAllRoomsOnceAndClampsPage()
    {
        RentalRequestSchema.Initialize(database);
        var post = CreatePublicListing("TRONG", "NHAP", false);
        var (_, building) = GetListingRoomAndBuilding(post);
        for(var i=2; i<=18; i++) Execute("INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES($building,$code,1,20,4000000,2,'TRONG','2026-01-01')",
            ("$building", building), ("$code", "P" + i.ToString("D2")));
        using var owner = await Login("CHU_NHA");
        var first = VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl));
        var second = VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl+"?trang=2"));
        Assert.Equal(15, first.Length); Assert.Equal(3, second.Length);
        Assert.Equal(18, first.Concat(second).Distinct().Count());
        Assert.Equal(first, VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl+"?trang=-1")));
        Assert.Equal(second, VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl+"?trang=2147483647")));
        var dashboard = VacantDashboardCard(await owner.GetStringAsync("/"));
        Assert.Equal(4, VacantRoomIds(dashboard).Length);
        Assert.Contains("data-unlisted-count=\"18\"", dashboard);
    }

    [Fact]
    public async Task VacantRooms_PublishingDraftRemovesRoomFromBothListsAndHidingRestoresIt()
    {
        RentalRequestSchema.Initialize(database);
        var post = CreatePublicListing("TRONG", "NHAP", false);
        using var owner = await Login("CHU_NHA");
        Assert.Single(VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl)));
        var token=AntiForgery(await owner.GetStringAsync("/TinDang/QuanLy"));
        Assert.Equal(HttpStatusCode.Redirect, (await PublishDraft(owner,post,token)).StatusCode);
        Assert.Empty(VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl)));
        Assert.Empty(VacantRoomIds(VacantDashboardCard(await owner.GetStringAsync("/"))));
        await owner.PostAsync($"/TinDang/Go/{post}",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",token}}));
        Assert.Single(VacantRoomIds(await owner.GetStringAsync(VacantRoomsUrl)));
        Assert.Single(VacantRoomIds(VacantDashboardCard(await owner.GetStringAsync("/"))));
    }
}
