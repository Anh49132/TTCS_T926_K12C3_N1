using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA,ADMIN")]
[ModuleAccess("PHONG_TRO")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DichVuPhongController(DichVuPhongService services) : Controller
{
    private int AccountId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Index(int phongId)
    {
        try { return View(await services.XemAsync(AccountId, phongId)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private IActionResult AfterSave(int phongId, bool returnToRoom) => returnToRoom
        ? RedirectToAction("Edit", "PhongTro", new { id = phongId }, "room-services")
        : RedirectToAction(nameof(Index), new { phongId });

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> Set(int phongId, int dichVuToaNhaId, bool enabled, long? donGiaRieng, bool returnToRoom = false)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (donGiaRieng < 0)
        {
            TempData["Error"] = "Đơn giá riêng phải là số nguyên không âm.";
            return AfterSave(phongId, returnToRoom);
        }
        try
        {
            await services.DatDichVuAsync(AccountId, phongId, dichVuToaNhaId, enabled, donGiaRieng);
            TempData["Success"] = enabled ? "Đã cập nhật dịch vụ của phòng." : "Đã ghi nhận ngừng dịch vụ từ kỳ sau; kỳ hiện tại vẫn tính đủ và hóa đơn cũ giữ nguyên.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DbUpdateException) { TempData["Error"] = "Dịch vụ vừa thay đổi. Vui lòng tải lại và thử lại."; }
        return AfterSave(phongId, returnToRoom);
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> SetPrice(int phongId, int dichVuToaNhaId, long? donGiaRieng, bool returnToRoom = false)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (donGiaRieng < 0)
        {
            TempData["Error"] = "Đơn giá riêng phải là số nguyên không âm.";
            return AfterSave(phongId, returnToRoom);
        }
        try
        {
            await services.DatDonGiaRiengAsync(AccountId, phongId, dichVuToaNhaId, donGiaRieng);
            TempData["Success"] = donGiaRieng is null ? "Đã chuyển dịch vụ về đơn giá chung." : "Đã cập nhật đơn giá riêng.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException) { return NotFound(); }
        catch (DbUpdateException) { TempData["Error"] = "Dịch vụ vừa thay đổi. Vui lòng tải lại và thử lại."; }
        return AfterSave(phongId, returnToRoom);
    }
}
