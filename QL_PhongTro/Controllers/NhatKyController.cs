using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "ADMIN")]
[QL_PhongTro.Authorization.ModuleAccess("TAI_KHOAN")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NhatKyController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(NhatKyViewModel filter)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !await db.TaiKhoans.AsNoTracking().AnyAsync(a => a.Id == id && a.DangHoatDong && a.VaiTro == "ADMIN" && !a.MustChangePassword)) return Forbid();
        if (!ModelState.IsValid || filter.TuNgay > filter.DenNgay ||
            filter.TuNgay?.Year < 1900 || filter.TuNgay?.Year > 9998 ||
            filter.DenNgay?.Year < 1900 || filter.DenNgay?.Year > 9998 ||
            filter.NguoiThucHienId <= 0 ||
            (!string.IsNullOrEmpty(filter.LoaiDoiTuong) && !NhatKyViewModel.Types.ContainsKey(filter.LoaiDoiTuong)))
            return BadRequest("Bộ lọc không hợp lệ. Ngày từ phải không sau ngày đến (1900–9998).");
        var query = db.NhatKyHoatDongs.AsNoTracking();
        // Vietnam calendar dates map to a half-open UTC interval, including the whole end day.
        if (filter.TuNgay is { } from)
        {
            var utc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
            query = query.Where(x => x.ThoiDiem >= utc);
        }
        if (filter.DenNgay is { } to)
        {
            var utc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
            query = query.Where(x => x.ThoiDiem < utc);
        }
        if (filter.NguoiThucHienId is { } actor) query = query.Where(x => x.NguoiThucHienId == actor);
        if (!string.IsNullOrEmpty(filter.LoaiDoiTuong)) query = query.Where(x => x.LoaiDoiTuong == filter.LoaiDoiTuong);
        filter.Total = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, filter.Pages);
        filter.Rows = await query.OrderByDescending(x => x.ThoiDiem).ThenByDescending(x => x.Id)
            .Skip((filter.Page - 1) * 50).Take(50).ToListAsync();
        filter.Actors = await db.NhatKyHoatDongs.AsNoTracking().Where(x => x.NguoiThucHienId != null)
            .GroupBy(x => x.NguoiThucHienId).Select(g => new AuditActorOption(g.Key!.Value,
                g.OrderByDescending(x => x.Id).Select(x => x.TenNguoiThucHien).FirstOrDefault())).ToListAsync();
        filter.Actors = filter.Actors.OrderBy(x => x.Name).ToList();
        return View(filter);
    }
}
