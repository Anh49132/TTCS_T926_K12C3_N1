using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;

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
            return View("BaoHong");
        }
        if (code is "PHONG_TRO" or "TAI_KHOAN" or "TIN_DANG") return Redirect(PermissionService.Url(code));
        return View(module);
    }

    [HttpGet("/api/modules/{code}")]
    public async Task<IActionResult> Details(string code)
    {
        var module = await db.AppModules.AsNoTracking().SingleOrDefaultAsync(m => m.Code == code);
        return module is null ? NotFound() : Ok(new { module.Code, module.Name });
    }
}

