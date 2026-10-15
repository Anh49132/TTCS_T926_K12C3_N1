using System.Net;
using System.Text.RegularExpressions;
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
            ["TenToaNha"] = "Tòa kiểm thử", ["DiaChi"] = "12 Hà Nội", ["SoTang"] = "3",
            ["PhuongXa"] = "Phường kiểm thử", ["QuanHuyen"] = "Quận kiểm thử", ["TinhThanh"] = "Hà Nội"
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
        Assert.Contains("value=\"\">Tất cả cơ sở", emptyPage);
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
        Assert.Contains("Chỉnh sửa phòng", WebUtility.HtmlDecode(await client.GetStringAsync($"/PhongTro/Edit/{roomId}")));
        form["MaPhong"] = "A102";
        var edited = await client.PostAsync($"/PhongTro/Edit/{roomId}", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        Assert.Contains("Đã cập nhật phòng thành công.", WebUtility.HtmlDecode(await client.GetStringAsync(edited.Headers.Location)));
        Assert.Equal("A102", Scalar("SELECT ma_phong FROM phong_tro WHERE id=$id", ("$id", roomId)));

        var buildingForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = form["__RequestVerificationToken"],
            ["TenToaNha"] = "Tòa đã sửa", ["DiaChi"] = "24 Hà Nội", ["SoTang"] = "4",
            ["PhuongXa"] = "Phường kiểm thử", ["QuanHuyen"] = "Quận kiểm thử", ["TinhThanh"] = "Hà Nội"
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
}
