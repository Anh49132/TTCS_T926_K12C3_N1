using System.Net;
using System.Globalization;
using System.Text.Json;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task PublicListingContactUsesBuildingOwnerRatherThanPostingManager()
    {
        var listing=CreatePublicListing("TRONG","DANG_HIEN_THI",includePhoto:false);
        Execute("UPDATE tai_khoan SET ho_ten=$name,so_dien_thoai=$phone WHERE id=$id",("$name","Chủ nhà Liên Hệ"),("$phone","0987654321"),("$id",accounts["CHU_NHA"]));
        Execute("UPDATE tin_dang SET nguoi_dang_id=$manager WHERE id=$id",("$manager",accounts["QUAN_LY"]),("$id",listing));
        using var guest=Client();
        foreach(var url in new[]{"/TimTin",$"/TinDang/ChiTiet/{listing}"}){
            var html=WebUtility.HtmlDecode(await guest.GetStringAsync(url));
            Assert.Contains("Chủ nhà Liên Hệ",html);Assert.Contains("href=\"tel:0987654321\"",html);
        }
        using var data=JsonDocument.Parse(await guest.GetStringAsync($"/api/tin-dang/{listing}"));
        Assert.Equal("0987654321",data.RootElement.GetProperty("dienThoaiChuNha").GetString());
    }

    [Fact]
    public async Task SearchLinksToDetailsUsesThumbnailAndExcludesOccupiedRooms()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: true);
        using var guest = Client();
        var html = await guest.GetStringAsync("/TimTin");
        Assert.Contains($"href=\"/TinDang/ChiTiet/{listingId}\"", html);
        Assert.Contains("/images/room-a-small.jpg", html);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        Execute("UPDATE phong_tro SET trang_thai='DA_DAT_COC' WHERE id=$id", ("$id", roomId));
        html = await guest.GetStringAsync("/TimTin");
        Assert.DoesNotContain($"href=\"/TinDang/ChiTiet/{listingId}\"", html);
    }

    [Fact]
    public async Task UnassignedParkingIsExcludedFromPublicListingAndTotal()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, buildingId) = GetListingRoomAndBuilding(listingId);
        var parking = CreateService(buildingId, "PARKING_UNASSIGNED", "Gửi xe tầng trệt");
        AddServicePrice(buildingId, parking, accounts["CHU_NHA"], CachTinhDichVu.CoDinh, "phòng/tháng", 100000);
        using var guest = Client();
        using var response = await guest.GetAsync($"/api/tin-dang/{listingId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.GetProperty("khoanCoDinh").EnumerateArray());
        Assert.Equal(2500000m, json.RootElement.GetProperty("tongChiPhiThangDau").GetDecimal());
        AddRoomSelection(buildingId, roomId, parking, 75000);
        using var assignedResponse = await guest.GetAsync($"/api/tin-dang/{listingId}");
        using var assigned = JsonDocument.Parse(await assignedResponse.Content.ReadAsStringAsync());
        Assert.Single(assigned.RootElement.GetProperty("khoanCoDinh").EnumerateArray());
        Assert.Equal(2575000m, assigned.RootElement.GetProperty("tongChiPhiThangDau").GetDecimal());
    }

    [Fact]
    public async Task AnonymousGuestCanReadListingDetailsAndPhotos()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: true);
        using var guest = Client();

        var page = await guest.GetAsync($"/TinDang/ChiTiet/{listingId}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True(page.Headers.CacheControl?.NoStore);
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Contains("Phòng gần trung tâm, có cửa sổ", html);
        Assert.Contains("2.500.000", html);
        Assert.Contains("25,5 m²", html);
        Assert.Contains("2 người", html);
        Assert.Contains("1.000.000 đ", html);
        Assert.Contains("data-testid=\"first-month-total\">2.500.000 đ", html);
        Assert.Contains("chưa gồm điện, nước theo sử dụng thực tế", html);
        Assert.Contains("Phòng sáng, thoáng và có chỗ để xe.", html);
        Assert.Contains("data-gallery-main src=\"/images/room-a.jpg\"", html);
        Assert.Contains("Chưa có đơn giá dịch vụ theo mức sử dụng.", html);
        Assert.Contains("Tin này chưa có khoản phí cố định hàng tháng.", html);

        var response = await guest.GetAsync($"/api/tin-dang/{listingId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement;
        Assert.Equal(listingId, data.GetProperty("id").GetInt32());
        Assert.Equal(2500000, data.GetProperty("giaThue").GetInt64());
        Assert.Equal(25.5m, data.GetProperty("dienTich").GetDecimal());
        Assert.Equal(2, data.GetProperty("soNguoiToiDa").GetInt32());
        Assert.Equal(1000000, data.GetProperty("tienCocDuKien").GetInt64());
        Assert.Equal("Phòng sáng, thoáng và có chỗ để xe.", data.GetProperty("moTa").GetString());
        Assert.Single(data.GetProperty("anh").EnumerateArray());
        Assert.Empty(data.GetProperty("dichVuTheoSuDung").EnumerateArray());
        Assert.Empty(data.GetProperty("khoanCoDinh").EnumerateArray());
        Assert.Equal(2500000m, data.GetProperty("tongChiPhiThangDau").GetDecimal());
    }

    [Fact]
    public async Task AnonymousGuestCanSeeUtilityRatesAndMultipleMonthlyFixedFees()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, buildingId) = GetListingRoomAndBuilding(listingId);
        var ownerId = accounts["CHU_NHA"];

        var electricity = CreateService(buildingId, "DIEN", "Điện");
        var water = CreateService(buildingId, "NUOC", "Nước");
        var garbage = CreateService(buildingId, "RAC", "Rác");
        var parking = CreateService(buildingId, "GUI_XE", "Gửi xe");
        var internet = CreateService(buildingId, "INTERNET", "Internet");
        AddServicePrice(buildingId, electricity, ownerId, CachTinhDichVu.TheoChiSo, "kWh", 3500);
        AddServicePrice(buildingId, water, ownerId, CachTinhDichVu.TheoChiSo, "m³", 25000);
        AddServicePrice(buildingId, garbage, ownerId, CachTinhDichVu.TheoNguoi, "người/tháng", 20000);
        AddServicePrice(buildingId, parking, ownerId, CachTinhDichVu.CoDinh, "phòng/tháng", 100000);
        AddServicePrice(buildingId, internet, ownerId, CachTinhDichVu.CoDinh, "phòng/tháng", 150000);
        foreach (var service in new[] { electricity, water, garbage, parking, internet })
            AddRoomSelection(buildingId, roomId, service, null);

        using var guest = Client();
        var html = WebUtility.HtmlDecode(await guest.GetStringAsync($"/TinDang/ChiTiet/{listingId}"));
        Assert.Contains("Điện", html);
        Assert.Contains("Theo chỉ số", html);
        Assert.Contains("kWh", html);
        Assert.Contains("3.500", html);
        Assert.Contains("Nước", html);
        Assert.Contains("m³", html);
        Assert.Contains("25.000", html);
        Assert.Contains("Rác", html);
        Assert.Contains("Gửi xe", html);
        Assert.Contains("100.000", html);
        Assert.Contains("Internet", html);
        Assert.Contains("150.000", html);
        Assert.Contains("data-testid=\"first-month-total\">2.750.000 đ", html);
        Assert.Contains("chưa gồm điện, nước theo sử dụng thực tế", html);

        using var response = await guest.GetAsync($"/api/tin-dang/{listingId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement;
        var usage = data.GetProperty("dichVuTheoSuDung").EnumerateArray().ToArray();
        var fixedFees = data.GetProperty("khoanCoDinh").EnumerateArray().ToArray();
        Assert.Equal(3, usage.Length);
        Assert.Equal(2, fixedFees.Length);
        Assert.Contains(usage, item => item.GetProperty("tenDichVu").GetString() == "Điện"
            && item.GetProperty("donViTinh").GetString() == "kWh"
            && item.GetProperty("donGia").GetInt64() == 3500);
        Assert.Contains(fixedFees, item => item.GetProperty("tenDichVu").GetString() == "Gửi xe"
            && item.GetProperty("donGia").GetInt64() == 100000);
        Assert.Equal(2750000m, data.GetProperty("tongChiPhiThangDau").GetDecimal());

        Execute("UPDATE phong_tro SET gia_thue=2600000 WHERE id=$room", ("$room", roomId));
        Execute("UPDATE cau_hinh_dich_vu SET don_gia=200000 WHERE toa_nha_id=$building AND dich_vu_id=$service",
            ("$building", buildingId), ("$service", parking));
        using var updatedResponse = await guest.GetAsync($"/api/tin-dang/{listingId}");
        using var updatedJson = JsonDocument.Parse(await updatedResponse.Content.ReadAsStringAsync());
        Assert.Equal(2950000m, updatedJson.RootElement.GetProperty("tongChiPhiThangDau").GetDecimal());
    }

    [Fact]
    public async Task GuestPricingUsesRoomOverrideAndOmitsInvalidOrInactivePrices()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, buildingId) = GetListingRoomAndBuilding(listingId);
        var ownerId = accounts["CHU_NHA"];

        var electricity = CreateService(buildingId, "DIEN", "Điện");
        AddServicePrice(buildingId, electricity, ownerId, CachTinhDichVu.TheoChiSo, "kWh", 3500);
        AddRoomSelection(buildingId, roomId, electricity, 4200);

        var water = CreateService(buildingId, "NUOC", "Nước");
        AddServicePrice(buildingId, water, ownerId, CachTinhDichVu.TheoChiSo, "m³", 0);

        var malformed = CreateService(buildingId, "KHAC", "Phí lỗi");
        AddServicePrice(buildingId, malformed, ownerId, CachTinhDichVu.TheoChiSo, "", 3000, ignoreChecks: true);

        using var guest = Client();
        var html = WebUtility.HtmlDecode(await guest.GetStringAsync($"/TinDang/ChiTiet/{listingId}"));
        Assert.Contains("Điện", html);
        Assert.Contains("4.200", html);
        Assert.Contains("Tin này chưa có khoản phí cố định hàng tháng.", html);
        Assert.DoesNotContain("3.500", html);
        Assert.DoesNotContain("Phí lỗi", html);

        using var response = await guest.GetAsync($"/api/tin-dang/{listingId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var usage = json.RootElement.GetProperty("dichVuTheoSuDung").EnumerateArray().ToArray();
        Assert.Single(usage);
        Assert.Equal(4200, usage[0].GetProperty("donGia").GetInt64());
        Assert.Empty(json.RootElement.GetProperty("khoanCoDinh").EnumerateArray());
        Assert.Equal(2500000m, json.RootElement.GetProperty("tongChiPhiThangDau").GetDecimal());
    }

    [Fact]
    public async Task FirstMonthEstimateDoesNotOverflowForLargeFixedFee()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, buildingId) = GetListingRoomAndBuilding(listingId);
        var largeFee = CreateService(buildingId, "LARGE", "Phí lớn");
        AddServicePrice(buildingId, largeFee, accounts["CHU_NHA"], CachTinhDichVu.CoDinh, "phòng/tháng", long.MaxValue);
        AddRoomSelection(buildingId, roomId, largeFee, null);
        using var guest = Client();

        using var response = await guest.GetAsync($"/api/tin-dang/{listingId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var expected = (decimal)2500000 + long.MaxValue;
        Assert.Equal(expected, json.RootElement.GetProperty("tongChiPhiThangDau").GetDecimal());
    }

    [Fact]
    public async Task MissingDescriptionsShowFallbackAndKeepEstimatedTotal()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        Execute("UPDATE tin_dang SET noi_dung=NULL WHERE id=$id; UPDATE phong_tro SET mo_ta=NULL WHERE id=$room",
            ("$id", listingId), ("$room", roomId));
        using var guest = Client();

        var page = await guest.GetAsync($"/TinDang/ChiTiet/{listingId}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Contains("Chưa có mô tả cho tin này.", html);
        Assert.Contains("data-testid=\"first-month-total\">2.500.000 đ", html);

        using var response = await guest.GetAsync($"/api/tin-dang/{listingId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("moTa").ValueKind);
        Assert.Equal(2500000m, json.RootElement.GetProperty("tongChiPhiThangDau").GetDecimal());
    }

    [Fact]
    public async Task PublicListingStillRendersIfOwnerAccountIsDisabled()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (_, buildingId) = GetListingRoomAndBuilding(listingId);
        var ownerId = accounts["CHU_NHA"];
        var fixedService = CreateService(buildingId, "GUI_XE", "Gửi xe");
        AddServicePrice(buildingId, fixedService, ownerId, CachTinhDichVu.CoDinh, "phòng/tháng", 100000);
        Execute("UPDATE tai_khoan SET dang_hoat_dong=0 WHERE id=$id", ("$id", ownerId));
        using var guest = Client();

        var page = await guest.GetAsync($"/TinDang/ChiTiet/{listingId}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Contains("Phòng gần trung tâm, có cửa sổ", html);
        Assert.Contains("Chi phí cố định ước tính mỗi tháng", html);
        Assert.Contains("Tin này chưa có khoản phí cố định hàng tháng.", html);

        using var response = await guest.GetAsync($"/api/tin-dang/{listingId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.GetProperty("khoanCoDinh").EnumerateArray());
        Assert.Equal(2500000m, json.RootElement.GetProperty("tongChiPhiThangDau").GetDecimal());
    }

    [Fact]
    public async Task MissingOrUnavailableListingReturnsNotFoundAndNoPhotoHasAnEmptyState()
    {
        var noPhotoId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var hiddenId = CreatePublicListing("TRONG", "TAM_AN", includePhoto: false);
        var rentedId = CreatePublicListing("DANG_THUE", "DANG_HIEN_THI", includePhoto: false);
        var expiredId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false, expired: true);
        using var guest = Client();

        foreach (var id in new[] { 999999, hiddenId, rentedId, expiredId })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/TinDang/ChiTiet/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/tin-dang/{id}")).StatusCode);
        }

        var page = await guest.GetStringAsync($"/TinDang/ChiTiet/{noPhotoId}");
        Assert.Contains("Tin này chưa có hình ảnh.", page);
        var response = await guest.GetAsync($"/api/tin-dang/{noPhotoId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.GetProperty("anh").EnumerateArray());
    }

    [Fact]
    public async Task ListingIndexUsesFirstThumbnailAndSharedPlaceholderWhileDetailUsesOriginal()
    {
        RentalRequestSchema.Initialize(database);
        var withPhoto = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: true);
        var withoutPhoto = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        using var guest = Client();

        var indexResponse = await guest.GetAsync("/TinDang");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        var index = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Contains("src=\"/images/room-a-small.jpg\"", index);
        Assert.DoesNotContain("src=\"/images/room-a.jpg\"", index);
        Assert.Contains("room-placeholder.svg", index);

        var detail = WebUtility.HtmlDecode(await guest.GetStringAsync($"/TinDang/ChiTiet/{withPhoto}"));
        Assert.Contains("data-gallery-main src=\"/images/room-a.jpg\"", detail);

        var emptyDetail = await guest.GetStringAsync($"/TinDang/ChiTiet/{withoutPhoto}");
        Assert.Contains("room-placeholder.svg", emptyDetail);
        Assert.Contains("Tin này chưa có hình ảnh.", emptyDetail);
    }

    private int CreatePublicListing(string roomStatus, string listingStatus, bool includePhoto, bool expired = false)
    {
        var key = Guid.NewGuid().ToString("N");
        Execute("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,dang_hoat_dong) VALUES($owner,$name,'12 Đường Hoa',1)",
            ("$owner", accounts["CHU_NHA"]), ("$name", "Nhà test " + key));
        var buildingId = Convert.ToInt32(Scalar("SELECT id FROM toa_nha WHERE ten_toa_nha=$name", ("$name", "Nhà test " + key)));
        Execute("""
            INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,mo_ta,ngay_tao,phien_ban)
            VALUES($building,'P01',1,25.5,2500000,1000000,2,$status,'Phòng sáng, thoáng và có chỗ để xe.',$created,0)
            """, ("$building", buildingId), ("$status", roomStatus), ("$created", DateTime.UtcNow.ToString("O")));
        var roomId = Convert.ToInt32(Scalar("SELECT id FROM phong_tro WHERE toa_nha_id=$building AND ma_phong='P01'", ("$building", buildingId)));
        var expiry = DateTime.UtcNow.AddDays(expired ? -1 : 30).ToString("O");
        Execute("""
            INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,ngay_dang,ngay_het_han,trang_thai,ngay_tao)
            VALUES($room,$owner,'Phòng gần trung tâm, có cửa sổ','Phòng sáng, thoáng và có chỗ để xe.',$published,$expiry,$status,$created)
            """, ("$room", roomId), ("$owner", accounts["CHU_NHA"]), ("$published", DateTime.UtcNow.ToString("O")),
            ("$expiry", expiry), ("$status", listingStatus), ("$created", DateTime.UtcNow.ToString("O")));
        var listingId = Convert.ToInt32(Scalar("SELECT id FROM tin_dang WHERE phong_id=$room", ("$room", roomId)));

        if (includePhoto)
            Execute("INSERT INTO anh_phong(phong_id,duong_dan,duong_dan_anh_nho,thu_tu,mo_ta,ngay_tao) VALUES($room,'/images/room-a.jpg','/images/room-a-small.jpg',1,'Phòng có cửa sổ',$created)",
                ("$room", roomId), ("$created", DateTime.UtcNow.ToString("O")));

        return listingId;
    }

    private (int RoomId, int BuildingId) GetListingRoomAndBuilding(int listingId)
    {
        var roomId = Convert.ToInt32(Scalar("SELECT phong_id FROM tin_dang WHERE id=$id", ("$id", listingId)));
        var buildingId = Convert.ToInt32(Scalar("SELECT toa_nha_id FROM phong_tro WHERE id=$id", ("$id", roomId)));
        return (roomId, buildingId);
    }

    private int CreateService(int buildingId, string code, string name, bool active = true)
    {
        Execute("INSERT INTO dich_vu(ma_dich_vu,ten_dich_vu,dang_hoat_dong) VALUES($code,$name,$active)",
            ("$code", code), ("$name", name), ("$active", active));
        var serviceId = Convert.ToInt32(Scalar("SELECT id FROM dich_vu WHERE ma_dich_vu=$code", ("$code", code)));
        Execute("INSERT INTO dich_vu_toa_nha(toa_nha_id,dich_vu_id,ap_dung_mac_dinh) VALUES($building,$service,1)",
            ("$building", buildingId), ("$service", serviceId));
        return serviceId;
    }

    private void AddServicePrice(int buildingId, int serviceId, int ownerId, string method, string unit,
        long price, bool ignoreChecks = false)
    {
        var startDate = DichVuService.HomNay().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var sql = """
            INSERT INTO cau_hinh_dich_vu(toa_nha_id,phong_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,dang_ap_dung,da_chot_gia,nguoi_tao_id,ngay_tao)
            VALUES($building,NULL,$service,$method,$unit,$price,$start,1,1,$owner,$created)
            """;
        if (ignoreChecks)
            sql = "PRAGMA ignore_check_constraints=ON; " + sql;
        Execute(sql, ("$building", buildingId), ("$service", serviceId),
            ("$method", method), ("$unit", unit), ("$price", price), ("$start", startDate),
            ("$owner", ownerId), ("$created", DateTime.UtcNow.ToString("O")));
    }

    private void AddRoomSelection(int buildingId, int roomId, int serviceId, long? roomPrice)
    {
        var catalogId = Convert.ToInt32(Scalar("SELECT id FROM dich_vu_toa_nha WHERE toa_nha_id=$building AND dich_vu_id=$service",
            ("$building", buildingId), ("$service", serviceId)));
        Execute("INSERT INTO dich_vu_phong(phong_id,dich_vu_toa_nha_id,don_gia_rieng) VALUES($room,$catalog,$price)",
            ("$room", roomId), ("$catalog", catalogId), ("$price", (object?)roomPrice ?? DBNull.Value));
    }
}
