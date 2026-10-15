using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Authorization;

namespace QL_PhongTro.Controllers;

public sealed partial class TatToanController
{
    [HttpGet, ModuleAccess("TAI_CHINH")]
    public async Task<IActionResult> TaiPdf(int id)
    {
        try
        {
            var pdf = await Service.PdfAsync(Actor, id);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(pdf.Content, "application/pdf", pdf.FileName);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}
