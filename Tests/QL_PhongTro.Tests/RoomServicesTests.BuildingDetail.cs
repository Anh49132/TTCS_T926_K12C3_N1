using System.Net;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class RoomServicesTests
{
    [Fact] public async Task BuildingDetailShowsRealRoomsFiltersPagesAndContract()
    {
        using var db=Context();var f=await BillingAsync(db);for(var i=0;i<8;i++)await AddRoom(db,"Extra-"+i);
        await db.Database.ExecuteSqlRawAsync("UPDATE phong_tro SET trang_thai='DANG_THUE' WHERE ma_phong='DEMO-A'");
        using var factory=BillingWeb();using var client=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(client,"owner");
        var html=WebUtility.HtmlDecode(await client.GetStringAsync("/PhongTro/ChiTietToaNha/1"));
        Assert.Contains("Building A",html);Assert.Contains("building-detail.css",html);Assert.Contains("10 phòng",html);Assert.Contains("Synthetic tenant",html);Assert.Contains("Ngày hết hạn",html);Assert.Contains("Nhập danh sách",html);Assert.Contains("Chưa có dữ liệu thu tiền",html);
        var filtered=WebUtility.HtmlDecode(await client.GetStringAsync("/PhongTro/ChiTietToaNha/1?trangThai=TRONG&tuKhoa=Extra-7"));Assert.Contains("Extra-7",filtered);Assert.DoesNotContain("HD-DEMO-A",filtered);
        var empty=WebUtility.HtmlDecode(await client.GetStringAsync("/PhongTro/ChiTietToaNha/1?tuKhoa=not-found"));Assert.Contains("Không có phòng phù hợp",empty);
        var last=WebUtility.HtmlDecode(await client.GetStringAsync("/PhongTro/ChiTietToaNha/1?trang=999"));Assert.Contains("Hiển thị 2 trong 10",last);
        Assert.Contains("/PhongTro/ChiTietToaNha/1",await client.GetStringAsync("/PhongTro/ToaNha"));
    }
    [Fact] public async Task BuildingDetailForeignOwnerDeniedAndAssignedManagerReadOnly()
    {
        using var db=Context();await AddRoom(db,"Visible room");
        using var factory=BillingWeb();using var other=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(other,"other");
        Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync("/PhongTro/ChiTietToaNha/1")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync("/PhongTro/ChiTietToaNha/99")).StatusCode);
        await db.Database.ExecuteSqlRawAsync("UPDATE tai_khoan SET vai_tro='QUAN_LY' WHERE id=2; UPDATE toa_nha SET quan_ly_id=2 WHERE id=1");
        using var manager=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(manager,"other");var html=WebUtility.HtmlDecode(await manager.GetStringAsync("/PhongTro/ChiTietToaNha/1"));
        Assert.Contains("Visible room",html);Assert.DoesNotContain("Đã thu tháng này",html);Assert.DoesNotContain("Chỉnh sửa tòa nhà",html);Assert.DoesNotContain("Cấu hình điện nước",html);
    }
}
