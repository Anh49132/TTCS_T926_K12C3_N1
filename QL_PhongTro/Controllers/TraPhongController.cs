using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA"), ModuleAccess("HOP_DONG", write: true)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TraPhongController(AppDbContext db, ITimeProvider clock) : Controller
{
    private int Actor => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private TraPhongService Service => new(db, clock);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var count = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('hop_dong','ky_hop_dong','hoa_don','chi_tiet_hoa_don','chi_so_dien_nuoc','hop_dong_chi_so_dau_ky','hop_dong_dich_vu')").SingleAsync();
        if (count != 7) { context.Result = View("ChuaSanSang"); return; }
        await next();
    }

    [HttpGet, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> Index()
    {
        var rows = await (from h in db.HopDongs.AsNoTracking() join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id join k in db.KhachThues on h.KhachDungTenId equals k.Id
            where b.ChuNhaId == Actor && h.TrangThai == "DANG_HIEU_LUC"
            orderby b.TenToaNha, p.MaPhong select new HopDongTraPhongChon(h.Id, h.MaHopDong, p.MaPhong, b.TenToaNha, k.HoTen)).ToListAsync();
        return View(rows);
    }

    [HttpGet, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> Nhap(int id)
    {
        try
        {
            var input = new TraPhongInput { HopDongId = id };
            var model = await Service.NhapAsync(Actor, input);
            if (model.HoaDonDaCo.HasValue) return RedirectToAction(nameof(ChiTiet), new { id = model.HoaDonDaCo });
            return View(model);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { ViewData["LoiTraPhong"] = ex.Message; return View(new TraPhongViewModel { Input = new() { HopDongId = id, NgayTra = Service.HomNay } }); }
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> Tinh([Bind(Prefix = "Input")] TraPhongInput input)
    {
        try
        {
            await Service.KiemTraQuyenAsync(Actor, input.HopDongId);
            if (!ModelState.IsValid) return await LoiAsync(input);
            var model = await Service.NhapAsync(Actor, input, true);
            model.Input.DauVanTay = TraPhongService.DauVanTay(model);
            ModelState.Remove("Input.DauVanTay");
            return View("ChiTiet", model);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); return await LoiAsync(input); }
        catch (OverflowException) { ModelState.AddModelError("", "Số tiền vượt phạm vi hỗ trợ."); return await LoiAsync(input); }
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> Luu([Bind(Prefix = "Input")] TraPhongInput input)
    {
        try
        {
            await Service.KiemTraQuyenAsync(Actor, input.HopDongId);
            if (!ModelState.IsValid) return await LoiAsync(input);
            var id = await Service.LuuAsync(Actor, input);
            TempData["TraPhongSuccess"] = "Đã lưu thông tin trả phòng và hóa đơn kỳ cuối ở trạng thái nháp.";
            return RedirectToAction(nameof(ChiTiet), new { id });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or SqliteException or OverflowException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", ex is InvalidOperationException ? ex.Message : "Không thể lưu hóa đơn do dữ liệu trùng hoặc đang thay đổi. Hãy tải lại và kiểm tra.");
            return await LoiAsync(input);
        }
    }

    private async Task<IActionResult> LoiAsync(TraPhongInput input)
    {
        TraPhongViewModel model;
        try { model = await Service.NhapAsync(Actor, new() { HopDongId = input.HopDongId }); }
        catch (InvalidOperationException) { model = new(); }
        model.Input = input;
        return View("Nhap", model);
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> UocTinh([Bind(Prefix = "Input")] TraPhongInput input)
    {
        try
        {
            await Service.KiemTraQuyenAsync(Actor, input.HopDongId);
            if (!ModelState.IsValid) return BadRequest(new { error = "Nhập đầy đủ ngày trả và chỉ số hợp lệ." });
            var model = await Service.NhapAsync(Actor, input, true);
            var bill = model.HoaDon!;
            return Json(new { total = bill.TongTien.ToString(System.Globalization.CultureInfo.InvariantCulture), lines = bill.ChiTiet.OrderBy(x => x.SoThuTu).Select(x => new {
                name = x.TenKhoan, amount = x.ThanhTien.ToString(System.Globalization.CultureInfo.InvariantCulture), quantity = x.SoLuong, unit = x.DonViTinh,
                days = x.SoNgayTinhTien ?? DateTime.DaysInMonth(bill.Nam, bill.Thang), monthDays = DateTime.DaysInMonth(bill.Nam, bill.Thang), rent = x.LoaiKhoan == "TIEN_PHONG"
            }) });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException)
        { return BadRequest(new { error = ex is InvalidOperationException ? ex.Message : "Số tiền vượt phạm vi hỗ trợ." }); }
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> Sua([Bind(Prefix = "Input")] TraPhongInput input)
    {
        try { await Service.KiemTraQuyenAsync(Actor, input.HopDongId); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return await LoiAsync(input);
    }

    [HttpGet, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> ChiTiet(int id)
    {
        var invoice = await db.HoaDons.AsNoTracking().Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == id && x.LoaiHoaDon == "KY_CUOI");
        if (invoice is null) return NotFound();
        try { await Service.KiemTraQuyenAsync(Actor, invoice.HopDongId); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        var h = await db.HopDongs.AsNoTracking().SingleAsync(x => x.Id == invoice.HopDongId);
        var p = await db.PhongTros.AsNoTracking().SingleAsync(x => x.Id == h.PhongId);
        return View(new TraPhongViewModel { HoaDon = invoice, MaHopDong = h.MaHopDong, Phong = p.MaPhong,
            ToaNha = await db.ToaNhas.Where(x => x.Id == p.ToaNhaId).Select(x => x.TenToaNha).SingleAsync(),
            Khach = await db.KhachThues.Where(x => x.Id == h.KhachDungTenId).Select(x => x.HoTen).SingleOrDefaultAsync() ?? "",
            Input = new() { HopDongId = h.Id, NgayTra = invoice.DenNgay } });
    }
}
