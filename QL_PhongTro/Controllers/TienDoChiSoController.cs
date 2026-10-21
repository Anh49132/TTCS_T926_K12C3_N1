using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA"), ModuleAccess("DIEN_NUOC")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TienDoChiSoController(TienDoChiSoService progress, ITimeProvider clock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? nam, int? thang, int? toaNhaId, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId)) return Forbid();

        var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        var year = nam ?? today.Year;
        var month = thang ?? today.Month;
        try
        {
            var model = await progress.XemAsync(ownerId, year, month, ct, toaNhaId);
            model.CapNhatLuc = clock.UtcNow;
            model.CanhBaos = (await progress.LayCanhBaoAsync(ownerId, today, ct))
                .Where(x => x.Nam == year && x.Thang == month && model.ToaNhas.Any(b => b.ToaNhaId == x.ToaNhaId)).ToList();
            return View(model);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet]
    public async Task<IActionResult> PhongConThieu(int? toaNhaId, int? nam, int? thang, CancellationToken ct, bool drawer = false)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId)) return Forbid();
        if (toaNhaId is null || nam is null || thang is null) return BadRequest();

        try
        {
            var model = await progress.PhongConThieuAsync(ownerId, toaNhaId.Value, nam.Value, thang.Value, ct);
            return drawer ? PartialView("_MissingRooms", model) : View(model);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(ex.Message); }
    }
}
