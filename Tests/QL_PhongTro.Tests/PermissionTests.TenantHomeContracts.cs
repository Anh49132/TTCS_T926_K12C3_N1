using System.Net;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class PermissionTests
{
    void TenantHomeFixture(string state="DANG_HIEU_LUC",bool roommate=false,string start="2020-01-01",string end="2090-01-01",string? departure=null)
    {
        QL_PhongTro.Data.RentalRequestSchema.Initialize(database);
        Execute("""
            INSERT INTO khach_thue(id,tai_khoan_id,ho_ten,ngay_tao) VALUES(80,$tenant,'Home tenant','2020-01-01');
            INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(81,'Other home tenant','2020-01-01');
            INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(80,$owner,'HOME-BUILDING','Demo');
            INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao)
            VALUES(80,80,'HOME-MY-ROOM',1,25,2000000,3,'DANG_THUE','2020-01-01'),(81,80,'HOME-FOREIGN-ROOM',1,25,2000000,3,'DANG_THUE','2020-01-01');
            INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai)
            VALUES(80,'HD-HOME-MINE',80,$signer,$state),(81,'HD-HOME-FOREIGN',81,81,'DANG_HIEU_LUC');
            INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue)
            VALUES(80,$start,$end,2200000),(81,'2020-01-01','2090-01-01',2000000);
            """,("$tenant",accounts["KHACH_THUE"]),("$owner",accounts["CHU_NHA"]),("$signer",roommate?81:80),("$state",state),("$start",start),("$end",end));
        if(roommate)Execute("INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao,ngay_ra) VALUES(80,80,$start,$departure)",("$start",start),("$departure",(object?)departure??DBNull.Value));
    }
    [Theory][InlineData(false,"Người đứng tên")][InlineData(true,"Người ở ghép")]
    public async Task TenantHomeContractLoadsSignerAndRoommateWithoutPublicListings(bool roommate,string role)
    {
        TenantHomeFixture(roommate:roommate);using var tenant=await Login("KHACH_THUE");var response=await tenant.GetAsync("/");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.True(response.Headers.CacheControl?.NoStore);
        var html=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());Assert.Contains("HOME-MY-ROOM",html);Assert.Contains("HOME-BUILDING",html);Assert.Contains("HD-HOME-MINE",html);Assert.Contains(role,html);Assert.Contains("2.200.000",html);
        Assert.Contains("/HopDongCuaToi/Details?maHopDong=HD-HOME-MINE",html);Assert.DoesNotContain("Chưa có thông tin phòng đang thuê",html);Assert.DoesNotContain("HOME-FOREIGN-ROOM",html);Assert.DoesNotContain("HD-HOME-FOREIGN",html);
    }
    [Theory][InlineData("CHO_HIEU_LUC","2091-01-01","2092-01-01","Chờ bắt đầu thuê")][InlineData("DANG_HIEU_LUC","2091-01-01","2092-01-01","Chờ bắt đầu thuê")][InlineData("DA_KET_THUC","2000-01-01","2001-01-01","Đã kết thúc thuê")]
    public async Task TenantHomeContractDoesNotCallFutureOrEndedTenancyActive(string state,string start,string end,string label)
    {
        TenantHomeFixture(state:state,start:start,end:end);using var tenant=await Login("KHACH_THUE");var html=WebUtility.HtmlDecode(await tenant.GetStringAsync("/"));Assert.Contains("HD-HOME-MINE",html);Assert.Contains(label,html);Assert.DoesNotContain("Phòng của tôi · Đang thuê",html);
    }
    [Fact] public async Task TenantHomeContractKeepsDepartedRoommateHistoryButNotCurrentRoom()
    {
        TenantHomeFixture(roommate:true,departure:"2021-01-01");using var tenant=await Login("KHACH_THUE");var html=WebUtility.HtmlDecode(await tenant.GetStringAsync("/"));Assert.Contains("HD-HOME-MINE",html);Assert.Contains("Đã kết thúc thuê",html);Assert.DoesNotContain("Phòng của tôi · Đang thuê",html);
    }
    [Theory][InlineData("NHAP")][InlineData("DA_HUY")]
    public async Task TenantHomeContractDoesNotExposeDraftOrCancelledContract(string state)
    {
        TenantHomeFixture(state:state);using var tenant=await Login("KHACH_THUE");var html=WebUtility.HtmlDecode(await tenant.GetStringAsync("/"));Assert.DoesNotContain("HD-HOME-MINE",html);Assert.Contains("Chưa có thông tin phòng đang thuê",html);
    }
    [Fact] public async Task TenantHomeContractRevokedPermissionHidesContracts()
    {
        TenantHomeFixture();Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='KHACH_THUE' AND ModuleCode='HOP_DONG'");using var tenant=await Login("KHACH_THUE");var html=WebUtility.HtmlDecode(await tenant.GetStringAsync("/"));Assert.DoesNotContain("HD-HOME-MINE",html);Assert.Contains("chưa được cấp quyền xem hợp đồng",html);
    }
}
