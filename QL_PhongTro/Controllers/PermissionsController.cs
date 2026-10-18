using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[ModuleAccess("TAI_KHOAN")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PermissionsController(AppDbContext db, PermissionService permissions) : Controller
{
    [HttpGet("/Permissions")]
    public async Task<IActionResult> Index()
    {
        if (!User.IsInRole("ADMIN")) return Forbid();
        return View(await MatrixAsync());
    }

    [HttpGet("/api/permissions")]
    public async Task<IActionResult> Data() => Ok(await MatrixAsync());

    private async Task<PermissionMatrixViewModel> MatrixAsync()
    {
        var current = (await permissions.CurrentAsync()).Single(p => p.ModuleCode == "TAI_KHOAN");
        var roles = db.AppRoles.AsNoTracking();
        // R* may inspect only their own role; it does not grant access to others' accounts.
        if (current.OwnDataOnly)
            roles = roles.Where(r => r.Code == permissions.RoleCode);
        var list = await roles.OrderBy(r => r.SortOrder).ToListAsync();
        var codes = list.Select(r => r.Code).ToList();
        return new()
        {
            Roles = list,
            Modules = await db.AppModules.AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync(),
            Permissions = await db.RolePermissions.AsNoTracking().Where(p => codes.Contains(p.RoleCode)).ToListAsync()
        };
    }
}

