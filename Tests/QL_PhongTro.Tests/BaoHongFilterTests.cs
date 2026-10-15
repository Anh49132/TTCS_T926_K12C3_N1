using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class BaoHongTests
{
    private static void FilterBuildings(Fixture f)
    {
        Managers(f);
        f.Execute("""
            INSERT INTO toa_nha(id,chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES
              (3,1,5,'Tòa B','Test B'),(4,1,5,'Tòa trống','Test empty');
            UPDATE phong_tro SET toa_nha_id=3 WHERE id=2;
            """);
    }

    private static async Task<Dictionary<string,int>> FilterReports(Fixture f)
    {
        FilterBuildings(f);
        using var db=f.Context(); var sender=new BaoHongService(db,f.Time);var ids=new Dictionary<string,int>();
        foreach (var (key, contract, tenant, priority, state) in new[] {
            ("a-new-urgent",1,2,"GAP","MOI"), ("a-new-normal",1,2,"THUONG","MOI"),
            ("a-progress",1,2,"GAP","DANG_XU_LY"), ("a-done",1,2,"THUONG","HOAN_THANH"),
            ("a-rejected",1,2,"GAP","TU_CHOI"), ("b-new-urgent",2,2,"GAP","MOI"),
            ("foreign",3,3,"GAP","MOI") })
        {
            var id=(await sender.GuiAsync(tenant,Input(contract,priority))).Id;ids[key]=id;
            if(state!="MOI") {
                await Management(db,f).CapNhatAsync(5,id,Update(state=="HOAN_THANH"?"DANG_XU_LY":state));
                if(state=="HOAN_THANH") await Management(db,f).CapNhatAsync(5,id,Update(state,1));
            }
        }
        f.Time.UtcNow=f.Time.UtcNow.AddHours(26);return ids;
    }

    [Fact]
    public async Task FiltersEachCriterionAndTheirIntersectionAndKeepsOverdueWarning()
    {
        using var f=new Fixture();var ids=await FilterReports(f);using var db=f.Context();var service=Management(db,f);
        Assert.Equal(new[]{1,3,4},(await service.ToaNhaLocAsync(5,new())).Select(x=>x.Id).Order());
        var all=await service.DanhSachLocAsync(5,new());Assert.Equal(6,all.Count);
        Assert.DoesNotContain(all,x=>x.BaoHong.Id==ids["foreign"]);
        Assert.Equal(5,(await service.DanhSachLocAsync(5,new(){ToaNhaId=1})).Count);
        Assert.Equal(3,(await service.DanhSachLocAsync(5,new(){TrangThai="MOI"})).Count);
        Assert.Equal(4,(await service.DanhSachLocAsync(5,new(){MucDo="GAP"})).Count);
        foreach(var status in new[]{"DANG_XU_LY","HOAN_THANH","TU_CHOI"})
            Assert.Equal(status,Assert.Single(await service.DanhSachLocAsync(5,new(){TrangThai=status})).BaoHong.TrangThai);
        var result=Assert.Single(await service.DanhSachLocAsync(5,new(){ToaNhaId=1,TrangThai="MOI",MucDo="GAP"}));
        Assert.Equal(ids["a-new-urgent"],result.BaoHong.Id);Assert.NotNull(result.CanhBaoQuaHan);
        Assert.Equal("26 giờ 0 phút",result.CanhBaoQuaHan.ThoiGianChoHienThi);
        Assert.Empty(await service.DanhSachLocAsync(5,new(){ToaNhaId=3,TrangThai="TU_CHOI",MucDo="THUONG"}));
        Assert.Empty(await service.DanhSachLocAsync(5,new(){ToaNhaId=4}));
        Assert.Equal(6,(await service.DanhSachLocAsync(5,new())).Count);
    }

    [Theory]
    [InlineData(2)] [InlineData(9999)]
    public async Task FilterRejectsForeignOrUnknownBuildingAndRechecksAssignment(int building)
    {
        using var f=new Fixture();await FilterReports(f);using var db=f.Context();var service=Management(db,f);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.DanhSachLocAsync(5,new(){ToaNhaId=building}));
        f.Execute("UPDATE toa_nha SET quan_ly_id=6 WHERE id=1");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.DanhSachLocAsync(5,new(){ToaNhaId=1}));
        Assert.DoesNotContain(await service.ToaNhaLocAsync(5,new()),x=>x.Id==1);
        Assert.All(await service.DanhSachLocAsync(5,new()),x=>Assert.Equal(3,x.ToaNhaId));
        f.Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.DanhSachLocAsync(5,new()));
    }

    [Theory]
    [InlineData(-1,null,null)] [InlineData(0,null,null)] [InlineData(null,"BAD",null)]
    [InlineData(null,null,"BAD")] [InlineData(null,"MOI' OR 1=1--",null)]
    public void InvalidFiltersAreRejected(int? building,string? state,string? priority) =>
        Assert.Throws<ArgumentException>(()=>new BaoHongBoLoc{ToaNhaId=building,TrangThai=state,MucDo=priority}.ChuanHoa());

    [Fact]
    public async Task FilterAppliesBeforePaginationAndReadOnlyQueriesDoNotWrite()
    {
        using var f=new Fixture();FilterBuildings(f);
        using(var db=f.Context()) {
            var sender=new BaoHongService(db,f.Time);
            for(var i=0;i<22;i++) await sender.GuiAsync(2,Input(level:"GAP"));
            for(var i=0;i<23;i++) await sender.GuiAsync(2,Input(2));
        }
        var audits=f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong");
        f.Time.UtcNow=f.Time.UtcNow.AddHours(26);
        using var check=f.Context();var service=Management(check,f);
        var filter=new BaoHongBoLoc{ToaNhaId=1,TrangThai="MOI",MucDo="GAP"};
        var page1=await service.DanhSachLocAsync(5,filter);Assert.Equal(21,page1.Count);
        filter.Page=2;var page2=await service.DanhSachLocAsync(5,filter);Assert.Equal(2,page2.Count);
        Assert.Empty(page1.Take(20).Select(x=>x.BaoHong.Id).Intersect(page2.Select(x=>x.BaoHong.Id)));
        Assert.All(page2,x=>Assert.NotNull(x.CanhBaoQuaHan));
        filter.Page=3;Assert.Empty(await service.DanhSachLocAsync(5,filter));
        Assert.Equal(audits,f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong"));
        Assert.Equal(0,f.Scalar("SELECT count(*) FROM bao_hong_trao_doi"));
    }
}
