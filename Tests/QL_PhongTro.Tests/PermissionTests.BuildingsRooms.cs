using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using QL_PhongTro.Models;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    private static async Task<string> PropertyToken(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return WebUtility.HtmlDecode(token);
    }

    private async Task<int> CreateTestBuilding(HttpClient client)
    {
        var response = await client.PostAsync("/PhongTro/TaoToaNha", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await PropertyToken(client, "/PhongTro/TaoToaNha"),
            ["TenToaNha"] = "Tòa kiểm thử", ["DiaChi"] = "12 Hà Nội", ["SoTang"] = "3"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Đã thêm tòa nhà thành công.", WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location)));
        return Convert.ToInt32(Scalar("SELECT id FROM toa_nha WHERE chu_nha_id=$id ORDER BY id DESC LIMIT 1", ("$id", accounts["CHU_NHA"])));
    }

    private static async Task<Dictionary<string, string>> RoomForm(HttpClient client, int buildingId) => new()
    {
        ["__RequestVerificationToken"] = await PropertyToken(client, "/PhongTro/Create"),
        ["ToaNhaId"] = buildingId.ToString(), ["MaPhong"] = "A101", ["Tang"] = "1",
        ["DienTich"] = "25", ["GiaThueDisplay"] = "2.000.000", ["SoNguoiToiDa"] = "2", ["TrangThai"] = "TRONG"
    };

    [Fact]
    public async Task BuildingRoomCrudShowsNotificationsAndRetainsBuildingSelection()
    {
        using var client = await Login("CHU_NHA");
        var buildingId = await CreateTestBuilding(client);
        var emptyPage = WebUtility.HtmlDecode(await client.GetStringAsync("/PhongTro"));
        Assert.Contains("Vui lòng chọn tòa nhà", emptyPage);
        Assert.Contains("value=\"\">Chọn tòa nhà", emptyPage);
        var createPage = await client.GetStringAsync("/PhongTro/Create");
        Assert.DoesNotContain($"selected=\"selected\" value=\"{buildingId}\"", createPage);

        var form = await RoomForm(client, buildingId);
        var created = await client.PostAsync("/PhongTro/Create", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Contains($"toaNhaId={buildingId}", created.Headers.Location!.ToString());
        var page = WebUtility.HtmlDecode(await client.GetStringAsync(created.Headers.Location));
        Assert.Contains("Đã thêm phòng thành công.", page);
        Assert.Contains("data-confirm=", page);
        Assert.Contains("A101", page);
        var roomId = Convert.ToInt32(Scalar("SELECT id FROM phong_tro WHERE toa_nha_id=$id", ("$id", buildingId)));
        Assert.Contains("Sửa phòng", WebUtility.HtmlDecode(await client.GetStringAsync($"/PhongTro/Edit/{roomId}")));
        form["MaPhong"] = "A102";
        var edited = await client.PostAsync($"/PhongTro/Edit/{roomId}", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        Assert.Contains("Đã cập nhật phòng thành công.", WebUtility.HtmlDecode(await client.GetStringAsync(edited.Headers.Location)));
        Assert.Equal("A102", Scalar("SELECT ma_phong FROM phong_tro WHERE id=$id", ("$id", roomId)));

        var buildingForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = form["__RequestVerificationToken"],
            ["TenToaNha"] = "Tòa đã sửa", ["DiaChi"] = "24 Hà Nội", ["SoTang"] = "4"
        };
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/PhongTro/SuaToaNha/{buildingId}", new FormUrlEncodedContent(buildingForm))).StatusCode);
        Assert.Contains("Đã cập nhật tòa nhà thành công.", WebUtility.HtmlDecode(await client.GetStringAsync("/PhongTro/ToaNha")));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/PhongTro/XoaToaNha/{buildingId}", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Contains("Không thể xóa tòa nhà đang có phòng", WebUtility.HtmlDecode(await client.GetStringAsync("/PhongTro/ToaNha")));

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/PhongTro/Delete/{roomId}", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Contains("Đã xóa phòng thành công.", WebUtility.HtmlDecode(await client.GetStringAsync($"/PhongTro?toaNhaId={buildingId}")));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM phong_tro WHERE id=$id", ("$id", roomId)));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/PhongTro/XoaToaNha/{buildingId}", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Contains("Đã xóa tòa nhà thành công.", WebUtility.HtmlDecode(await client.GetStringAsync("/PhongTro/ToaNha")));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM toa_nha WHERE id=$id", ("$id", buildingId)));
    }

    [Fact]
    public async Task BuildingRoomMutationsRejectInvalidInputAndProtectReferencedRooms()
    {
        using var client = await Login("CHU_NHA");
        var buildingId = await CreateTestBuilding(client);
        var form = await RoomForm(client, buildingId);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/PhongTro/Create", new FormUrlEncodedContent(form))).StatusCode);
        var roomId = Convert.ToInt32(Scalar("SELECT id FROM phong_tro WHERE toa_nha_id=$id", ("$id", buildingId)));
        form["GiaThueDisplay"] = "100";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/PhongTro/Edit/{roomId}", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(2000000L, Scalar("SELECT gia_thue FROM phong_tro WHERE id=$id", ("$id", roomId)));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/PhongTro/Delete/{roomId}", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Execute("UPDATE phong_tro SET trang_thai='DANG_THUE' WHERE id=$id", ("$id", roomId));
        await client.PostAsync($"/PhongTro/Delete/{roomId}", new FormUrlEncodedContent(form));
        Assert.Contains("Không thể xóa phòng đang thuê", WebUtility.HtmlDecode(await client.GetStringAsync($"/PhongTro?toaNhaId={buildingId}")));
        Execute("UPDATE phong_tro SET trang_thai='TRONG' WHERE id=$id; CREATE TABLE room_reference_test (room_id INTEGER REFERENCES phong_tro(id)); INSERT INTO room_reference_test VALUES ($id);", ("$id", roomId));
        await client.PostAsync($"/PhongTro/Delete/{roomId}", new FormUrlEncodedContent(form));
        Assert.Contains("Không thể xóa phòng đang được hợp đồng", WebUtility.HtmlDecode(await client.GetStringAsync($"/PhongTro?toaNhaId={buildingId}")));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM phong_tro WHERE id=$id", ("$id", roomId)));

        Execute("UPDATE toa_nha SET chu_nha_id=$other WHERE id=$id", ("$other", accounts["ADMIN"]), ("$id", buildingId));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/PhongTro/Edit/{roomId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/PhongTro/Edit/{roomId}", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/PhongTro/Delete/{roomId}", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/PhongTro/XoaToaNha/{buildingId}", new FormUrlEncodedContent(form))).StatusCode);
    }

    [Fact]
    public async Task RoomListDisablesListingActionForOccupiedRoomWithReason()
    {
        using var client = await Login("CHU_NHA");
        var buildingId = await CreateTestBuilding(client);
        Execute("""
            INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao,phien_ban)
            VALUES($building,'R001',1,25,2000000,0,2,'DANG_THUE',$created,0)
            """, ("$building", buildingId), ("$created", DateTime.UtcNow.ToString("O")));
        var roomId = Convert.ToInt32(Scalar("SELECT id FROM phong_tro WHERE toa_nha_id=$id AND ma_phong='R001'", ("$id", buildingId)));

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/PhongTro?toaNhaId={buildingId}"));
        var row = Regex.Match(html, "<tr>[\\s\\S]*?R001[\\s\\S]*?</tr>").Value;
        Assert.NotEmpty(row);
        Assert.Contains("disabled", row);
        Assert.Contains("Phòng đang được thuê", row);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/TinDang/Tao?phongId={roomId}")).StatusCode);
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM tin_dang WHERE phong_id=$id", ("$id", roomId)));
    }

    [Fact]
    public async Task LandlordCreatesDraftWithRoomDataLockedAndThirtyDayExpiry()
    {
        using var client = await Login("CHU_NHA");
        var buildingId = await CreateTestBuilding(client);
        var roomForm = await RoomForm(client, buildingId);
        roomForm["MaPhong"] = "DRAFT1";
        roomForm["DienTich"] = "25.5";
        roomForm["GiaThueDisplay"] = "2.500.000";
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/PhongTro/Create", new FormUrlEncodedContent(roomForm))).StatusCode);
        var roomId = Convert.ToInt32(Scalar("SELECT id FROM phong_tro WHERE toa_nha_id=$id AND ma_phong='DRAFT1'", ("$id", buildingId)));

        Execute("INSERT INTO anh_phong(phong_id,duong_dan,duong_dan_anh_nho,thu_tu,mo_ta,ngay_tao) VALUES($room,'/images/draft-room.jpg','/images/draft-room-small.jpg',1,'Phòng sáng',$created)",
            ("$room", roomId), ("$created", DateTime.UtcNow.ToString("O")));
        var electricity = CreateService(buildingId, "DIEN", "Điện");
        AddServicePrice(buildingId, electricity, accounts["CHU_NHA"], CachTinhDichVu.TheoChiSo, "kWh", 3500);
        AddRoomSelection(buildingId, roomId, electricity, 4200);
        var parking = CreateService(buildingId, "GUI_XE", "Gửi xe");
        AddServicePrice(buildingId, parking, accounts["CHU_NHA"], CachTinhDichVu.CoDinh, "phòng/tháng", 100000);

        var formUrl = $"/TinDang/Tao?phongId={roomId}";
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(formUrl));
        Assert.Contains("value=\"25,5 m²\" readonly", html);
        Assert.Contains("value=\"2.500.000 đ\" readonly", html);
        Assert.Contains("/images/draft-room-small.jpg", html);
        Assert.Contains("Điện", html);
        Assert.Contains("4.200", html);
        Assert.Contains("Gửi xe", html);
        Assert.Contains("100.000", html);
        var titleInput = Regex.Match(html, "<input[^>]*id=\"TieuDe\"[^>]*>").Value;
        Assert.NotEmpty(titleInput);
        Assert.DoesNotContain("readonly", titleInput);
        var descriptionField = Regex.Match(html, "<textarea[^>]*id=\"MoTaThem\"[^>]*>").Value;
        Assert.NotEmpty(descriptionField);
        Assert.DoesNotContain("readonly", descriptionField);
        Assert.Contains("id=\"NgayHetHan\"", html);
        Assert.Contains("readonly", Regex.Match(html, "<input[^>]*id=\"NgayHetHan\"[^>]*>").Value);

        var token = await PropertyToken(client, formUrl);
        var response = await client.PostAsync("/TinDang/Tao", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["PhongId"] = roomId.ToString(),
            ["TieuDe"] = "Phòng DRAFT1 thoáng mát",
            ["MoTaThem"] = "Có cửa sổ và chỗ để xe.",
            ["DienTich"] = "1",
            ["GiaThue"] = "1"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.Equal("Phòng DRAFT1 thoáng mát", Scalar("SELECT tieu_de FROM tin_dang WHERE phong_id=$id", ("$id", roomId)));
        Assert.Equal("Có cửa sổ và chỗ để xe.", Scalar("SELECT noi_dung FROM tin_dang WHERE phong_id=$id", ("$id", roomId)));
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM tin_dang WHERE phong_id=$id", ("$id", roomId)));
        var created = DateTime.Parse(Convert.ToString(Scalar("SELECT ngay_tao FROM tin_dang WHERE phong_id=$id", ("$id", roomId)))!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var expires = DateTime.Parse(Convert.ToString(Scalar("SELECT ngay_het_han FROM tin_dang WHERE phong_id=$id", ("$id", roomId)))!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.Equal(TimeSpan.FromDays(30), expires - created);
        Assert.Equal(25.5m, Convert.ToDecimal(Scalar("SELECT dien_tich FROM phong_tro WHERE id=$id", ("$id", roomId))));
        Assert.Equal(2500000L, Scalar("SELECT gia_thue FROM phong_tro WHERE id=$id", ("$id", roomId)));
    }
}
