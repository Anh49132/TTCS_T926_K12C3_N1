using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using System.Security.Claims;
namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA,KHACH_THUE"), ModuleAccess("HOP_DONG")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class YeuCauTraPhongController(AppDbContext db, ITimeProvider clock) : Controller
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private CheckoutRequestService Service => new(db, clock);
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!await Service.ReadyAsync()) { context.Result = StatusCode(503, "Luồng yêu cầu trả phòng cần cập nhật schema v40. Vui lòng liên hệ quản trị viên."); return; }
        await next();
    }
    [HttpGet]
    public async Task<IActionResult> Index(int? hopDongId, string? maHopDong, int page = 1)
    {
        if (!string.IsNullOrWhiteSpace(maHopDong))
        {
            hopDongId = await db.HopDongs.Where(x => x.MaHopDong == maHopDong).Select(x => (int?)x.Id).SingleOrDefaultAsync();
            if (hopDongId is null) return NotFound();
        }
        try { var model = await Service.PageAsync(Actor, hopDongId); model.Page = Math.Clamp(page, 1, Math.Max(1, (model.Rows.Count + 19) / 20)); return View(model); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "KHACH_THUE")]
    public async Task<IActionResult> Gui(int hopDongId, DateOnly? ngayDeNghi, string? lyDo)
    {
        try
        {
            if (!ModelState.IsValid) { await Service.PageAsync(Actor, hopDongId); throw new InvalidOperationException("Ngày đề nghị không hợp lệ."); }
            var id = await Service.SubmitAsync(Actor, hopDongId, ngayDeNghi, lyDo);
            TempData["Success"] = "Đã gửi yêu cầu trả phòng. Hợp đồng vẫn hiệu lực và tiền cọc chưa thay đổi.";
            return Redirect(Url.Action(nameof(Index), new { hopDongId }) + $"#request-{id}");
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or DbException)
        { TempData["Error"] = ex is InvalidOperationException ? ex.Message : "Không thể gửi yêu cầu. Có thể dữ liệu đã thay đổi; hãy tải lại để kiểm tra."; return RedirectToAction(nameof(Index), new { hopDongId }); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> XuLy(int id, int? phienBan, string? thaoTac, DateOnly? ngayBanGiao, string? phanHoi)
    {
        if (User.IsInRole("KHACH_THUE") && thaoTac != "DA_HUY") return Forbid();
        if (User.IsInRole("CHU_NHA") && thaoTac == "DA_HUY") return Forbid();
        if (User.IsInRole("CHU_NHA") && !await HttpContext.RequestServices.GetRequiredService<PermissionService>().AllowsAsync("HOP_DONG", true)) return Forbid();
        try
        {
            if (!ModelState.IsValid || phienBan is null) throw new InvalidOperationException("Dữ liệu xử lý không hợp lệ. Hãy tải lại yêu cầu.");
            await Service.ChangeAsync(Actor, id, phienBan.Value, thaoTac ?? "", ngayBanGiao, phanHoi);
            TempData["Success"] = thaoTac == "DA_TIEP_NHAN" ? "Đã tiếp nhận và ghi nhận ngày bàn giao. Chưa kết thúc hợp đồng hoặc tất toán cọc." : "Đã cập nhật yêu cầu trả phòng.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or DbException)
        { TempData["Error"] = ex is InvalidOperationException ? ex.Message : "Yêu cầu đã thay đổi hoặc không thể lưu. Hãy tải lại và kiểm tra."; }
        return Redirect(Url.Action(nameof(Index)) + $"#request-{id}");
    }
}
