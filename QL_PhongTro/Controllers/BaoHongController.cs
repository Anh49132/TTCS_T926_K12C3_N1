using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;

[Authorize(Roles = "KHACH_THUE"), ModuleAccess("BAO_HONG")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BaoHongController(BaoHongService service) : Controller
{
    private int ActorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 100000); ViewData["Page"] = page;
        try { return View(await service.DanhSachAsync(ActorId, page, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet, ModuleAccess("BAO_HONG", write: true)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        try { return View(new TaoBaoHongViewModel { Phongs = await service.PhongsAsync(ActorId, ct) }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("BAO_HONG", write: true)]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] BaoHongInput input, CancellationToken ct)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var result = await service.GuiAsync(ActorId, input, ct);
                TempData["BaoHongSuccess"] = "Đã gửi báo hỏng thành công.";
                return RedirectToAction(nameof(Details), new { id = result.Id });
            }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (BaoHongValidationException ex) { ModelState.AddModelError(ex.Field.Length == 0 ? "" : "Input." + ex.Field, ex.Message); }
        catch (Exception ex) when (ex is SqliteException or DbUpdateException)
        { ModelState.AddModelError("", "Chưa xác nhận được kết quả gửi. Hãy thử lại cùng biểu mẫu hoặc kiểm tra danh sách trước khi tạo yêu cầu mới."); }
        try { return View(new TaoBaoHongViewModel { Input = input, Phongs = await service.PhongsAsync(ActorId, ct) }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        try { var result = await service.ChiTietAsync(ActorId, id, ct); return result is null ? NotFound() : View(result); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
