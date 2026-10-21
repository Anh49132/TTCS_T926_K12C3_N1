using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;

public partial class HoaDonDichVuController
{
    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> RejectPaymentReport(int id, int paymentId, string? lyDo, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (!await db.ThanhToans.AnyAsync(x => x.Id == paymentId && x.HoaDonId == id, ct)) return Forbid();
        try
        {
            await HttpContext.RequestServices.GetRequiredService<GhiThuHoaDonService>().TuChoiBaoCaoAsync(AccountId, paymentId, lyDo, ct);
            TempData["DraftSuccess"] = "Đã từ chối báo thanh toán và thông báo cho khách. Công nợ được giữ nguyên.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (GhiThuValidationException ex) { TempData["DraftError"] = string.Join(" ", ex.Errors.Values.SelectMany(x => x)); }
        catch (InvalidOperationException ex) { TempData["DraftError"] = ex.Message; }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        { TempData["DraftError"] = "Không thể từ chối lúc này. Chưa có thay đổi nào được lưu; hãy tải lại hóa đơn."; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> ConfirmPaymentReport(int id, int paymentId, string? khoaChongTrung, CancellationToken ct,
        bool daKiemTraTien = false, bool dungSoTien = false, bool dungKhach = false)
    {
        if (!ModelState.IsValid) return BadRequest();
        // Bind both IDs to prevent a forged return target or a cross-invoice confirmation.
        if (!await (from p in db.ThanhToans join i in db.HoaDons on p.HoaDonId equals i.Id
            join h in db.HopDongs on i.HopDongId equals h.Id join r in db.PhongTros on h.PhongId equals r.Id
            join b in db.ToaNhas on r.ToaNhaId equals b.Id where p.Id == paymentId && i.Id == id && b.ChuNhaId == AccountId select p.Id).AnyAsync(ct)) return Forbid();
        if (!daKiemTraTien || !dungSoTien || !dungKhach)
        {
            TempData["DraftError"] = "Hãy kiểm tra tiền thực nhận, số tiền và đúng khách thuê/phòng trước khi xác nhận.";
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            await HttpContext.RequestServices.GetRequiredService<GhiThuHoaDonService>().XacNhanBaoCaoAsync(AccountId, paymentId, khoaChongTrung, ct);
            TempData["DraftSuccess"] = "Đã xác nhận báo thanh toán và cập nhật số còn phải trả.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (GhiThuValidationException ex) { TempData["DraftError"] = string.Join(" ", ex.Errors.Values.SelectMany(x => x)); }
        catch (InvalidOperationException ex) { TempData["DraftError"] = ex.Message; }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        { TempData["DraftError"] = "Chưa thể xác nhận. Hãy tải lại hóa đơn; không có khoản thu mới được ghi thêm."; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> CancelCollection(HuyLanThuHoaDonInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { errors = new Dictionary<string, string[]> { [""] = ["Thông tin lần thu không hợp lệ. Hãy tải lại hóa đơn."] } });
        try { return Ok(await HttpContext.RequestServices.GetRequiredService<GhiThuHoaDonService>().HuyAsync(AccountId, input, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (GhiThuValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
        catch (InvalidOperationException ex) { return Conflict(new { errors = new Dictionary<string, string[]> { [""] = [ex.Message] } }); }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        { return Conflict(new { errors = new Dictionary<string, string[]> { [""] = ["Chưa thể hủy lần thu. Dữ liệu đã được giữ nguyên; hãy tải lại hóa đơn và thử lại."] } }); }
    }
    [HttpGet, Authorize(Roles = "CHU_NHA")]
    public async Task<IActionResult> CollectionDetails(int id, CancellationToken ct)
    {
        try { return Ok(await HttpContext.RequestServices.GetRequiredService<GhiThuHoaDonService>().ChiTietAsync(AccountId, id, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> Collect(GhiThuHoaDonInput input, CancellationToken ct)
    {
        // Transport is unformatted digits. Do not let culture-specific decimal binding turn a fractional amount into thousands.
        var rawAmount = Request.Form["SoTien"].ToString();
        if (rawAmount.Length == 0 || rawAmount.Any(c => c is < '0' or > '9'))
            ModelState.AddModelError("SoTien", "Chỉ nhập số nguyên đồng hợp lệ.");
        if (!ModelState.IsValid)
            return BadRequest(new { errors = ModelState.Where(x => x.Value!.Errors.Count != 0).ToDictionary(x => x.Key,
                x => new[] { x.Key == "SoTien" ? "Chỉ nhập số nguyên đồng hợp lệ." : "Giá trị không hợp lệ." }) });
        try { return Ok(await HttpContext.RequestServices.GetRequiredService<GhiThuHoaDonService>().GhiAsync(AccountId, input, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (GhiThuValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
        catch (InvalidOperationException ex) { return Conflict(new { errors = new Dictionary<string, string[]> { [""] = [ex.Message] } }); }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        { return Conflict(new { errors = new Dictionary<string, string[]> { [""] = ["Dữ liệu đang được cập nhật. Hãy thử lại cùng lần xác nhận để tránh ghi trùng."] } }); }
    }
}
