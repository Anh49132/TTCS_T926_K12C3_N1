using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "KHACH_THUE")]
[Route("api/thong-bao")]
public class ThongBaoApiController(AppDbContext db, ITimeProvider clock) : Controller
{
    private int AccountId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("chua-doc")]
    public async Task<IActionResult> DemChuaDoc(CancellationToken cancellationToken)
    {
        var count = await db.ThongBaoHoaDons.AsNoTracking()
            .CountAsync(x => x.NguoiNhanId == AccountId && x.NgayDoc == null, cancellationToken);
        return Json(new { count });
    }

    [HttpGet("danh-sach")]
    public async Task<IActionResult> DanhSach(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var items = await db.ThongBaoHoaDons.AsNoTracking()
            .Where(x => x.NguoiNhanId == AccountId)
            .OrderByDescending(x => x.NgayTao).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.LoaiThongBao, x.TieuDe, x.NoiDung, x.DuongDan, x.NgayDoc, x.NgayTao })
            .ToListAsync(cancellationToken);
        var vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var result = items.Select(t => new
        {
            t.Id,
            t.LoaiThongBao,
            t.TieuDe,
            t.NoiDung,
            t.DuongDan,
            DaDoc = t.NgayDoc.HasValue,
            NgayTao = t.NgayTao.AddHours(7).ToString("dd/MM/yyyy HH:mm", vn)
        });
        return Json(new { items = result });
    }

    [HttpPost("{id}/da-doc"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DanhDauDaDoc(int id, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await db.ThongBaoHoaDons.Where(x => x.Id == id && x.NguoiNhanId == AccountId && x.NgayDoc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.NgayDoc, now), cancellationToken);
        if (!await db.ThongBaoHoaDons.AnyAsync(x => x.Id == id && x.NguoiNhanId == AccountId, cancellationToken))
            return NotFound();
        var count = await db.ThongBaoHoaDons.CountAsync(
            x => x.NguoiNhanId == AccountId && x.NgayDoc == null, cancellationToken);
        return Json(new { success = true, unreadCount = count });
    }

    [HttpPost("tat-ca-da-doc"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DanhDauTatCaDaDoc(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var markedCount = await db.ThongBaoHoaDons
            .Where(x => x.NguoiNhanId == AccountId && x.NgayDoc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.NgayDoc, now), cancellationToken);
        return Json(new { success = true, markedCount, unreadCount = 0 });
    }
}
