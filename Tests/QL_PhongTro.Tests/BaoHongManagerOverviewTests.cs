using System.Net;
using System.Text.RegularExpressions;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class BaoHongTests
{
    [Fact]
    public async Task ManagerOverviewCountsWholeAssignedScopeAndFilteredTabsWithoutLeakingOtherBuildings()
    {
        using var f = new Fixture(); var ids = await FilterReports(f);
        using var db = f.Context(); var service = Management(db, f);
        var audits = f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong");
        var all = await service.TongQuanAsync(5, new());
        Assert.Equal(6, all.Tong); Assert.Equal(3, all.Moi); Assert.Equal(1, all.HoanThanh);
        Assert.Equal(2, all.QuaHan); Assert.Equal(3, all.SoToaNha); Assert.Equal(6, all.KetQua);
        Assert.DoesNotContain(ids["foreign"], new[]{all.CanhBao!.BaoHong.Id});
        var selected = await service.TongQuanAsync(5, new(){ ToaNhaId=1, MucDo="GAP", TrangThai="MOI" });
        Assert.Equal(6, selected.Tong); Assert.Equal(1, selected.KetQua);
        Assert.Equal(3, selected.TheoTrangThai.Values.Sum());
        Assert.Equal(1, selected.TheoTrangThai["DANG_XU_LY"]);
        Assert.Equal(ids["a-new-urgent"], selected.CanhBao!.BaoHong.Id);
        var done = await service.TongQuanAsync(5, new(){ TrangThai="HOAN_THANH" });
        Assert.Null(done.CanhBao); Assert.Equal(1, done.KetQua);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.TongQuanAsync(5,new(){ToaNhaId=2}));
        Assert.Equal(audits, f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong"));
        f.Execute("UPDATE toa_nha SET quan_ly_id=6 WHERE id=1");
        Assert.Equal(1,(await service.TongQuanAsync(5,new())).Tong);
        f.Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.TongQuanAsync(5,new()));
    }

    [Fact]
    public async Task ManagerTabsKeepBuildingAndPriorityResetPageAndRenderAccurateTotalBeyondOnePage()
    {
        using var f = new Fixture(); FilterBuildings(f);
        using(var db = f.Context()) for(var i=0;i<22;i++)
            await new QL_PhongTro.Services.BaoHongService(db,f.Time).GuiAsync(2,Input(level:"GAP"));
        using(var db = f.Context()) {
            var all = await Management(db,f).TongQuanAsync(5,new(){Page=2});
            Assert.Equal(22,all.Tong); Assert.Equal(22,all.Moi); Assert.Equal(22,all.KetQua);
        }
        var password=Guid.NewGuid().ToString("N")+"Aa1!";
        f.Execute($"UPDATE tai_khoan SET mat_khau='{BCrypt.Net.BCrypt.HashPassword(password,4)}'");
        using var web=FilterWeb(f); using var client=await FilterLogin(web,password);
        var page=WebUtility.HtmlDecode(await client.GetStringAsync("/QuanLyBaoHong?toaNhaId=1&mucDo=GAP&page=2"));
        Assert.Contains("22 báo hỏng",page); Assert.Contains("Hiển thị 21–22 trong tổng số 22",page);
        Assert.Equal(2,Regex.Matches(page,"<tr data-repair-id=").Count);
        var tabs=Regex.Match(page,"<nav class=\"mr-tabs\"[\\s\\S]*?</nav>").Value;
        var nextState=FilterHref(tabs,"Hoàn thành");
        Assert.Contains("toaNhaId=1",nextState);Assert.Contains("mucDo=GAP",nextState);
        Assert.Contains("trangThai=HOAN_THANH",nextState);Assert.DoesNotContain("page=2",nextState);
        var empty=await client.GetStringAsync(nextState);
        Assert.Contains("Không có báo hỏng phù hợp",WebUtility.HtmlDecode(empty));
        Assert.DoesNotContain("data-repair-id=",empty);
        Assert.DoesNotContain("mr-urgent\"",empty);
        Assert.Contains("manager-repairs.css",page);
    }
}
