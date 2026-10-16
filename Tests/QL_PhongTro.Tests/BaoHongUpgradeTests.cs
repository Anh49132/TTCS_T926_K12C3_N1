using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class BaoHongTests
{
    [Fact]
    public async Task UpgradeTenantFiltersWholeHistoryAndClampsPageWithoutLeakingOtherTenant()
    {
        using var f=new Fixture(); using var db=f.Context();var service=new BaoHongService(db,f.Time);
        for(var i=0;i<23;i++) await service.GuiAsync(2,Input(level:i==0?"GAP":"THUONG"));
        await service.GuiAsync(3,Input(3,"GAP"));
        var filter=new BaoHongBoLoc{MucDo="GAP",Page=9,TimKiem="BH-202610"};
        var result=await service.DanhSachLocAsync(2,filter);
        Assert.Equal(23,result.Summary.Tong);Assert.Equal(1,result.Summary.KetQua);
        Assert.Equal(1,filter.Page);Assert.Equal(2,Assert.Single(result.Rows).BaoHong.NguoiBaoId);
        Assert.Equal(3,(await service.DanhSachLocAsync(2,new(){Page=2})).Rows.Count);
        Assert.Empty((await service.DanhSachLocAsync(2,new(){TimKiem="missing"})).Rows);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.DanhSachLocAsync(2,new(){CanhBao="QUA_HAN"}));
    }

    [Fact]
    public async Task UpgradeManagerSearchWarningSortAndMetadataStayInAssignedScope()
    {
        using var f=new Fixture();var ids=await FilterReports(f);using var db=f.Context();var service=Management(db,f);
        var overdue=await service.DanhSachLocAsync(5,new(){CanhBao="QUA_HAN",SapXep="CU_NHAT"});
        Assert.Equal(2,overdue.Count);Assert.All(overdue,x=>Assert.NotNull(x.CanhBaoQuaHan));
        var found=Assert.Single(await service.DanhSachLocAsync(5,new(){TimKiem=$"BH-202610-{ids["a-progress"]:0000}"}));
        Assert.Equal(ids["a-progress"],found.BaoHong.Id);Assert.True(found.CapNhatGanNhat>=found.BaoHong.NgayTao);
        Assert.NotNull(found.NguoiXuLyGanNhat);
        Assert.Empty(await service.DanhSachLocAsync(5,new(){TimKiem=$"BH-202610-{ids["foreign"]:0000}"}));
        Assert.Equal(1,(await service.TongQuanAsync(5,new())).HoanThanhThang);
        var route=new BaoHongBoLoc{TimKiem="tap",CanhBao="QUA_HAN",SapXep="UU_TIEN"}.RouteValues(2);
        Assert.Equal("tap",route["timKiem"]);Assert.Equal("QUA_HAN",route["canhBao"]);Assert.Equal("UU_TIEN",route["sapXep"]);
    }
}
