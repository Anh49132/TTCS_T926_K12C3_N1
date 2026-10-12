using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using UglyToad.PdfPig;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class ContractCreationTests
{
    private async Task<int> AssignedContractService(AppDbContext db)
    {
        DichVuSchemaInitializer.InitializeInvoices(path);
        await new DichVuService(db).ThemAsync(1, new TaoDichVuViewModel
        {
            ToaNhaId = 1, TenDichVu = "Internet hợp đồng", DonGia = 100000,
            CachTinh = CachTinhDichVu.CoDinh, DonViTinh = "tháng", ChonPhongCuThe = true, PhongIds = [1]
        });
        await db.Database.ExecuteSqlRawAsync("UPDATE cau_hinh_dich_vu SET tu_ngay='2026-10-01'");
        return await db.DichVuPhongs.Select(x => x.DichVuToaNha.DichVuId).SingleAsync();
    }

    [Fact]
    public async Task CreatedContractPdfIncludesAssignedPrivatePriceAndKeepsItsSnapshot()
    {
        using var db = Context();
        var serviceId = await AssignedContractService(db);
        await db.Database.ExecuteSqlRawAsync("UPDATE dich_vu_phong SET don_gia_rieng=123456");
        // A future price applies on the selected start date, not on today's form date.
        var old = await db.CauHinhDichVus.SingleAsync();
        old.DenNgay = new(2026, 10, 31);
        db.CauHinhDichVus.Add(new CauHinhDichVu
        {
            ToaNhaId = 1, DichVuId = serviceId, DonGia = 250000, CachTinh = CachTinhDichVu.CoDinh,
            DonViTinh = "tháng", TuNgay = new(2026, 11, 1), NguoiTaoId = 1, NgayTao = new FormClock().UtcNow
        });
        await db.SaveChangesAsync();
        Assert.IsType<RedirectToActionResult>(await Controller(db).Create(Valid(), default));
        var contract = await db.HopDongs.SingleAsync();
        var snapshot = await db.HopDongDichVus.SingleAsync();
        Assert.Equal(123456, snapshot.DonGia);
        Assert.Equal(new DateOnly(2026, 11, 1), (await db.CauHinhDichVus.SingleAsync(x => x.Id == snapshot.CauHinhDichVuId)).TuNgay);
        await db.Database.ExecuteSqlRawAsync("UPDATE dich_vu_phong SET don_gia_rieng=999999;UPDATE khach_thue SET tai_khoan_id=2");
        var tenant = new HopDongCuaToiController(db, new HopDongPdfService(), new FormClock())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "2"), new Claim(ClaimTypes.Role, "KHACH_THUE")], "test")) } }
        };
        var pdf = Assert.IsType<FileContentResult>(await tenant.DownloadPdf(contract.MaHopDong));
        using var document = PdfDocument.Open(pdf.FileContents);
        var text = string.Join(" ", document.GetPages().Select(x => x.Text));
        Assert.Contains("Internet hợp đồng", text);
        Assert.Contains("123.456", text);
        Assert.DoesNotContain("999.999", text);
        Assert.Contains("Chưa ở", text);
        Assert.DoesNotContain("Đã chuyển đi", text);
    }

    [Fact]
    public async Task ContractServiceSnapshotFailureRollsBackContractRoomListingAndAudit()
    {
        using var db = Context();
        await AssignedContractService(db);
        var auditCount = await db.NhatKyHoatDongs.CountAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_contract_service BEFORE INSERT ON hop_dong_dich_vu BEGIN SELECT RAISE(ABORT,'snapshot failure'); END");
        Assert.IsType<ViewResult>(await Controller(db).Create(Valid(), default));
        using var check = Context();
        Assert.Empty(await check.HopDongs.ToListAsync());
        Assert.Empty(await check.KyHopDongs.ToListAsync());
        Assert.Equal("TRONG", (await check.PhongTros.SingleAsync()).TrangThai);
        Assert.Equal("DANG_HIEN_THI", (await check.TinDangs.SingleAsync()).TrangThai);
        Assert.Equal(auditCount, await check.NhatKyHoatDongs.CountAsync());
    }

    [Fact]
    public async Task ResavedDraftRefreshesServicesWithoutDuplicatingThem()
    {
        using var db = Context();
        await AssignedContractService(db);
        var input = Valid(); input.Intent = "NHAP";
        Assert.IsType<RedirectToActionResult>(await Controller(db).Create(input, default));
        await db.Database.ExecuteSqlRawAsync("UPDATE dich_vu_phong SET don_gia_rieng=120000");
        Assert.IsType<RedirectToActionResult>(await Controller(db).Create(input, default));
        Assert.Single(await db.HopDongs.ToListAsync());
        Assert.Equal(120000, (await db.HopDongDichVus.SingleAsync()).DonGia);
    }
}
