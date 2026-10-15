using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

public partial class HoaDonDichVuController
{
    [HttpGet, Authorize(Roles = "CHU_NHA,ADMIN")]
    public async Task<IActionResult> PaymentProofLink(int paymentId, CancellationToken ct)
    {
        try
        {
            var link = await HttpContext.RequestServices.GetRequiredService<IPaymentProofService>()
                .CreateLinkAsync(AccountId, paymentId, ct);
            var url = Url.Action(nameof(ViewPaymentProof), new { paymentId, token = link.Token });
            return Ok(new { url, expiresAt = link.ExpiresUtc });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet, Authorize(Roles = "CHU_NHA,ADMIN")]
    public async Task<IActionResult> ViewPaymentProof(int paymentId, string token, CancellationToken ct)
    {
        try
        {
            var proof = await HttpContext.RequestServices.GetRequiredService<IPaymentProofService>()
                .OpenAsync(AccountId, paymentId, token, ct);
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            return PhysicalFile(proof.Path, proof.ContentType, enableRangeProcessing: false);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (PaymentProofLinkExpiredException) { return StatusCode(StatusCodes.Status410Gone); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
