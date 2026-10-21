using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;

namespace QL_PhongTro.Authorization;

// Scoped to one request: menu, page and API read the same current database snapshot.
public sealed class PermissionService(AppDbContext db, IHttpContextAccessor accessor, YeuCauThueService requests)
{
    private Task<List<RolePermission>>? permissions;
    public string? RoleCode => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
    public Task<List<RolePermission>> CurrentAsync() => permissions ??= LoadAsync();
    private async Task<List<RolePermission>> LoadAsync()
    {
        if (accessor.HttpContext?.User.Identity?.IsAuthenticated != true || RoleCode is null)
            return [];
        return await db.RolePermissions.AsNoTracking().Where(p => p.RoleCode == RoleCode).ToListAsync();
    }
    public async Task<bool> AllowsAsync(string module, bool write = false)
    {
        // Admin manages accounts, but business modules always remain read-only.
        // A legacy FULL row must not grant business mutations; NONE/missing still denies.
        if (RoleCode == "ADMIN" && module != "TAI_KHOAN" && write) return false;
        var permission = (await CurrentAsync()).SingleOrDefault(p => p.ModuleCode == module);
        return permission is not null && (write
            ? permission.AccessLevel is "WRITE" or "FULL"
            : permission.AccessLevel is "READ" or "WRITE" or "FULL");
    }
    public async Task<List<AppModule>> MenuAsync()
    {
        var allowed = (await CurrentAsync()).Where(p => p.AccessLevel is "READ" or "WRITE" or "FULL")
            // PHONG_TRO currently links to staff management, not a tenant room page.
            .Where(p => RoleCode != "KHACH_THUE" || p.ModuleCode != "PHONG_TRO")
            .Select(p => p.ModuleCode).ToList();
        return await db.AppModules.AsNoTracking().Where(m => allowed.Contains(m.Code))
            .OrderBy(m => m.SortOrder).ToListAsync();
    }
    public static string Url(string code) => code switch
    {
        "TAI_KHOAN" => "/HoSo/CuaToi",
        "PHONG_TRO" => "/PhongTro",
        "TIN_DANG" => "/TinDang",
        "BAO_CAO" => "/BaoCao",
        _ => "/Modules/" + Uri.EscapeDataString(code)
    };

    public async Task<int> UnprocessedRequestCountAsync()
    {
        if (RoleCode is not ("CHU_NHA" or "QUAN_LY" or "ADMIN")) return 0;
        if (!await AllowsAsync("YEU_CAU_THUE")) return 0;
        if (!int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return 0;
        if (!await requests.IsInstalled()) return 0;
        var role = RoleCode;
        var visibleBuildings = db.ToaNhas.AsNoTracking().Where(x => x.DangHoatDong &&
            (role == "ADMIN" || x.ChuNhaId == accountId || (role == "QUAN_LY" && x.QuanLyId == accountId)));
        return await db.YeuCauThues.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiYeuCau.Moi || x.TrangThai == TrangThaiYeuCau.DaHenLich)
            .Join(db.TinDangs.AsNoTracking(), request => request.TinDangId, listing => listing.Id,
                (request, listing) => listing.PhongId)
            .Join(db.PhongTros.AsNoTracking(), phongId => phongId, room => room.Id,
                (_, room) => room.ToaNhaId)
            .Join(visibleBuildings, toaNhaId => toaNhaId, building => building.Id, (_, _) => 1)
            .CountAsync();
    }
}

