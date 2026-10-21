using System.Net;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class PermissionTests
{
    [Fact] public async Task MyAccountShowsActualIdEvenBeforeProfileAndNeverExposesFullIdentity()
    {
        using var tenant=await Login("KHACH_THUE");
        var initial=WebUtility.HtmlDecode(await tenant.GetStringAsync("/HoSo/Xem"));
        Assert.Contains("Hồ sơ của tôi",initial);Assert.Contains($"data-copy-account=\"{accounts["KHACH_THUE"]}\"",initial);Assert.Contains("Chưa cập nhật",initial);
        Execute("INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(99,'Unrelated profile','2026-01-01')");
        var form=await ProfileForm(tenant,new DateOnly(2000,1,1));Assert.Equal(HttpStatusCode.Redirect,(await tenant.PostAsync("/HoSo",new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(Scalar("SELECT so_dien_thoai FROM tai_khoan WHERE id=$id",("$id",accounts["KHACH_THUE"])),Scalar("SELECT so_dien_thoai FROM khach_thue WHERE tai_khoan_id=$id",("$id",accounts["KHACH_THUE"])));
        var response=await tenant.GetAsync("/HoSo/Xem");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.True(response.Headers.CacheControl?.NoStore);
        var page=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());Assert.Contains("********8901",page);Assert.DoesNotContain("012345678901",page);
        Assert.Contains($"data-copy-account=\"{accounts["KHACH_THUE"]}\"",page);Assert.DoesNotContain("data-copy-account=\"100\"",page);Assert.DoesNotContain("Unrelated profile",page);
        Assert.Contains("Chỉnh sửa hồ sơ",page);Assert.Contains("/Account/ChangePassword",page);Assert.Contains("/HopDongCuaToi",page);Assert.Contains("__RequestVerificationToken",page);
    }
    [Fact] public async Task MyAccountRentalSummaryIncludesOnlyOwnCurrentMembership()
    {
        QL_PhongTro.Data.RentalRequestSchema.Initialize(database);
        using var tenant=await Login("KHACH_THUE");var form=await ProfileForm(tenant,new DateOnly(2000,1,1));Assert.Equal(HttpStatusCode.Redirect,(await tenant.PostAsync("/HoSo",new FormUrlEncodedContent(form))).StatusCode);
        var own=Scalar("SELECT id FROM khach_thue WHERE tai_khoan_id=$id",("$id",accounts["KHACH_THUE"]));
        Execute("""
            INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(99,'Another renter','2026-01-01');
            INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(99,$owner,'Account summary','Demo');
            INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(99,99,'ACC-1',1,25,2000000,3,'DANG_THUE','2026-01-01');
            INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES(99,'HD-account-summary',99,99,'DANG_HIEU_LUC');
            INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES(99,'2020-01-01','2090-01-01',2000000);
            INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao) VALUES(99,$profile,'2020-01-01');
            """,("$owner",accounts["CHU_NHA"]),("$profile",own!));
        var page=WebUtility.HtmlDecode(await tenant.GetStringAsync("/HoSo/Xem"));Assert.Contains("Người ở ghép",page);Assert.DoesNotContain("Another renter",page);
        Execute("UPDATE nguoi_o_ghep SET ngay_ra='2021-01-01' WHERE hop_dong_id=99");
        page=WebUtility.HtmlDecode(await tenant.GetStringAsync("/HoSo/Xem"));Assert.Contains("Chưa tham gia hợp đồng",page);
    }
}
