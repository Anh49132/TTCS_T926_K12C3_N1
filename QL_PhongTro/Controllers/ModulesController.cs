using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

// Catalog only. No unimplemented business data or placeholder CRUD endpoints.
[ModuleAccess]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ModulesController(AppDbContext db) : Controller
{
    [HttpGet("/Modules/{code}")]
    public async Task<IActionResult> Index(string code, string? section)
    {
        var module = await db.AppModules.AsNoTracking().SingleOrDefaultAsync(m => m.Code == code);
        if (module is null) return NotFound();
        ViewData["SectionTitle"] = (code, section) switch
        {
            ("TAI_CHINH", "cong-no") => "Công nợ",
            ("YEU_CAU_THUE", "thong-bao") => "Thông báo",
            _ => null
        };
        if (code == "HOP_DONG") return RedirectToAction("Index", "HopDong");
        if (code == "TAI_CHINH" && section == null && User.IsInRole("CHU_NHA")) return RedirectToAction("Management", "HoaDonDichVu");
        if (code == "DIEN_NUOC" && (User.IsInRole("QUAN_LY") || User.IsInRole("CHU_NHA"))) return RedirectToAction("Index", "ChiSoDienNuoc");
        if (code == "BAO_HONG")
        {
            if (User.IsInRole("KHACH_THUE")) return RedirectToAction("Index", "BaoHong");
            if (User.IsInRole("QUAN_LY")) return RedirectToAction("Index", "QuanLyBaoHong");
            if (User.IsInRole("CHU_NHA")) return RedirectToAction("Index", "ChuNhaBaoHong");
            return View("BaoHong");
        }
        if (code == "TAI_CHINH" && section == "cong-no")
            return await CongNo(toaNhaId: null);
        if (code is "PHONG_TRO" or "TAI_KHOAN" or "TIN_DANG") return Redirect(PermissionService.Url(code));
        return View(module);
    }

    [HttpGet("/CongNo")]
    [ModuleAccess]
    public Task<IActionResult> CongNo(int? toaNhaId) => CongNoPage(toaNhaId);

    private async Task<IActionResult> CongNoPage(int? toaNhaId)
    {
        var buildings = await db.ToaNhas.AsNoTracking().OrderBy(x => x.TenToaNha)
            .Select(x => new CongNoIndexViewModel.BuildingOption(x.Id, x.TenToaNha)).ToListAsync();
        var invoiceRows = await (from invoice in db.HoaDons.AsNoTracking()
                                 join contract in db.HopDongs.AsNoTracking() on invoice.HopDongId equals contract.Id
                                 join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
                                 join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
                                 join account in db.TaiKhoans.AsNoTracking() on contract.KhachDungTenId equals account.Id into accounts
                                 from account in accounts.DefaultIfEmpty()
                                 where invoice.TrangThai == "DA_PHAT_HANH" && (!toaNhaId.HasValue || building.Id == toaNhaId.Value)
                                 select new { RoomId = room.Id, room.MaPhong, Building = building.TenToaNha, Name = account == null ? "Chưa có người đứng tên" : account.HoTen, InvoiceId = invoice.Id, invoice.TongTien, invoice.HanThanhToan }).ToListAsync();
        var paid = await db.ThanhToans.AsNoTracking().Where(x => x.TrangThai == "DA_XAC_NHAN")
            .GroupBy(x => x.HoaDonId).Select(x => new { Id = x.Key, Total = x.Sum(y => y.SoTien) }).ToDictionaryAsync(x => x.Id, x => x.Total);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = invoiceRows.GroupBy(x => new { x.RoomId, x.MaPhong, x.Building, x.Name }).Select(g =>
        {
            var outstanding = g.Where(x => x.TongTien - (paid.TryGetValue(x.InvoiceId, out var p) ? p : 0) > 0).ToList();
            return new CongNoRowViewModel(g.Key.RoomId, g.Key.MaPhong, g.Key.Building, g.Key.Name, outstanding.Count,
                outstanding.Sum(x => Math.Max(0, x.TongTien - (paid.TryGetValue(x.InvoiceId, out var p) ? p : 0))),
                outstanding.Count == 0 ? 0 : outstanding.Max(x => Math.Max(0, today.DayNumber - x.HanThanhToan.DayNumber)));
        }).Where(x => x.TongConThieu > 0).OrderBy(x => x.TenToaNha).ThenBy(x => x.MaPhong).ToList();
        return View("CongNo", new CongNoIndexViewModel { ToaNhaId = toaNhaId, Buildings = buildings, Rows = rows });
    }

    [HttpGet("/api/modules/{code}")]
    public async Task<IActionResult> Details(string code)
    {
        var module = await db.AppModules.AsNoTracking().SingleOrDefaultAsync(m => m.Code == code);
        return module is null ? NotFound() : Ok(new { module.Code, module.Name });
    }
}

