using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA,QUAN_LY,ADMIN")]
[ModuleAccess("BAO_CAO")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BaoCaoController(BaoCaoService service) : Controller
{
    private int ActorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string ActorRole => User.FindFirstValue(ClaimTypes.Role) ?? "";

    [HttpGet("/BaoCao")]
    [HttpGet("/BaoCao/DoanhThu")]
    public async Task<IActionResult> Index(
        int? toaNhaId,
        int? tuThang = null,
        int? tuNam = null,
        int? denThang = null,
        int? denNam = null,
        int? thang = null,
        int? nam = null,
        CancellationToken ct = default)
    {
        ViewData["Title"] = "B\u00E1o c\u00E1o doanh thu v\u00E0 l\u1EA5p \u0111\u1EA7y";
        if (thang.HasValue)
        {
            tuThang ??= thang;
            denThang ??= thang;
        }
        if (nam.HasValue)
        {
            tuNam ??= nam;
            denNam ??= nam;
        }

        try
        {
            var model = await service.LayBaoCaoAsync(
                ActorId,
                ActorRole,
                toaNhaId,
                tuThang,
                tuNam,
                denThang,
                denNam,
                ct);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
