using System.Net;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class RoomServicesTests
{
    [Theory][InlineData("2026-09-30",1)][InlineData("2026-10-01",2)][InlineData("2026-10-04",2)][InlineData("2026-10-05",2)][InlineData("2026-10-31",2)]
    public async Task DepartureBillingKeepsDepartureMonthAndReducesFollowingMonth(string departure,int octoberCount)
    {
        using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);await AddStay(db,f.A,new(2026,9,1),DateOnly.Parse(departure));
        var october=await Issue(db,await OccupantInput(db,f,people,new(2026,10,20)));
        var november=await Issue(db,await OccupantInput(db,f,people,new(2026,11,20)));
        var invoice=await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==october);
        Assert.Equal(octoberCount,invoice.SoNguoiTinhPhi);Assert.Equal(1100000+octoberCount*80000,invoice.TongTien);
        var next=await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==november);
        Assert.Equal(1,next.SoNguoiTinhPhi);Assert.Equal(1180000,next.TongTien);
        Assert.Equal(100000,next.ChiTiet.Single(x=>x.DichVuId==f.Service).ThanhTien);Assert.Equal(80000,next.ChiTiet.Single(x=>x.DichVuId==people).ThanhTien);
    }
    [Fact] public async Task DepartureHttpWarningSnapshotsAndFutureUnissuedBill()
    {
        using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);var profile=await AddStay(db,f.A,new(2026,9,1));var stay=await db.NguoiOGheps.SingleAsync(x=>x.KhachThueId==profile);
        var october=await Issue(db,await OccupantInput(db,f,people,new(2026,10,1)));
        var november=await Issue(db,await OccupantInput(db,f,people,new(2026,11,1)));
        using var factory=BillingWeb();using var client=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(client,"owner");
        var url=$"/HopDong/ChuyenDi/{f.A}?nguoiId={stay.Id}";
        var page=WebUtility.HtmlDecode(await client.GetStringAsync($"/HopDong/Details/{f.A}?tab=people"));Assert.Contains("Ghi nhận chuyển đi",page);
        var fields=new Dictionary<string,string>{["ChuyenDi.NgayRa"]="2026-10-04",["ChuyenDi.PhienBanPhong"]=(await db.PhongTros.FindAsync(f.A))!.PhienBan.ToString()};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync(url,new FormUrlEncodedContent(fields))).StatusCode);
        using var other=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(other,"other");Assert.Equal(HttpStatusCode.Forbidden,(await Post(other,url,new(fields))).StatusCode);
        var response=await Post(client,url,fields);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        page=WebUtility.HtmlDecode(await client.GetStringAsync($"/HopDong/Details/{f.A}?tab=people"));Assert.Contains("11/2026",page);Assert.Contains("đã lập được giữ nguyên",page);Assert.Contains("Ngày chuyển đi:",page);Assert.Contains("04/10/2026",page);Assert.Contains("Người đã rời phòng",page);
        using var fresh=Context();Assert.Equal(2,(await fresh.HoaDons.FindAsync(october))!.SoNguoiTinhPhi);Assert.Equal(2,(await fresh.HoaDons.FindAsync(november))!.SoNguoiTinhPhi);Assert.Equal(1260000,(await fresh.HoaDons.FindAsync(november))!.TongTien);
        var december=await Issue(fresh,await OccupantInput(fresh,f,people,new(2026,12,1)));Assert.Equal(1,(await fresh.HoaDons.FindAsync(december))!.SoNguoiTinhPhi);Assert.Equal(1180000,(await fresh.HoaDons.FindAsync(december))!.TongTien);
        var snapshot=WebUtility.HtmlDecode(await client.GetStringAsync($"/HoaDonDichVu/Details/{november}"));Assert.Contains("2 người",snapshot);Assert.Contains("80.000",snapshot);
    }
}
