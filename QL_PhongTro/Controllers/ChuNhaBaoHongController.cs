using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;
namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA,ADMIN"), ModuleAccess("BAO_HONG")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChuNhaBaoHongController(ChuNhaBaoHongService service, BaoHongImageStore images) : Controller
{
    private int ActorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 100000); ViewData["Page"] = page; ViewData["LaChuNha"] = true;
        try { return View("~/Views/QuanLyBaoHong/Index.cshtml", await service.DanhSachAsync(ActorId, page, ct)); }
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
}
