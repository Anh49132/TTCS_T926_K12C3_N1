using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[Authorize, Route("api/ngan-hang")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerBankController(OwnerBankService service, OwnerBankImageStore images) : Controller
{
    private int Actor => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [HttpGet("cua-toi"), Authorize(Roles = "CHU_NHA")]
    public async Task<IActionResult> Own(CancellationToken ct)
    {
        try { return Json(new { bank = OwnerBankService.Response(await service.OwnAsync(Actor, ct)) }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpPost("cua-toi"), Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Save([FromForm] OwnerBankInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { errors = new Dictionary<string, string[]> { [""] = ["Dữ liệu không hợp lệ."] } });
        try { return Json(new { bank = OwnerBankService.Response(await service.SaveAsync(Actor, input, ct)) }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (OwnerBankValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
        catch (InvalidOperationException ex) { return Conflict(new { errors = new Dictionary<string, string[]> { [""] = [ex.Message] } }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException or DbException)
        { return Conflict(new { errors = new Dictionary<string, string[]> { [""] = ["Không thể lưu thông tin. Dữ liệu cũ được giữ nguyên; vui lòng thử lại và chọn lại ảnh."] } }); }
    }
    [HttpGet("cua-toi/qr"), Authorize(Roles = "CHU_NHA")]
    public async Task<IActionResult> OwnQr(CancellationToken ct)
    {
        try { return Qr(await service.OwnAsync(Actor, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet("hoa-don/{id:int}"), Authorize(Roles = "KHACH_THUE"), ModuleAccess("TAI_CHINH")]
    public async Task<IActionResult> Invoice(int id, CancellationToken ct)
    {
        try { return Json(new { bank = OwnerBankService.Response(await service.ForInvoiceAsync(Actor, id, ct)) }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet("hoa-don/{id:int}/qr"), Authorize(Roles = "KHACH_THUE"), ModuleAccess("TAI_CHINH")]
    public async Task<IActionResult> InvoiceQr(int id, CancellationToken ct)
    {
        try { return Qr(await service.ForInvoiceAsync(Actor, id, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    private IActionResult Qr(TaiKhoanNganHang? bank)
    {
        if (bank?.QrKey is null) return NotFound();
        var path = images.FilePath(bank.QrKey);
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, bank.QrContentType!);
    }
}
