using System.Net;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class PermissionTests
{
    void OwnerProfileFixture()
    {
        QL_PhongTro.Data.RentalRequestSchema.Initialize(database);
        Execute("""
            INSERT INTO khach_thue(id,ho_ten,so_dien_thoai,so_giay_to,ngay_tao) VALUES
                (10,'Scoped main','0901234510','012345678910','2026-01-01'),
                (11,'Scoped roommate','0901234511','012345678911','2026-01-01'),
                (12,'Scoped former',NULL,NULL,'2026-01-01'),
                (13,'Draft outsider','0901234513','012345678913','2026-01-01'),
                (14,'Foreign outsider','0901234514','012345678914','2026-01-01'),
                (15,'Cancelled outsider','0901234515','012345678915','2026-01-01');
            INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(50,$owner,'Scoped building','Demo'),(51,$other,'Foreign building','Other');
            INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES
                (50,50,'SC-1',1,25,2000000,3,'DANG_THUE','2026-01-01'),(51,50,'SC-old',1,25,2000000,3,'TRONG','2026-01-01'),
                (52,50,'SC-draft',1,25,2000000,3,'TRONG','2026-01-01'),(53,51,'FOREIGN',1,25,2000000,3,'DANG_THUE','2026-01-01'),
                (54,50,'SC-cancel',1,25,2000000,3,'TRONG','2026-01-01');
            INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES
                (50,'HD-scope-active',50,10,'DANG_HIEU_LUC'),(51,'HD-scope-old',51,12,'DA_KET_THUC'),
                (52,'HD-scope-draft',52,13,'NHAP'),(53,'HD-scope-foreign',53,14,'DANG_HIEU_LUC'),(54,'HD-scope-cancel',54,15,'DA_HUY');
            INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES
                (50,'2020-01-01','2090-01-01',2000000),(51,'2020-01-01','2021-01-01',2000000),
                (52,'2020-01-01','2090-01-01',2000000),(53,'2020-01-01','2090-01-01',2000000),(54,'2020-01-01','2090-01-01',2000000);
            INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao) VALUES(50,11,'2020-01-01');
            """,("$owner",accounts["CHU_NHA"]),("$other",accounts["ADMIN"]));
    }
    [Fact] public async Task OwnerListMasksIdentityAndExcludesDraftCancelledAndForeignTenants()
    {
        OwnerProfileFixture();using var owner=await Login("CHU_NHA");var response=await owner.GetAsync("/HoSo/DanhSach");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.True(response.Headers.CacheControl?.NoStore);
        var html=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Scoped main",html);Assert.Contains("Scoped roommate",html);Assert.Contains("Scoped former",html);
        Assert.DoesNotContain("Draft outsider",html);Assert.DoesNotContain("Foreign outsider",html);Assert.DoesNotContain("Cancelled outsider",html);
        Assert.Contains("******4510",html);Assert.Contains("********8910",html);Assert.DoesNotContain("0901234510",html);Assert.DoesNotContain("012345678910",html);
        var token=WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var filteredResponse=await owner.PostAsync("/HoSo/LocDanhSach",new FormUrlEncodedContent(new Dictionary<string,string>{{"timKiem","roommate"},{"toaNhaId","50"},{"trangThai","DANG_THUE"},{"__RequestVerificationToken",token}}));
        Assert.Equal(HttpStatusCode.Redirect,filteredResponse.StatusCode);
        var filtered=WebUtility.HtmlDecode(await owner.GetStringAsync(filteredResponse.Headers.Location!));Assert.Contains("Scoped roommate",filtered);Assert.DoesNotContain("Scoped main",filtered);Assert.DoesNotContain("Scoped former",filtered);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync("/HoSo/LocDanhSach",new FormUrlEncodedContent(new Dictionary<string,string>{{"timKiem","012345678910"}}))).StatusCode);
        var identitySearch=await owner.PostAsync("/HoSo/LocDanhSach",new FormUrlEncodedContent(new Dictionary<string,string>{{"timKiem","012345678910"},{"__RequestVerificationToken",token}}));
        Assert.Equal(HttpStatusCode.Redirect,identitySearch.StatusCode);Assert.DoesNotContain("012345678910",identitySearch.Headers.Location!.OriginalString);
        var identityPage=WebUtility.HtmlDecode(await owner.GetStringAsync(identitySearch.Headers.Location));Assert.Contains("Scoped main",identityPage);Assert.DoesNotContain("Scoped roommate",identityPage);Assert.DoesNotContain("timKiem=012345678910",identityPage);
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/HoSo/DanhSach?toaNhaId=51")).StatusCode);
    }
    [Fact] public async Task OwnerDetailIncludesOnlyScopedContractsAndIsReadOnly()
    {
        OwnerProfileFixture();using var owner=await Login("CHU_NHA");var html=WebUtility.HtmlDecode(await owner.GetStringAsync("/HoSo/Xem?id=11"));
        Assert.Contains("Người ở ghép",html);Assert.Contains("HD-scope-active",html);Assert.Contains("Scoped building",html);Assert.Contains("012345678911",html);Assert.Contains("Chỉ xem",html);Assert.DoesNotContain("Chỉnh sửa hồ sơ",html);
        Assert.DoesNotContain("HD-scope-foreign",html);Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/HoSo/Xem?id=14")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/HoSo/Xem?id=13")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/HoSo/Xem?id=15")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await owner.GetAsync("/HoSo/Anh?id=14&mat=truoc")).StatusCode);
    }
    [Fact] public async Task RevokedContractPermissionBlocksOwnerListDetailAndImages()
    {
        OwnerProfileFixture();Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA' AND ModuleCode='HOP_DONG'");
        using var owner=await Login("CHU_NHA");Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/HoSo/DanhSach")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/HoSo/Xem?id=10")).StatusCode);
        using var manager=await Login("QUAN_LY");Assert.Equal(HttpStatusCode.Forbidden,(await manager.GetAsync("/HoSo/DanhSach")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await manager.GetAsync("/HoSo/Xem?id=10")).StatusCode);
    }
    [Theory][InlineData("CHU_NHA","Chủ nhà")][InlineData("QUAN_LY","Quản lý tòa nhà")]
    public async Task StaffPersonalMenuUsesOwnAccountNotTenantProfiles(string role,string title)
    {
        OwnerProfileFixture();using var staff=await Login(role);var response=await staff.GetAsync("/HoSo/CuaToi?id=14");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var html=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());Assert.Contains("Hồ sơ của tôi",html);Assert.Contains(title,html);Assert.Contains(role.ToLowerInvariant()+"@s104.test",html);
        Assert.DoesNotContain("Scoped main",html);Assert.DoesNotContain("Foreign outsider",html);Assert.Contains("/HoSo/CuaToi",html);Assert.DoesNotContain(">Xem hồ sơ</a>",html);Assert.Contains("__RequestVerificationToken",html);
    }
    [Fact] public async Task OwnerProfilePaginationRetainsFiltersAndDoesNotDuplicatePeople()
    {
        OwnerProfileFixture();Execute("INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao,ngay_ra) VALUES(50,11,'2019-01-01','2019-12-31')");
        for(var id=20;id<31;id++) Execute("INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES($id,$name,'2026-01-01'); INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao) VALUES(50,$id,'2020-01-01')",("$id",id),("$name","Paged resident "+id));
        using var owner=await Login("CHU_NHA");var initial=await owner.GetStringAsync("/HoSo/DanhSach");var token=WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(initial,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect,(await owner.PostAsync("/HoSo/LocDanhSach",new FormUrlEncodedContent(new Dictionary<string,string>{{"timKiem","Paged"},{"toaNhaId","50"},{"__RequestVerificationToken",token}}))).StatusCode);
        var page=WebUtility.HtmlDecode(await owner.GetStringAsync("/HoSo/DanhSach?toaNhaId=50&trang=2"));
        Assert.Contains("Paged resident 30",page);Assert.DoesNotContain("Paged resident 20",page);Assert.DoesNotContain("timKiem=Paged",page);Assert.Contains("toaNhaId=50",page);Assert.Contains("của 11 khách thuê",page);
    }
}
