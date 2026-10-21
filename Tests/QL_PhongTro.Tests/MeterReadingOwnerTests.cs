using System.Net;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class MeterReadingListTests
{
    [Fact]
    public async Task OwnerCanOpenAndSaveTheirBuildingWithTheirOwnActorId()
    {
        MeterFixture();
        using var factory=Factory();
        using var owner=await Login(factory,"owner@meter.test");
        var module=await owner.GetAsync("/Modules/DIEN_NUOC");
        Assert.Equal(HttpStatusCode.Redirect,module.StatusCode);
        Assert.Equal("/ChiSoDienNuoc",module.Headers.Location?.OriginalString);
        var html=await owner.GetStringAsync("/ChiSoDienNuoc?toaNhaId=1");
        Assert.Contains("href=\"/ChiSoDienNuoc\"",html);
        using var manager=await Login(factory,"manager@meter.test");
        var managerFields=await MeterForm(manager);managerFields["DienMoi"]="2";managerFields["NuocMoi"]="14";
        var fields=await MeterForm(owner);fields["DienMoi"]="1";fields["NuocMoi"]="13";
        var noToken=new Dictionary<string,string>(fields);noToken.Remove("__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(noToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await owner.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields))).StatusCode);
        using var db=new QL_PhongTro.Data.AppDbContext(new DbContextOptionsBuilder<QL_PhongTro.Data.AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
        var saved=await db.ChiSoDienNuocs.ToListAsync();Assert.Equal(2,saved.Count);Assert.All(saved,x=>Assert.Equal(1,x.NguoiNhapId));
        var stale=await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(managerFields));
        Assert.Equal(HttpStatusCode.OK,stale.StatusCode);
        Assert.Contains("Chỉ số đã thay đổi",WebUtility.HtmlDecode(await stale.Content.ReadAsStringAsync()));
        Assert.Equal(2,Count("chi_so_dien_nuoc"));
    }
    [Fact]
    public async Task OwnerCannotReadOrSaveForeignBuildingEvenWhenAssignedAsManager()
    {
        MeterFixture();Execute("UPDATE toa_nha SET chu_nha_id=5,quan_ly_id=1 WHERE id=2");
        using var factory=Factory();using var owner=await Login(factory,"owner@meter.test");
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/ChiSoDienNuoc?toaNhaId=2")).StatusCode);
        var html=await owner.GetStringAsync("/ChiSoDienNuoc");Assert.DoesNotContain("SECRET",html);
        var fields=await MeterForm(owner);fields["ToaNhaId"]="2";fields["PhongId"]="5";fields["HopDongId"]="5";
        fields["DienMoi"]="1";fields["NuocMoi"]="13";
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(0,Count("chi_so_dien_nuoc"));
    }
    [Fact]
    public async Task OwnerWritePermissionAndInvoiceLockStillProtectReadings()
    {
        MeterFixture();using var factory=Factory();using var owner=await Login(factory,"owner@meter.test");
        var fields=await MeterForm(owner);fields["DienMoi"]="1";fields["NuocMoi"]="13";
        Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='CHU_NHA' AND ModuleCode='DIEN_NUOC'");
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields))).StatusCode);
        Execute("UPDATE role_permission SET AccessLevel='WRITE' WHERE RoleCode='CHU_NHA' AND ModuleCode='DIEN_NUOC'");
        Invoice(1,1,10,issued:true);
        var response=await owner.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("đã khóa",WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(0,Count("chi_so_dien_nuoc"));
        Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA' AND ModuleCode='DIEN_NUOC'");
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/ChiSoDienNuoc")).StatusCode);
    }
}
