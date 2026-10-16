using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Globalization;
using QL_PhongTro.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

// Catalog only. No unimplemented business data or placeholder CRUD endpoints.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ModulesController(AppDbContext db, ITimeProvider clock) : Controller
{
    [HttpGet("/Modules/{code}"), ModuleAccess]
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
        if (code == "BAO_CAO") return RedirectToAction("Index", "BaoCao");
        if (code == "TAI_CHINH" && section == "cong-no")
        {
            if (!User.IsInRole("CHU_NHA") && !User.IsInRole("ADMIN")) return Forbid();
            return RedirectToAction(nameof(CongNo));
        }
        if (code is "PHONG_TRO" or "TAI_KHOAN" or "TIN_DANG") return Redirect(PermissionService.Url(code));
        return View(module);
    }

    private int ActorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("/CongNo"), ModuleAccess("TAI_CHINH"), Authorize(Roles = "CHU_NHA,ADMIN")]
    public async Task<IActionResult> CongNo(int? toaNhaId, int? quaHanTu, int? quaHanDen, long? tienTu, long? tienDen)
    {
        try { return View("CongNo", await LoadDebt(toaNhaId, quaHanTu, quaHanDen, tienTu, tienDen)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private Task<CongNoIndexViewModel> LoadDebt(int? toaNhaId, int? quaHanTu, int? quaHanDen, long? tienTu, long? tienDen)
        => new CongNoService(db, clock).LayAsync(ActorId, User.IsInRole("ADMIN"), toaNhaId, quaHanTu, quaHanDen, tienTu, tienDen);

    [HttpGet("/CongNo/ExportCsv"), ModuleAccess("TAI_CHINH"), Authorize(Roles = "CHU_NHA,ADMIN")]
    public async Task<IActionResult> ExportCsv(int? toaNhaId, int? quaHanTu, int? quaHanDen, long? tienTu, long? tienDen)
    {
        CongNoIndexViewModel model;
        try { model = await LoadDebt(toaNhaId, quaHanTu, quaHanDen, tienTu, tienDen); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        static string Csv(string value)
        {
            // Spreadsheet applications must treat user-supplied names as text.
            if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') ||
                value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        var csv = new StringBuilder("Phòng,Tòa nhà,Người đứng tên,HĐ chưa trả hết,Tổng còn thiếu,Quá hạn dài nhất\r\n");
        foreach (var row in model.Rows)
            csv.Append(string.Join(',', Csv(row.MaPhong), Csv(row.TenToaNha), Csv(row.NguoiDungTen),
                Number(row.SoHoaDonChuaTraHet), Number(row.TongConThieu), Number(row.SoNgayQuaHanDaiNhat))).Append("\r\n");
        csv.Append(string.Join(',', Csv("TỔNG KẾT QUẢ ĐANG LỌC"), "", "", Number(model.TotalInvoices), Number(model.TotalOutstanding), "")).Append("\r\n");
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"cong-no-{clock.UtcNow.AddHours(7):yyyyMMdd-HHmmss}.csv");
    }

    [HttpGet("/api/modules/{code}"), ModuleAccess]
    public async Task<IActionResult> Details(string code)
    {
        var module = await db.AppModules.AsNoTracking().SingleOrDefaultAsync(m => m.Code == code);
        return module is null ? NotFound() : Ok(new { module.Code, module.Name });
    }
}

