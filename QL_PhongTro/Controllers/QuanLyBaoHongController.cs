using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;

[Authorize(Roles = "QUAN_LY"), ModuleAccess("BAO_HONG")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class QuanLyBaoHongController(QuanLyBaoHongService service, BaoHongImageStore images, BaoHongChiPhiService costs) : Controller
{
    private int ActorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("/ThongBaoQuanLy/TomTat")]
    public async Task<IActionResult> ThongBaoTomTat(CancellationToken ct)
    {
        try
        {
            var summary = await service.TongQuanAsync(ActorId, new(), ct);
            var rows = await service.DanhSachLocAsync(ActorId, new() { SapXep = "UU_TIEN" }, ct);
            return Json(new { canTiepNhan = summary.Moi, items = rows.Take(6).Select(x => new {
                tieuDe = x.BaoHong.TrangThai == "MOI" ? "Báo hỏng cần tiếp nhận" : "Báo hỏng của khách thuê",
                noiDung = $"{x.TenNguoiBao} · Phòng {x.MaPhong} · {x.BaoHong.LoaiThietBi}",
                trangThai = BaoHongService.TenTrangThai(x.BaoHong.TrangThai),
                ngayTao = x.BaoHong.NgayTao.AddHours(7).ToString("dd/MM/yyyy HH:mm"),
                duongDan = Url.Action(nameof(Details), new { id = x.BaoHong.Id })
            }) });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("/ThongBaoQuanLy")]
    public async Task<IActionResult> ThongBao([FromQuery] BaoHongBoLoc filter, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Điều kiện lọc thông báo không hợp lệ.");
        try
        {
            var summary = await service.TongQuanAsync(ActorId, filter, ct);
            filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, (summary.KetQua + 19) / 20));
            var rows = await service.DanhSachLocAsync(ActorId, filter, ct);
            ViewData["BaoHongBoLoc"] = filter;
            ViewData["BaoHongTongQuan"] = summary;
            return View("ThongBao", rows.Take(20).ToList());
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException) { return BadRequest("Điều kiện lọc thông báo không hợp lệ."); }
    }
    private async Task LoadFilterAsync(BaoHongBoLoc filter, CancellationToken ct)
    {
        if (ModelState.Any(x => x.Value?.Errors.Count > 0 &&
            (x.Key.Equals("ToaNhaId", StringComparison.OrdinalIgnoreCase) || x.Key.Equals("Page", StringComparison.OrdinalIgnoreCase)
                || x.Key.StartsWith("filter.", StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Điều kiện lọc báo hỏng không hợp lệ.");
        ViewData["BaoHongToaNhas"] = await service.ToaNhaLocAsync(ActorId, filter, ct);
        ViewData["BaoHongBoLoc"] = filter;
        ViewData["Page"] = filter.Page;
    }

    private IActionResult DetailRedirect(int id, BaoHongBoLoc filter)
    {
        var routes = new Microsoft.AspNetCore.Routing.RouteValueDictionary();
        foreach (var route in filter.RouteValues()) routes[route.Key] = route.Value;
        routes["id"] = id;
        return RedirectToAction(nameof(Details), routes);
    }
    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("BAO_HONG", write: true)]
    public async Task<IActionResult> ChiPhi(int id, [Bind(Prefix = "ChiPhiInput")] BaoHongChiPhiInput input, CancellationToken ct, [FromQuery] BaoHongBoLoc filter)
    {
        try
        {
            await LoadFilterAsync(filter, ct);
            if (ModelState.IsValid)
            {
                await costs.LuuAsync(ActorId, id, input, ct);
                TempData["BaoHongSuccess"] = "Đã lưu chi phí sửa chữa.";
                return DetailRedirect(id, filter);
            }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException) { return BadRequest("Điều kiện lọc báo hỏng không hợp lệ."); }
        catch (BaoHongValidationException ex) { ModelState.AddModelError(ex.Field.Length == 0 ? "" : "ChiPhiInput." + ex.Field, ex.Message); }
        catch (ChiPhiConflictException ex) { Response.StatusCode = 409; ModelState.AddModelError("", ex.Message); }
        catch (Exception ex) when (ex is SqliteException or DbUpdateException)
        { ModelState.AddModelError("", "Chưa xác nhận được kết quả lưu. Hãy kiểm tra lịch sử hoặc thử lại cùng biểu mẫu."); }
        try
        {
            var result = await service.ChiTietAsync(ActorId, id, ct);
            if (result is null) return NotFound();
            if (!result.ChoPhepXuLy) return Forbid();
            result.ChiPhiInput = input;
            return View("~/Views/BaoHong/Details.cshtml", result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] BaoHongBoLoc filter, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest("Điều kiện lọc báo hỏng không hợp lệ.");
        try
        {
            await LoadFilterAsync(filter, ct);
            ViewData["BaoHongTongQuan"] = await service.TongQuanAsync(ActorId, filter, ct);
            return View(await service.DanhSachLocAsync(ActorId, filter, ct));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException) { return BadRequest("Điều kiện lọc báo hỏng không hợp lệ."); }
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct, [FromQuery] BaoHongBoLoc filter)
    {
        if (!ModelState.IsValid) return BadRequest("Điều kiện lọc báo hỏng không hợp lệ.");
        try
        {
            await LoadFilterAsync(filter, ct);
            var result = await service.ChiTietAsync(ActorId, id, ct);
            return result is null ? NotFound() : View("~/Views/BaoHong/Details.cshtml", result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException) { return BadRequest("Điều kiện lọc báo hỏng không hợp lệ."); }
    }
    [HttpGet]
    public async Task<IActionResult> Image(int id, int order, CancellationToken ct)
    {
        try
        {
            var report = await service.ChiTietAsync(ActorId, id, ct);
            var photo = report?.Images.SingleOrDefault(x => x.ThuTu == order);
            if (photo is null) return NotFound();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            try { return File(new FileStream(images.FilePath(photo.KhoaLuu), FileMode.Open, FileAccess.Read, FileShare.Read), photo.ContentType); }
            catch (FileNotFoundException) { return NotFound(); }
            catch (DirectoryNotFoundException) { return NotFound(); }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("BAO_HONG", write: true)]
    public async Task<IActionResult> PhanHoi(int id, [Bind(Prefix = "PhanHoi")] BaoHongPhanHoiInput input, CancellationToken ct, [FromQuery] BaoHongBoLoc filter) =>
        await SubmitAsync(id, "PhanHoi", input, null, filter, ct);

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("BAO_HONG", write: true)]
    public async Task<IActionResult> CapNhat(int id, [Bind(Prefix = "CapNhat")] BaoHongTrangThaiInput input, CancellationToken ct, [FromQuery] BaoHongBoLoc filter) =>
        await SubmitAsync(id, "CapNhat", null, input, filter, ct);

    private async Task<IActionResult> SubmitAsync(int id, string prefix, BaoHongPhanHoiInput? reply,
        BaoHongTrangThaiInput? update, BaoHongBoLoc filter, CancellationToken ct)
    {
        try
        {
            await LoadFilterAsync(filter, ct);
            if (ModelState.IsValid)
            {
                if (reply is not null) await service.PhanHoiAsync(ActorId, id, reply, ct);
                else await service.CapNhatAsync(ActorId, id, update!, ct);
                TempData["BaoHongSuccess"] = reply is not null ? "Đã gửi phản hồi." : "Đã cập nhật trạng thái.";
                return DetailRedirect(id, filter);
            }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException) { return BadRequest("Điều kiện lọc báo hỏng không hợp lệ."); }
        catch (BaoHongValidationException ex) { ModelState.AddModelError(ex.Field.Length == 0 ? "" : prefix + "." + ex.Field, ex.Message); }
        catch (BaoHongConflictException ex) { Response.StatusCode = 409; ModelState.AddModelError("", ex.Message); }
        catch (Exception ex) when (ex is SqliteException or DbUpdateException)
        { ModelState.AddModelError("", "Chưa xác nhận được kết quả lưu. Hãy kiểm tra lịch sử hoặc thử lại cùng biểu mẫu."); }
        try
        {
            var result = await service.ChiTietAsync(ActorId, id, ct);
            if (result is null) return NotFound();
            if (!result.ChoPhepXuLy) return Forbid();
            if (reply is not null) result.PhanHoi = reply;
            if (update is not null) result.CapNhat = update;
            return View("~/Views/BaoHong/Details.cshtml", result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
