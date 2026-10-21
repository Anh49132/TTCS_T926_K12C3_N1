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
public sealed class EmailGuiLogController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> BaoHong(string? trangThai="THAT_BAI",int page=1,CancellationToken ct=default)
    {
        if(!string.IsNullOrEmpty(trangThai) && !EmailGuiLogViewModel.TrangThais.ContainsKey(trangThai))return BadRequest();
        if(!await QL_PhongTro.Services.RepairEmailQueue.InstalledAsync(db,ct))return View(new List<QL_PhongTro.Services.RepairEmailRow>());
        var query=db.Database.SqlQueryRaw<QL_PhongTro.Services.RepairEmailRow>(QL_PhongTro.Services.RepairEmailDispatcher.SelectSql);
        if(!string.IsNullOrEmpty(trangThai))query=query.Where(x=>x.Status==trangThai);
        var total=await query.CountAsync(ct);var pages=Math.Max(1,(total+49)/50);page=Math.Clamp(page,1,pages);
        ViewData["Total"]=total;ViewData["Page"]=page;ViewData["Pages"]=pages;ViewData["Status"]=trangThai;
        return View(await query.OrderByDescending(x=>x.Id).Skip((page-1)*50).Take(50).ToListAsync(ct));
    }
    [HttpGet]
    public async Task<IActionResult> Index(EmailGuiLogViewModel filter)
    {
        if (!ModelState.IsValid || filter.TuNgay > filter.DenNgay || filter.DenNgay?.Year > 9998
            || filter.TuNgay?.Year < 1900 || filter.DenNgay?.Year < 1900
            || (!string.IsNullOrEmpty(filter.TrangThai) && !EmailGuiLogViewModel.TrangThais.ContainsKey(filter.TrangThai)))
            return BadRequest("Bộ lọc không hợp lệ. Kiểm tra trạng thái và khoảng ngày.");
        if(filter.Nhom is not (null or "" or "HOA_DON" or "THANH_TOAN"))return BadRequest("Nhóm email không hợp lệ.");
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !await db.TaiKhoans.AsNoTracking().AnyAsync(a => a.Id == id && a.DangHoatDong && a.VaiTro == "ADMIN" && !a.MustChangePassword)) return Forbid();

        var query = db.EmailGuiLogs.AsNoTracking().AsQueryable();
        if(filter.Nhom=="THANH_TOAN")query=query.Where(x=>x.LoaiThongBao.StartsWith(QL_PhongTro.Services.PaymentEmailQueue.Prefix));
        if(filter.Nhom=="HOA_DON")query=query.Where(x=>!x.LoaiThongBao.StartsWith(QL_PhongTro.Services.PaymentEmailQueue.Prefix));

        if (!string.IsNullOrEmpty(filter.TrangThai))
            query = query.Where(x => x.TrangThai == filter.TrangThai);

        if (filter.TuNgay is { } from)
        {
            var utc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
            query = query.Where(x => x.NgayGui >= utc);
        }
        if (filter.DenNgay is { } to)
        {
            var utc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
            query = query.Where(x => x.NgayGui < utc);
        }

        filter.Total = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, filter.Pages);
        filter.Rows = await query.OrderByDescending(x => x.NgayGui).ThenByDescending(x => x.Id)
            .Skip((filter.Page - 1) * 50).Take(50)
            .Select(x => new EmailGuiLogRow(x.Id, x.HoaDonId, x.LoaiThongBao, x.NguoiNhanId, x.EmailNguoiNhan,
                x.TrangThai, x.SoLanThu, x.LoiGanNhat, x.NgayGui, x.NgayThuTiep))
            .ToListAsync();

        return View(filter);
    }
}
