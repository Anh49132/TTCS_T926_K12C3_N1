using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

public partial class HomeController
{
    private async Task<(bool Read, bool Write)> UnlistedAccessAsync()
    {
        if (db is null) return (false, false);
        var levels = await db.RolePermissions.AsNoTracking().Where(p => p.RoleCode == "CHU_NHA"
            && (p.ModuleCode == "PHONG_TRO" || p.ModuleCode == "TIN_DANG"))
            .ToDictionaryAsync(p => p.ModuleCode, p => p.AccessLevel);
        return ((levels.GetValueOrDefault("PHONG_TRO") is "READ" or "WRITE" or "FULL")
            && (levels.GetValueOrDefault("TIN_DANG") is "READ" or "WRITE" or "FULL"),
            levels.GetValueOrDefault("TIN_DANG") is "WRITE" or "FULL");
    }

    private async Task LoadUnlistedRoomsAsync(HomeDashboardViewModel dashboard, int ownerId, int? buildingId, DateTime now)
    {
        var access = await UnlistedAccessAsync();
        if (!access.Read || db is null) return;
        var service = new VacantRoomListingService(db);
        var unlisted = service.Query(ownerId, buildingId, now);
        dashboard.UnlistedRoomCount = await unlisted.CountAsync();
        dashboard.UnlistedRooms = await unlisted.Take(4).ToListAsync();
        dashboard.CanManageUnlistedRooms = access.Write;
        dashboard.UnlistedRoomsAvailable = true;
        dashboard.InterestedVacantRoomCodes = await (from request in db.YeuCauThues.AsNoTracking()
            join listing in db.TinDangs on request.TinDangId equals listing.Id
            join room in service.EligibleRooms(ownerId, buildingId, now) on listing.PhongId equals room.Id
            where request.TrangThai == QL_PhongTro.Models.TrangThaiYeuCau.Moi || request.TrangThai == QL_PhongTro.Models.TrangThaiYeuCau.DaHenLich
            select room.MaPhong).Distinct().OrderBy(code => code).Take(2).ToListAsync();
    }

    [Authorize(Roles = "CHU_NHA"), ModuleAccess("TIN_DANG"), HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> PhongTrongChuaDangTin(int? toaNhaId = null, int trang = 1)
    {
        if (db is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId)) return Forbid();
        var access = await UnlistedAccessAsync();
        if (!access.Read) return Forbid();
        var buildings = await db.ToaNhas.AsNoTracking().Where(b => b.ChuNhaId == ownerId && b.DangHoatDong)
            .OrderBy(b => b.TenToaNha).ThenBy(b => b.Id).Select(b => new HomeBuildingOption(b.Id, b.TenToaNha)).ToListAsync();
        if (toaNhaId.HasValue && !buildings.Any(b => b.Id == toaNhaId.Value)) return Forbid();
        if (expiration is not null) await expiration.ExpireAsync();
        var query = new VacantRoomListingService(db).Query(ownerId, toaNhaId, clock?.UtcNow ?? DateTime.UtcNow);
        var total = await query.CountAsync();
        const int pageSize = 15;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        trang = Math.Clamp(trang, 1, pages);
        return View(new HomeVacantRoomsViewModel
        {
            Rooms = await query.Skip((trang - 1) * pageSize).Take(pageSize).ToListAsync(),
            Buildings = buildings, BuildingId = toaNhaId, Total = total, Page = trang, TotalPages = pages, CanManage = access.Write
        });
    }
}
