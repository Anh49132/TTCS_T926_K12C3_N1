using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Theory]
    [InlineData(true, "3.500", true)]
    [InlineData(false, "3.500", true)]
    [InlineData(true, "0", false)]
    [InlineData(true, "-100", false)]
    public async Task CreateRoomSavesSelectedPhotoAmenitiesAndInternalNote(bool applyService, string price, bool valid)
    {
        var listing = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (_, building) = GetListingRoomAndBuilding(listing);
        Execute("INSERT OR IGNORE INTO dich_vu(ma_dich_vu,ten_dich_vu,dang_hoat_dong) VALUES('DIEN','Điện',1)");
        var electricity = Convert.ToInt32(Scalar("SELECT id FROM dich_vu WHERE ma_dich_vu='DIEN'"));
        Execute("INSERT INTO dich_vu_toa_nha(toa_nha_id,dich_vu_id,ap_dung_mac_dinh) VALUES($building,$service,1)", ("$building",building), ("$service",electricity));
        var catalog = Convert.ToInt32(Scalar("SELECT id FROM dich_vu_toa_nha WHERE toa_nha_id=$building AND dich_vu_id=$service", ("$building",building), ("$service",electricity)));
        using var owner = await Login("CHU_NHA");
        var page = await owner.GetStringAsync($"/PhongTro/Create?toaNhaId={building}");
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string,string> {
            ["__RequestVerificationToken"] = token, ["ToaNhaId"] = building.ToString(),
            ["MaPhong"] = "NEW-PHOTO", ["Tang"] = "1", ["DienTich"] = "25",
            ["GiaThueDisplay"] = "3.500.000", ["SoNguoiToiDa"] = "2", ["TrangThai"] = "TRONG",
            ["DichVuDaChon"] = "true", ["DichVuMoi[0].CatalogId"] = catalog.ToString(),
            ["DichVuMoi[0].ApDung"] = applyService.ToString(), ["DichVuMoi[0].Gia"] = price, ["DichVuMoi[0].CachTinh"] = "THEO_CHI_SO",
            ["GhiChuNoiBo"] = "Ghi chú nội bộ kiểm thử", ["TienNghi"] = "DIEU_HOA" })
            form.Add(new StringContent(field.Value), field.Key);
        form.Add(new ByteArrayContent(EncodeJpeg(640,480)), "AnhPhongMoi", "room.jpg");
        using var response = await owner.PostAsync("/PhongTro/Create", form);
        if (!valid)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM phong_tro WHERE ma_phong='NEW-PHOTO'"));
            return;
        }
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        var room = Convert.ToInt32(Scalar("SELECT id FROM phong_tro WHERE ma_phong='NEW-PHOTO'"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM anh_phong WHERE phong_id=$room AND thu_tu=1", ("$room",room)));
        Assert.Equal(applyService ? 1L : 0L, Scalar("SELECT COUNT(*) FROM dich_vu_phong WHERE phong_id=$room", ("$room",room)));
        Assert.Equal(applyService ? 1L : 0L, Scalar("SELECT COUNT(*) FROM cau_hinh_dich_vu WHERE phong_id=$room AND don_gia=3500 AND cach_tinh='THEO_CHI_SO' AND don_vi_tinh='kWh'", ("$room",room)));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM phong_tro WHERE id=$room AND ghi_chu_noi_bo='Ghi chú nội bộ kiểm thử' AND tien_nghi LIKE '%DIEU_HOA%'", ("$room",room)));
        CleanupRoomImages(room);
    }
}


