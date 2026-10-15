using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "KHACH_THUE")]
[Route("api/thong-bao")]
public class ThongBaoApiController(ThongBaoHoaDonService thongBaoService) : Controller
{
    private int AccountId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("chua-doc")]
    public async Task<IActionResult> DemChuaDoc()
    {
        var count = await thongBaoService.DemChuaDocAsync(AccountId);
        return Json(new { count });
    }

    [HttpGet("danh-sach")]
    public async Task<IActionResult> DanhSach(int page = 1, int pageSize = 20)
    {
        var items = await thongBaoService.LayDanhSachThongBaoAsync(AccountId, page, pageSize);
        var vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var result = items.Select(t => new
        {
            t.Id,
            t.LoaiThongBao,
            t.TieuDe,
            t.NoiDung,
            t.DuongDan,
            t.DaDoc,
            NgayTao = t.NgayTao.AddHours(7).ToString("dd/MM/yyyy HH:mm", vn)
        });
        return Json(new { items = result });
    }

    [HttpPost("{id}/da-doc")]
    public async Task<IActionResult> DanhDauDaDoc(int id)
    {
        var success = await thongBaoService.DanhDauDaDocAsync(id, AccountId);
        if (!success) return NotFound();
        var count = await thongBaoService.DemChuaDocAsync(AccountId);
        return Json(new { success = true, unreadCount = count });
    }

    [HttpPost("tat-ca-da-doc")]
    public async Task<IActionResult> DanhDauTatCaDaDoc()
    {
        var count = await thongBaoService.DanhDauTatCaDaDocAsync(AccountId);
        return Json(new { success = true, markedCount = count, unreadCount = 0 });
    }
}