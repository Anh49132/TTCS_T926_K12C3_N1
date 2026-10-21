using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "KHACH_THUE"), ModuleAccess("TAI_CHINH")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/chung-tu-thanh-toan")]
public sealed class PaymentEvidenceController(IPaymentProofService proofs) : Controller
{
    private int Actor => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [HttpGet("{id:int}/link")]
    public async Task<IActionResult> Link(int id, CancellationToken ct)
    {
        try { var link = await proofs.CreateLinkAsync(Actor, id, ct); return Json(new { url = Url.Action(nameof(Image), new { id, token = link.Token }), expiresAt = link.ExpiresUtc }); }
        catch (UnauthorizedAccessException) { return Forbid(); } catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> Image(int id, string token, CancellationToken ct)
    {
        try
        {
            var file = await proofs.OpenAsync(Actor, id, token, ct);
            Response.Headers["X-Content-Type-Options"] = "nosniff"; Response.Headers["Referrer-Policy"] = "no-referrer";
            return PhysicalFile(file.Path, file.ContentType, enableRangeProcessing: false);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (PaymentProofLinkExpiredException) { return StatusCode(410); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
