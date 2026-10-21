using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    private static async Task<Dictionary<string, string>> ProfileForm(HttpClient client, DateOnly birthDate)
    {
        var html = await client.GetStringAsync("/HoSo");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return new()
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token),
            ["HoTen"] = "Hồ sơ kiểm thử",
            ["NgaySinh"] = birthDate.ToString("yyyy-MM-dd"),
            ["SoCanCuoc"] = "012345678901",
            ["QueQuan"] = "Hà Nội",
            ["NgheNghiep"] = "Sinh viên"
        };
    }

    [Fact]
    public async Task ProfileRejectsFutureDatesAndShowsCreateUpdateDeleteNotifications()
    {
        using var client = await Login("KHACH_THUE");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var form = await ProfileForm(client, today.AddDays(1));
        var rejected = await client.PostAsync("/HoSo", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Ngày sinh không được sau ngày hiện tại.", WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync()));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM khach_thue WHERE tai_khoan_id=$id", ("$id", accounts["KHACH_THUE"])));

        form["NgaySinh"] = today.ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/HoSo", new FormUrlEncodedContent(form))).StatusCode);
        var createdPage = WebUtility.HtmlDecode(await client.GetStringAsync("/HoSo"));
        Assert.Contains("Đã tạo hồ sơ cá nhân thành công.", createdPage);
        Assert.Contains("delete-profile-form", createdPage);
        Assert.DoesNotContain("Đã tạo hồ sơ cá nhân thành công.", WebUtility.HtmlDecode(await client.GetStringAsync("/HoSo")));

        form = await ProfileForm(client, today.AddYears(1));
        form["HoTen"] = "Không được lưu";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/HoSo", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal("Hồ sơ kiểm thử", Scalar("SELECT ho_ten FROM khach_thue WHERE tai_khoan_id=$id", ("$id", accounts["KHACH_THUE"])));

        form["NgaySinh"] = "2000-02-29";
        form["HoTen"] = "Hồ sơ đã sửa";
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/HoSo", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Contains("Đã cập nhật hồ sơ cá nhân thành công.", WebUtility.HtmlDecode(await client.GetStringAsync("/HoSo")));
        Assert.Equal("2000-02-29", Scalar("SELECT ngay_sinh FROM khach_thue WHERE tai_khoan_id=$id", ("$id", accounts["KHACH_THUE"])));

        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/HoSo/Xoa", new FormUrlEncodedContent(form))).StatusCode);
        var deletedPage = WebUtility.HtmlDecode(await client.GetStringAsync("/HoSo"));
        Assert.Contains("Đã xóa hồ sơ cá nhân thành công.", deletedPage);
        Assert.DoesNotContain("delete-profile-form", deletedPage);
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM khach_thue WHERE tai_khoan_id=$id", ("$id", accounts["KHACH_THUE"])));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM tai_khoan WHERE id=$id", ("$id", accounts["KHACH_THUE"])));
    }

    [Fact]
    public async Task ProfileDeleteUsesSignedInTenantAndRejectsOtherRoles()
    {
        using var client = await Login("KHACH_THUE");
        var form = await ProfileForm(client, new DateOnly(2000, 1, 1));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/HoSo", new FormUrlEncodedContent(form))).StatusCode);
        Execute("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES ($id,'Other profile','2026-01-01');", ("$id", accounts["CHU_NHA"]));
        var otherProfileId = Scalar("SELECT id FROM khach_thue WHERE tai_khoan_id=$id", ("$id", accounts["CHU_NHA"]))!;
        form["id"] = otherProfileId.ToString()!;
        form["TaiKhoanId"] = accounts["CHU_NHA"].ToString();
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/HoSo/Xoa", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM khach_thue WHERE id=$id", ("$id", otherProfileId)));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM khach_thue WHERE tai_khoan_id=$id", ("$id", accounts["KHACH_THUE"])));

        using var owner = await Login("CHU_NHA");
        var html = await owner.GetStringAsync("/Account/ChangePassword");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var denied = await owner.PostAsync("/HoSo/Xoa", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        Assert.True(denied.StatusCode == HttpStatusCode.Forbidden ||
            (denied.StatusCode == HttpStatusCode.Redirect && denied.Headers.Location!.ToString().Contains("AccessDenied")));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM khach_thue WHERE id=$id", ("$id", otherProfileId)));
    }

    [Fact]
    public async Task ProfileDeleteRequiresAntiforgeryAndPreservesReferencedProfile()
    {
        using var client = await Login("KHACH_THUE");
        var form = await ProfileForm(client, new DateOnly(2000, 1, 1));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/HoSo", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/HoSo/Xoa", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);

        Execute("CREATE TABLE profile_reference_test (profile_id INTEGER REFERENCES khach_thue(id)); INSERT INTO profile_reference_test SELECT id FROM khach_thue WHERE tai_khoan_id=$id;", ("$id", accounts["KHACH_THUE"]));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/HoSo/Xoa", new FormUrlEncodedContent(form))).StatusCode);
        var page = WebUtility.HtmlDecode(await client.GetStringAsync("/HoSo"));
        Assert.Contains("Không thể xóa hồ sơ đang được hợp đồng hoặc dữ liệu khác sử dụng.", page);
        Assert.DoesNotContain("Đã xóa hồ sơ cá nhân thành công.", page);
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM khach_thue WHERE tai_khoan_id=$id", ("$id", accounts["KHACH_THUE"])));
    }

    [Fact]
    public async Task ProfileReadIsScopedToSelfOrSignedContractAndOwnerCannotEdit()
    {
        QL_PhongTro.Data.RentalRequestSchema.Initialize(database);
        using var tenant = await Login("KHACH_THUE");
        var form = await ProfileForm(tenant, new DateOnly(2000, 1, 1));
        Assert.Equal(HttpStatusCode.Redirect, (await tenant.PostAsync("/HoSo", new FormUrlEncodedContent(form))).StatusCode);
        var own = Scalar("SELECT id FROM khach_thue WHERE tai_khoan_id=$id", ("$id", accounts["KHACH_THUE"]))!;
        Execute("INSERT INTO khach_thue(ho_ten,ngay_tao) VALUES ('Unrelated tenant','2026-01-01')");
        var other = Scalar("SELECT id FROM khach_thue WHERE ho_ten='Unrelated tenant'")!;
        Assert.Equal(HttpStatusCode.OK, (await tenant.GetAsync($"/HoSo/Xem?id={own}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync($"/HoSo/Xem?id={other}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync("/HoSo/DanhSach")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tenant.GetAsync($"/HoSo/Anh?id={other}&mat=truoc")).StatusCode);

        using var owner = await Login("CHU_NHA");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/HoSo/Xem?id={own}")).StatusCode);
        Execute("""
            INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,dang_hoat_dong) VALUES ($owner,'Profile building','Demo',1);
            INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao)
            VALUES (last_insert_rowid(),'PROFILE-1',1,25,2000000,3,'TRONG','2026-01-01');
            INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES (1,'HD-PROFILE',last_insert_rowid(),$tenant,'NHAP',$owner,'2026-01-01');
            """, ("$owner", accounts["CHU_NHA"]), ("$tenant", own));
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/HoSo/Xem?id={own}")).StatusCode);
        Execute("UPDATE hop_dong SET trang_thai='DA_KET_THUC'");
        var page = WebUtility.HtmlDecode(await owner.GetStringAsync("/HoSo/DanhSach"));
        Assert.Contains("Hồ sơ kiểm thử", page);
        Assert.DoesNotContain("Unrelated tenant", page);
        var detail = WebUtility.HtmlDecode(await owner.GetStringAsync($"/HoSo/Xem?id={own}"));
        Assert.Contains("012345678901", detail);
        Assert.DoesNotContain("Chỉnh sửa hồ sơ của tôi", detail);
        var tokenPage = await owner.GetStringAsync("/Account/ChangePassword");
        form["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(tokenPage, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        form["HoTen"] = "Owner overwrite";
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync("/HoSo", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal("Hồ sơ kiểm thử", Scalar("SELECT ho_ten FROM khach_thue WHERE id=$id", ("$id", own)));
        Execute("UPDATE toa_nha SET chu_nha_id=$other", ("$other", accounts["ADMIN"]));
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/HoSo/Xem?id={own}")).StatusCode);
        Assert.DoesNotContain("Hồ sơ kiểm thử", WebUtility.HtmlDecode(await owner.GetStringAsync("/HoSo/DanhSach")));
    }
}
