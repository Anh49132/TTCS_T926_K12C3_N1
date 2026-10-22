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
public sealed class BaoHongController(BaoHongService service, BaoHongImageStore images) : Controller
{
    private int ActorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] BaoHongBoLoc filter, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest("Điều kiện lọc báo hỏng không hợp lệ.");
        try { var result = await service.DanhSachLocAsync(ActorId, filter, ct); ViewData["Page"] = filter.Page; ViewData["BaoHongBoLoc"] = filter; ViewData["BaoHongTongQuan"] = result.Summary; return View(result.Rows); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException) { return BadRequest("Điều kiện lọc báo hỏng không hợp lệ."); }
    }
    [HttpGet, ModuleAccess("BAO_HONG", write: true)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        try { return View(new TaoBaoHongViewModel { Phongs = await service.PhongsAsync(ActorId, ct) }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [RequestSizeLimit(20 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
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
        catch (IOException)
        { ModelState.AddModelError("Input.Images", "Không lưu được ảnh. Báo hỏng chưa được gửi; hãy chọn lại ảnh và thử lại."); }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        { ModelState.AddModelError("", "Chưa xác nhận được kết quả gửi. Hãy thử lại cùng biểu mẫu hoặc kiểm tra danh sách trước khi tạo yêu cầu mới."); }
        if (input.Images.Count > 0) ModelState.AddModelError("Input.Images", "Hãy chọn lại ảnh trước khi gửi lại biểu mẫu.");
        try { return View(new TaoBaoHongViewModel { Input = input, Phongs = await service.PhongsAsync(ActorId, ct) }); }
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
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        try { var result = await service.ChiTietAsync(ActorId, id, ct); return result is null ? NotFound() : View(result); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
