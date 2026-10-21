using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.Authorization;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "KHACH_THUE,CHU_NHA"), ModuleAccess("TAI_CHINH")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/thong-bao")]
public class ThongBaoApiController(AppDbContext db, ITimeProvider clock) : Controller
{
    private int AccountId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private async Task<bool> CheckoutEnabled() => await HttpContext.RequestServices.GetRequiredService<PermissionService>().AllowsAsync("HOP_DONG")
        && await new CheckoutRequestService(db, clock).ReadyAsync();
    private async Task<int> CheckoutUnread(CancellationToken ct) => await CheckoutEnabled()
        ? await db.ThongBaoTraPhongs.CountAsync(x => x.NguoiNhanId == AccountId && x.NgayDoc == null, ct) : 0;

    [HttpGet("chua-doc")]
    public async Task<IActionResult> DemChuaDoc(CancellationToken cancellationToken)
    {
        var count = await db.ThongBaoHoaDons.AsNoTracking()
            .CountAsync(x => x.NguoiNhanId == AccountId && x.NgayDoc == null, cancellationToken);
        count += await db.ThongBaoThanhToans.CountAsync(x => x.NguoiNhanId == AccountId && x.NgayDoc == null, cancellationToken);
        count += await CheckoutUnread(cancellationToken);
        return Json(new { count });
    }

    [HttpGet("danh-sach")]
    public async Task<IActionResult> DanhSach(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 1000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var items = await db.ThongBaoHoaDons.AsNoTracking()
            .Where(x => x.NguoiNhanId == AccountId)
            .OrderByDescending(x => x.NgayTao).ThenByDescending(x => x.Id)
            .Take(page * pageSize)
            .Select(x => new { x.Id, x.LoaiThongBao, x.TieuDe, x.NoiDung, x.DuongDan, x.NgayDoc, x.NgayTao })
            .ToListAsync(cancellationToken);
        var payments = await db.ThongBaoThanhToans.AsNoTracking().Where(x => x.NguoiNhanId == AccountId)
            .OrderByDescending(x => x.NgayTao).ThenByDescending(x => x.Id).Take(page * pageSize).ToListAsync(cancellationToken);
        var checkouts = await CheckoutEnabled() ? await db.ThongBaoTraPhongs.AsNoTracking().Where(x => x.NguoiNhanId == AccountId)
            .OrderByDescending(x => x.NgayTao).ThenByDescending(x => x.Id).Take(page * pageSize).ToListAsync(cancellationToken) : [];
        var result = items.Select(t => new BellItem(t.Id, "INVOICE", t.LoaiThongBao, t.TieuDe, t.NoiDung, t.DuongDan, t.NgayDoc.HasValue, t.NgayTao))
            .Concat(payments.Select(t => new BellItem(t.Id, "PAYMENT", t.Loai, t.TieuDe, t.NoiDung, t.DuongDan, t.NgayDoc.HasValue, t.NgayTao)))
            .Concat(checkouts.Select(t => new BellItem(t.Id, "CHECKOUT", t.Loai, t.TieuDe, t.NoiDung, t.DuongDan, t.NgayDoc.HasValue, t.NgayTao)))
            .OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Json(new { items = result.Select(t => new
        {
            t.Id, t.Source, t.LoaiThongBao, t.TieuDe, t.NoiDung, t.DuongDan, t.DaDoc,
            NgayTao = t.CreatedUtc.AddHours(7).ToString("dd/MM/yyyy HH:mm")
        }) });
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
        count += await db.ThongBaoThanhToans.CountAsync(x => x.NguoiNhanId == AccountId && x.NgayDoc == null, cancellationToken);
        count += await CheckoutUnread(cancellationToken);
        return Json(new { success = true, unreadCount = count });
    }

    [HttpPost("tat-ca-da-doc"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DanhDauTatCaDaDoc(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var markedCount = await db.ThongBaoHoaDons
            .Where(x => x.NguoiNhanId == AccountId && x.NgayDoc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.NgayDoc, now), cancellationToken);
        markedCount += await db.ThongBaoThanhToans.Where(x => x.NguoiNhanId == AccountId && x.NgayDoc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.NgayDoc, now), cancellationToken);
        if (await CheckoutEnabled()) markedCount += await db.ThongBaoTraPhongs.Where(x => x.NguoiNhanId == AccountId && x.NgayDoc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.NgayDoc, now), cancellationToken);
        return Json(new { success = true, markedCount, unreadCount = 0 });
    }

    [HttpPost("thanh-toan/{id:int}/da-doc"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadPayment(int id, CancellationToken ct)
    {
        if (!await db.ThongBaoThanhToans.AnyAsync(x => x.Id == id && x.NguoiNhanId == AccountId, ct)) return NotFound();
        await db.ThongBaoThanhToans.Where(x => x.Id == id && x.NguoiNhanId == AccountId && x.NgayDoc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgayDoc, clock.UtcNow), ct);
        return Ok(new { success = true });
    }

    [HttpGet("/ThongBaoThanhToan")]
    public IActionResult Inbox() => View("~/Views/Shared/PaymentNotificationInbox.cshtml");

    [HttpPost("tra-phong/{id:int}/da-doc"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadCheckout(int id, CancellationToken ct)
    {
        if (!await CheckoutEnabled()) return Forbid();
        if (!await db.ThongBaoTraPhongs.AnyAsync(x => x.Id == id && x.NguoiNhanId == AccountId, ct)) return NotFound();
        await db.ThongBaoTraPhongs.Where(x => x.Id == id && x.NguoiNhanId == AccountId && x.NgayDoc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgayDoc, clock.UtcNow), ct);
        return Ok(new { success = true });
    }

    private sealed record BellItem(int Id, string Source, string LoaiThongBao, string TieuDe, string NoiDung, string DuongDan, bool DaDoc, DateTime CreatedUtc);
}
