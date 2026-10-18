using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace QL_PhongTro.Tests;
public sealed partial class PermissionTests
{
    [Fact]
    public async Task AdminReadOnlyBusinessPagesAndLegacyFullCannotWrite()
    {
        QL_PhongTro.Data.DichVuSchemaInitializer.InitializeInvoices(database);
        using var admin=await Login("ADMIN");
        Execute("UPDATE role_permission SET AccessLevel='FULL' WHERE RoleCode='ADMIN' AND ModuleCode<>'TAI_KHOAN'");
        foreach(var path in new[]{"/PhongTro","/PhongTro/ToaNha","/DichVu","/TinDang/QuanLy","/YeuCau","/HopDong","/ChiSoDienNuoc","/HoaDonDichVu/Management","/CongNo","/ChuNhaBaoHong","/BaoCao","/NhatKy","/EmailGuiLog?TrangThai=THAT_BAI","/Account/ChangePassword"})
            { var response=await admin.GetAsync(path); Assert.True(response.StatusCode==HttpStatusCode.OK,path+": "+response.StatusCode); }
        foreach(var path in new[]{"/PhongTro/Create","/PhongTro/TaoToaNha","/TinDang/Tao","/HopDong/Create","/DichVu/Create"})
            Assert.Equal(HttpStatusCode.Forbidden,(await admin.GetAsync(path)).StatusCode);
        foreach(var path in new[]{"/PhongTro/Delete/1","/TinDang/DoiTrangThai","/ChiSoDienNuoc/Save","/HopDong/Create","/DichVu/Initialize"})
            Assert.Equal(HttpStatusCode.Forbidden,(await admin.PostAsync(path,new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        var home=await admin.GetStringAsync("/");
        Assert.Contains("/ManagedAccounts/Create",home);Assert.Contains("/EmailGuiLog",home);
        Assert.DoesNotContain("href=\"/PhongTro/Create",home);
        Assert.DoesNotContain("href=\"/TinDang/Tao",await admin.GetStringAsync("/TinDang/QuanLy"));
        Assert.DoesNotContain("action=\"/ChiSoDienNuoc/Save",await admin.GetStringAsync("/ChiSoDienNuoc"));
        Assert.DoesNotContain("href=\"/HoaDonDichVu/Setup",await admin.GetStringAsync("/HoaDonDichVu/Management"));
    }

    [Fact]
    public async Task AdminRevokedOrMissingPermissionHidesMenuAndDeniesRoute()
    {
        using var admin=await Login("ADMIN");
        Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='ADMIN' AND ModuleCode='PHONG_TRO'");
        Execute("DELETE FROM role_permission WHERE RoleCode='ADMIN' AND ModuleCode='TIN_DANG'");
        var home=await admin.GetStringAsync("/");
        Assert.DoesNotContain("data-menu-module=\"PHONG_TRO\"",home);
        Assert.DoesNotContain("data-menu-module=\"TIN_DANG\"",home);
        Assert.Equal(HttpStatusCode.Forbidden,(await admin.GetAsync("/PhongTro")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await admin.GetAsync("/TinDang/QuanLy")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync("/NhatKy")).StatusCode);
        Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='ADMIN' AND ModuleCode='TAI_KHOAN'");
        Assert.DoesNotContain("href=\"/ManagedAccounts/Create",await admin.GetStringAsync("/"));
        Assert.DoesNotContain("action=\"/ManagedAccounts/SetActive",await admin.GetStringAsync("/ManagedAccounts"));
        Assert.Equal(HttpStatusCode.Forbidden,(await admin.GetAsync("/ManagedAccounts/Create")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.GetAsync("/EmailGuiLog?TuNgay=2026-10-20&DenNgay=2026-10-01")).StatusCode);
    }

    [Fact]
    public async Task AdminCanLockTenantAndRevokeItsSessionButCannotLockSelf()
    {
        using var tenant=await Login("KHACH_THUE");using var admin=await Login("ADMIN");
        var html=await admin.GetStringAsync("/ManagedAccounts");
        var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        async Task<HttpResponseMessage> Lock(int id)=>await admin.PostAsync("/ManagedAccounts/SetActive",new FormUrlEncodedContent(new Dictionary<string,string>{
            ["id"]=id.ToString(),["active"]="false",["confirmed"]="true",["__RequestVerificationToken"]=token}));
        Assert.Equal(HttpStatusCode.Redirect,(await Lock(accounts["KHACH_THUE"])).StatusCode);
        Assert.Equal(0L,Scalar("SELECT dang_hoat_dong FROM tai_khoan WHERE id=$id",("$id",accounts["KHACH_THUE"])));
        Assert.Equal(HttpStatusCode.Redirect,(await tenant.GetAsync("/Permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Lock(accounts["ADMIN"])).StatusCode);
    }
}
