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
        ViewData["Title"] = "Báo cáo tài chính theo tháng";
        thang ??= denThang ?? tuThang ?? service.GioHienTaiVn.Month;
        nam ??= denNam ?? tuNam ?? service.GioHienTaiVn.Year;
        try
        {
            var model = await service.LayBaoCaoThangAsync(
                ActorId,
                ActorRole,
                toaNhaId,
                thang.Value,
                nam.Value,
                ct);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
