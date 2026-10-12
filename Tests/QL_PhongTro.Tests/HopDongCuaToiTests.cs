using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using UglyToad.PdfPig;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class HopDongCuaToiTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor httpContextAccessor = new();
    private readonly AppDbContext db;
    private readonly int ownerAccountId;
    private readonly int coTenantAccountId;
    private readonly int outsiderAccountId;
    private const string ContractCode = "HD-2026-0001";
    private static readonly DateOnly Start = new(2026, 1, 15);
    private static readonly DateOnly End = new(2027, 1, 14);

    public HopDongCuaToiTests()
    {
        connection.Open();
        db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options,
            httpContextAccessor);
        db.Database.EnsureCreated();

        var owner = NewAccount("Nguyễn An", "owner@example.test");
        var coTenant = NewAccount("Trần Bình", "guest@example.test");
        var outsider = NewAccount("Lê Chi", "outsider@example.test");
        InsertAccount(owner);
        InsertAccount(coTenant);
        InsertAccount(outsider);
        ownerAccountId = db.TaiKhoans.Single(x => x.Email == owner.Email).Id;
        coTenantAccountId = db.TaiKhoans.Single(x => x.Email == coTenant.Email).Id;
        outsiderAccountId = db.TaiKhoans.Single(x => x.Email == outsider.Email).Id;
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, ownerAccountId.ToString())], "test"))
        };

        var profiles = new[]
        {
            NewProfile(ownerAccountId, "Nguyễn An"),
            NewProfile(coTenantAccountId, "Trần Bình"),
            NewProfile(outsiderAccountId, "Lê Chi")
        };
        db.KhachThues.AddRange(profiles);
        db.SaveChanges();
        db.Database.ExecuteSqlRaw("""
            INSERT INTO toa_nha(chu_nha_id, ten_toa_nha, dia_chi, thang_may, bai_do_xe,
                camera_an_ninh, bao_ve_24h, khu_giat_say, san_thuong, ngay_chot_hang_thang, dang_hoat_dong)
            VALUES ({0}, 'Nhà Alpha', '12 Đường Mẫu, Quận 1', 0, 0, 0, 0, 0, 0, 1, 1)
            """, ownerAccountId);

        var building = db.ToaNhas.Single();
        db.Database.ExecuteSqlRaw("""
            INSERT INTO phong_tro(toa_nha_id, ma_phong, tang, dien_tich, gia_thue, tien_coc_du_kien,
                so_nguoi_toi_da, trang_thai, ngay_tao, phien_ban)
            VALUES ({0}, 'A108', 1, 25, 3500000, 3500000, 3, 'DANG_THUE', {1}, 0)
            """, building.Id, DateTime.UtcNow);
        var roomId = db.PhongTros.Single().Id;

        var contract = new HopDongThamChieu
        {
            MaHopDong = ContractCode,
            PhongId = roomId,
            KhachDungTenId = profiles[0].Id,
            TienCoc = 3500000,
            TrangThai = "DANG_HIEU_LUC"
        };
        db.HopDongs.Add(contract);
        db.SaveChanges();
        db.KyHopDongs.Add(new KyHopDongThamChieu
        {
            HopDongId = contract.Id,
            NgayBatDau = Start,
            NgayKetThuc = End,
            GiaThue = 3500000
        });
        db.NguoiOGheps.Add(new NguoiOGhep
        {
            HopDongId = contract.Id,
            KhachThueId = profiles[1].Id,
            NgayVao = Start
        });
        db.Database.ExecuteSqlRaw("""
            INSERT INTO dich_vu(ma_dich_vu, ten_dich_vu, dang_hoat_dong)
            VALUES ('DIEN', 'Điện', 1), ('NET', 'Internet', 1)
            """);
        db.Database.ExecuteSqlRaw("""
            INSERT INTO cau_hinh_dich_vu(toa_nha_id, dich_vu_id, cach_tinh, don_vi_tinh, don_gia,
                tu_ngay, dang_ap_dung, da_chot_gia, nguoi_tao_id, ngay_tao)
            SELECT {0}, id, CASE ma_dich_vu WHEN 'DIEN' THEN 'THEO_CHI_SO' ELSE 'CO_DINH' END,
                CASE ma_dich_vu WHEN 'DIEN' THEN 'kWh' ELSE 'tháng' END,
                CASE ma_dich_vu WHEN 'DIEN' THEN 3500 ELSE 120000 END,
                {1}, 1, 1, {2}, {3}
            FROM dich_vu WHERE ma_dich_vu IN ('DIEN', 'NET')
            """, building.Id, Start.ToString("yyyy-MM-dd"), ownerAccountId, DateTime.UtcNow);
        db.Database.ExecuteSqlRaw("""
            INSERT INTO hop_dong_dich_vu(hop_dong_id, dich_vu_id, cau_hinh_dich_vu_id,
                ten_dich_vu, cach_tinh, don_vi_tinh, don_gia, ngay_ghi_nhan)
            SELECT {0}, service.id, config.id, service.ten_dich_vu, config.cach_tinh,
                config.don_vi_tinh, config.don_gia, {1}
            FROM dich_vu service JOIN cau_hinh_dich_vu config ON config.dich_vu_id = service.id
            WHERE service.ma_dich_vu IN ('DIEN', 'NET')
            """, contract.Id, DateTime.UtcNow);
        db.SaveChanges();
    }

    [Fact]
    public async Task SignatoryAndCotenantsCanReadTheContractAndOccupants()
    {
        foreach (var accountId in new[] { ownerAccountId, coTenantAccountId })
        {
            var controller = ControllerFor(accountId);
            var list = Assert.IsType<ViewResult>(await controller.Index());
            var listModel = Assert.IsType<HopDongCuaToiViewModel>(list.Model);
            var card = Assert.Single(listModel.Contracts);
            Assert.Equal(ContractCode, card.MaHopDong);
            Assert.Equal("A108", card.MaPhong);

            var details = Assert.IsType<ViewResult>(await controller.Details(ContractCode));
            var model = Assert.IsType<HopDongChiTietViewModel>(details.Model);
            Assert.Equal(3500000, model.GiaThueHienTai);
            Assert.Equal(3500000, model.TienCoc);
            Assert.Equal(Start, model.NgayBatDau);
            Assert.Equal(End, model.NgayKetThuc);
            Assert.Collection(model.NguoiO,
                owner => Assert.Equal("Nguyễn An", owner.HoTen),
                guest => Assert.Equal("Trần Bình", guest.HoTen));
            Assert.Collection(model.DichVus,
                service =>
                {
                    Assert.Equal("Internet", service.TenDichVu);
                    Assert.Equal("CO_DINH", service.CachTinh);
                    Assert.Equal(120000, service.DonGia);
                },
                service =>
                {
                    Assert.Equal("Điện", service.TenDichVu);
                    Assert.Equal("THEO_CHI_SO", service.CachTinh);
                    Assert.Equal(3500, service.DonGia);
                });
        }
    }

    [Fact]
    public async Task SignatoryAndCotenantsCanDownloadCompleteContractPdf()
    {
        foreach (var accountId in new[] { ownerAccountId, coTenantAccountId })
        {
            var result = Assert.IsType<FileContentResult>(
                await ControllerFor(accountId).DownloadPdf(ContractCode));

            Assert.Equal("application/pdf", result.ContentType);
            Assert.Equal($"HopDong-{ContractCode}.pdf", result.FileDownloadName);
            using var document = PdfDocument.Open(result.FileContents);
            var text = string.Join(" ", document.GetPages().Select(page => page.Text));

            Assert.Contains(ContractCode, text);
            Assert.Contains("A108", text);
            Assert.Contains("3.500.000", text);
            Assert.Contains("15/01/2026", text);
            Assert.Contains("14/01/2027", text);
            Assert.Contains("Nguyễn An", text);
            Assert.Contains("Trần Bình", text);
            Assert.Contains("Điện", text);
            Assert.Contains("Internet", text);
            Assert.Contains("Theo chỉ số", text);
            Assert.Contains("kWh", text);
            Assert.Contains("3.500", text);
            Assert.Contains("120.000", text);
        }
    }

    [Fact]
    public async Task PersonOutsideTheContractReceives403()
    {
        var result = Assert.IsType<StatusCodeResult>(await ControllerFor(outsiderAccountId).Details(ContractCode));
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);

        var downloadResult = Assert.IsType<StatusCodeResult>(
            await ControllerFor(outsiderAccountId).DownloadPdf(ContractCode));
        Assert.Equal(StatusCodes.Status403Forbidden, downloadResult.StatusCode);
    }

    [Fact]
    public async Task MissingContractShowsNotFoundWith404()
    {
        var controller = ControllerFor(ownerAccountId);
        var result = Assert.IsType<ViewResult>(await controller.Details("MISSING"));
        Assert.Equal("NotFound", result.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);

        var pdfController = ControllerFor(ownerAccountId);
        var pdfResult = Assert.IsType<ViewResult>(await pdfController.DownloadPdf("MISSING"));
        Assert.Equal("NotFound", pdfResult.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, pdfController.Response.StatusCode);
    }

    [Fact]
    public async Task ContractDetailsRemainAvailableWhenOptionalServiceSnapshotTableIsMissing()
    {
        db.Database.ExecuteSqlRaw("DROP TABLE hop_dong_dich_vu");

        var result = Assert.IsType<ViewResult>(await ControllerFor(ownerAccountId).Details(ContractCode));
        var model = Assert.IsType<HopDongChiTietViewModel>(result.Model);

        Assert.Empty(model.DichVus);
    }

    [Theory]
    [InlineData(29, 29)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    [InlineData(31, 31)]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    public void RemainingDaysUsesDateDifference(int offset, int expected)
    {
        var today = new DateOnly(2026, 10, 10);
        Assert.Equal(expected, HopDongCuaToiController.SoNgayConLai(today.AddDays(offset), today));
    }

    [Fact]
    public void MissingEndDateHasNoRemainingDays()
    {
        Assert.Null(HopDongCuaToiController.SoNgayConLai(null, new DateOnly(2026, 10, 10)));
    }

    [Theory]
    [InlineData(29, true)]
    [InlineData(1, true)]
    [InlineData(30, false)]
    [InlineData(31, false)]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void ExpiryWarningIncludesTodayAndDaysUnderThirty(int remaining, bool expected)
    {
        var model = new HopDongChiTietViewModel { SoNgayConLai = remaining };
        Assert.Equal(expected, model.SapHetHan);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task RoommateRemainsPresentThroughTheirDepartureDate(int offset, bool expected)
    {
        var departure = new DateOnly(2026, 10, 12);
        db.NguoiOGheps.Single().NgayRa = departure;
        await db.SaveChangesAsync();
        var time = new MockTimeProvider { UtcNow = departure.AddDays(offset).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) };
        var details = Assert.IsType<HopDongChiTietViewModel>(Assert.IsType<ViewResult>(
            await ControllerFor(ownerAccountId, time).Details(ContractCode)).Model);
        Assert.Equal(expected, details.NguoiO.Single(x => x.VaiTro == "Người ở cùng").DangO);
    }

    private HopDongCuaToiController ControllerFor(int accountId, ITimeProvider? time = null)
    {
        var controller = new HopDongCuaToiController(db, new HopDongPdfService(), time)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "test"))
                }
            }
        };
        return controller;
    }

    private static TaiKhoan NewAccount(string name, string email) => new()
    {
        HoTen = name,
        Email = email,
        SoDienThoai = "09" + Random.Shared.Next(0, 100000000).ToString("D8"),
        MatKhau = "unused-test-hash",
        VaiTro = "KHACH_THUE",
        DangHoatDong = true,
        NgayTao = DateTime.UtcNow,
        NgayCapNhat = DateTime.UtcNow
    };

    private void InsertAccount(TaiKhoan account) =>
        db.Database.ExecuteSqlRaw("""
            INSERT INTO tai_khoan(ho_ten, email, so_dien_thoai, mat_khau, vai_tro, dang_hoat_dong,
                must_change_password, email_confirmed, is_deleted, is_staff, is_superuser, ngay_tao, ngay_cap_nhat)
            VALUES ({0}, {1}, {2}, {3}, {4}, 1, 0, 1, 0, 0, 0, {5}, {5})
            """, account.HoTen, account.Email, account.SoDienThoai, account.MatKhau, account.VaiTro, DateTime.UtcNow);

    private static KhachThue NewProfile(int accountId, string name) => new()
    {
        TaiKhoanId = accountId,
        HoTen = name,
        NgayTao = DateTime.UtcNow
    };

    public void Dispose()
    {
        db.Dispose();
        connection.Dispose();
    }
}
