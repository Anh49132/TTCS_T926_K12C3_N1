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
        if (code == "YEU_CAU_THUE" && section == "thong-bao" && User.IsInRole("QUAN_LY"))
            return RedirectToAction("ThongBao", "QuanLyBaoHong");
        var module = await db.AppModules.AsNoTracking().SingleOrDefaultAsync(m => m.Code == code);
        if (module is null) return NotFound();
        ViewData["SectionTitle"] = (code, section) switch
        {
            ("TAI_CHINH", "cong-no") => "Công nợ",
            ("YEU_CAU_THUE", "thong-bao") => "Thông báo",
            _ => null
        };
        if (code == "HOP_DONG") return RedirectToAction("Index", "HopDong");
        if (code == "TAI_CHINH" && section == null && (User.IsInRole("CHU_NHA") || User.IsInRole("ADMIN"))) return RedirectToAction("Management", "HoaDonDichVu");
        if (code == "DIEN_NUOC" && (User.IsInRole("QUAN_LY") || User.IsInRole("CHU_NHA") || User.IsInRole("ADMIN"))) return RedirectToAction("Index", "ChiSoDienNuoc");
        if (code == "BAO_HONG")
        {
            if (User.IsInRole("KHACH_THUE")) return RedirectToAction("Index", "BaoHong");
            if (User.IsInRole("QUAN_LY")) return RedirectToAction("Index", "QuanLyBaoHong");
            if (User.IsInRole("CHU_NHA") || User.IsInRole("ADMIN")) return RedirectToAction("Index", "ChuNhaBaoHong");
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
    public async Task<IActionResult> CongNo(int? toaNhaId, int? quaHanTu, int? quaHanDen, long? tienTu, long? tienDen,
        string? timKiem = null, string sapXep = "PHONG", string tinhTrang = "TAT_CA", int page = 1, int pageSize = 10)
    {
        ValidateDebt(quaHanTu, quaHanDen, tienTu, tienDen, timKiem, sapXep, tinhTrang);
        try
        {
            var valid = ModelState.IsValid;
            var model = await LoadDebt(toaNhaId, valid ? quaHanTu : null, valid ? quaHanDen : null, valid ? tienTu : null, valid ? tienDen : null,
                valid ? timKiem : null, valid ? sapXep : "PHONG", valid ? tinhTrang : "TAT_CA");
            model.LocLoi = !valid;
            model.PageSize = pageSize is 10 or 20 or 50 ? pageSize : 10;
            model.Page = Math.Clamp(page, 1, model.TotalPages);
            if (!valid)
            {
                Response.StatusCode = 400;
                ViewData["ToastError"] = "Chưa áp dụng được bộ lọc công nợ. Vui lòng kiểm tra các giá trị được báo lỗi.";
            }
            return View("CongNo", model);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private Task<CongNoIndexViewModel> LoadDebt(int? toaNhaId, int? quaHanTu, int? quaHanDen, long? tienTu, long? tienDen,
        string? timKiem = null, string sapXep = "PHONG", string tinhTrang = "TAT_CA")
        => new CongNoService(db, clock).LayAsync(ActorId, User.IsInRole("ADMIN"), toaNhaId, quaHanTu, quaHanDen, tienTu, tienDen, timKiem, sapXep, tinhTrang);

    private void ValidateDebt(int? from, int? to, long? min, long? max, string? search, string sort, string state)
    {
        foreach (var entry in ModelState.Values.Where(x => x.Errors.Count > 0))
        { entry.Errors.Clear(); entry.Errors.Add("Giá trị bộ lọc không hợp lệ. Ngày và tiền phải là số nguyên, tiền nhập bằng đồng, không có dấu phân cách."); }
        if (from < 0 || to < 0) ModelState.AddModelError("", "Số ngày quá hạn không được âm.");
        if (min < 0 || max < 0) ModelState.AddModelError("", "Tiền còn thiếu không được âm.");
        if (from > to) ModelState.AddModelError("", "Quá hạn từ không được lớn hơn quá hạn đến.");
        if (min > max) ModelState.AddModelError("", "Tiền thiếu từ không được lớn hơn tiền thiếu đến.");
        if (search?.Length > 100) ModelState.AddModelError("", "Từ khóa tối đa 100 ký tự.");
        if (sort is not ("PHONG" or "TIEN_GIAM" or "TIEN_TANG" or "QUAHAN_GIAM" or "QUAHAN_TANG")) ModelState.AddModelError("", "Cách sắp xếp không hợp lệ.");
        if (state is not ("TAT_CA" or "QUA_HAN" or "CHUA_QUA_HAN")) ModelState.AddModelError("", "Trạng thái lọc không hợp lệ.");
    }

    [HttpGet("/CongNo/Phong/{id:int}"), ModuleAccess("TAI_CHINH"), Authorize(Roles = "CHU_NHA,ADMIN")]
    public async Task<IActionResult> DebtRoom(int id)
    {
        var room = await (from r in db.PhongTros.AsNoTracking() join b in db.ToaNhas.AsNoTracking() on r.ToaNhaId equals b.Id
            where r.Id == id select new { r.MaPhong, r.ToaNhaId, b.ChuNhaId, b.TenToaNha }).SingleOrDefaultAsync();
        if (room is null) return NotFound();
        if (!User.IsInRole("ADMIN") && room.ChuNhaId != ActorId) return Forbid();
        try
        {
            var model = await LoadDebt(room.ToaNhaId, null, null, null, null);
            return PartialView("_DebtRoom", model.Rows.SingleOrDefault(x => x.PhongId == id)
                ?? new CongNoRowViewModel(id, room.MaPhong, room.TenToaNha, "", 0, 0, 0));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("/CongNo/ExportCsv"), ModuleAccess("TAI_CHINH"), Authorize(Roles = "CHU_NHA,ADMIN")]
    public async Task<IActionResult> ExportCsv(int? toaNhaId, int? quaHanTu, int? quaHanDen, long? tienTu, long? tienDen,
        string? timKiem = null, string sapXep = "PHONG", string tinhTrang = "TAT_CA")
    {
        ValidateDebt(quaHanTu, quaHanDen, tienTu, tienDen, timKiem, sapXep, tinhTrang);
        if (!ModelState.IsValid) return BadRequest(new { message = "Không thể xuất CSV: bộ lọc chưa hợp lệ.", errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage) });
        CongNoIndexViewModel model;
        try { model = await LoadDebt(toaNhaId, quaHanTu, quaHanDen, tienTu, tienDen, timKiem, sapXep, tinhTrang); }
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
        var searchTag = string.IsNullOrWhiteSpace(timKiem) ? "" : "_tk-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(timKiem.Trim())))[..8];
        var name = $"cong-no-{clock.UtcNow.AddHours(7):yyyyMMdd-HHmmss}_toa-{toaNhaId?.ToString() ?? "tat-ca"}_qh-{quaHanTu ?? 0}-{quaHanDen?.ToString() ?? "max"}_tien-{tienTu ?? 0}-{tienDen?.ToString() ?? "max"}_{tinhTrang}_{sapXep}{searchTag}.csv";
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", name);
    }

    [HttpGet("/api/modules/{code}"), ModuleAccess]
    public async Task<IActionResult> Details(string code)
    {
        var module = await db.AppModules.AsNoTracking().SingleOrDefaultAsync(m => m.Code == code);
        return module is null ? NotFound() : Ok(new { module.Code, module.Name });
    }
}

