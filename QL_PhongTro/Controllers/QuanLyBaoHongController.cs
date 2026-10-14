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
    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("BAO_HONG", write: true)]
    public async Task<IActionResult> ChiPhi(int id, [Bind(Prefix = "ChiPhiInput")] BaoHongChiPhiInput input, CancellationToken ct)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await costs.LuuAsync(ActorId, id, input, ct);
                TempData["BaoHongSuccess"] = "Đã lưu chi phí sửa chữa.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
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
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 100000); ViewData["Page"] = page;
        try { return View(await service.DanhSachAsync(ActorId, page, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        try
        {
            var result = await service.ChiTietAsync(ActorId, id, ct);
            return result is null ? NotFound() : View("~/Views/BaoHong/Details.cshtml", result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
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
    public async Task<IActionResult> PhanHoi(int id, [Bind(Prefix = "PhanHoi")] BaoHongPhanHoiInput input, CancellationToken ct) =>
        await SubmitAsync(id, "PhanHoi", input, null, ct);

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("BAO_HONG", write: true)]
    public async Task<IActionResult> CapNhat(int id, [Bind(Prefix = "CapNhat")] BaoHongTrangThaiInput input, CancellationToken ct) =>
        await SubmitAsync(id, "CapNhat", null, input, ct);

    private async Task<IActionResult> SubmitAsync(int id, string prefix, BaoHongPhanHoiInput? reply,
        BaoHongTrangThaiInput? update, CancellationToken ct)
    {
        try
        {
            if (ModelState.IsValid)
            {
                if (reply is not null) await service.PhanHoiAsync(ActorId, id, reply, ct);
                else await service.CapNhatAsync(ActorId, id, update!, ct);
                TempData["BaoHongSuccess"] = reply is not null ? "Đã gửi phản hồi." : "Đã cập nhật trạng thái.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
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
