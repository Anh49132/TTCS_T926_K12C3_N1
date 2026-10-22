using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

/// <summary>
/// S2-08 exposes one detail page, reached directly by URL. It deliberately has no Index
/// action: the landlord request list is S2-07 and the tenant list is S2-09, so a list here
/// would collide with both. The routes are written out in full for the same reason.
/// </summary>
[Authorize]
[ModuleAccess("YEU_CAU_THUE")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class LichHenController(
    LichHenService service,
    AppDbContext? db = null,
    IAppointmentEmailSender? email = null) : Controller
{
    private int AccountId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>The time the user types is Vietnam time; the database keeps UTC.</summary>
    private static DateTime? ChuyenNhapSangUtc(DateTime? nhap) => nhap.HasValue ? nhap.Value.AddHours(-7) : null;

    private static DateTime? ChuyenUtcSangNhap(DateTime? utc) => utc.HasValue ? utc.Value.AddHours(7) : null;

    [HttpGet("/LichHen/ChiTiet/{id:int}")]
    public async Task<IActionResult> ChiTiet(int id, CancellationToken ct)
    {
        // yeu_cau_thue thuộc S2-06 và có thể chưa tồn tại trên CSDL của máy khác. Khi thiếu bảng
        // thì báo trạng thái chờ dữ liệu, không ném lỗi. Còn khi bảng có mà không tìm thấy mã
        // yêu cầu, hoặc người xem không phải chủ nhà/khách của yêu cầu đó, vẫn trả NotFound/Forbid
        // để không lộ ra yêu cầu nào đang tồn tại.
        if (!await service.CoBangYeuCauAsync(ct)) return View("ChuaCoDuLieu");
        if (await service.DocAsync(id, ct) is not { } yeuCau) return NotFound();
        var laChuNha = await service.ChuNhaCuaYeuCauAsync(AccountId, id, ct);
        if (!laChuNha && !await service.CoQuyenDocYeuCauAsync(AccountId, id, ct)) return Forbid();

        List<LichHenLichSuMuc> lichSu;
        try
        {
            lichSu = await service.LichSuAsync(AccountId, id, ct);
        }
        catch (KeyNotFoundException) { return NotFound(); }

        var view = new LichHenChiTietViewModel
        {
            YeuCau = yeuCau,
            LaChuNha = laChuNha,
            HienThiFormXacNhan = laChuNha && yeuCau.TrangThai == LichHenTrangThai.Moi,
            LichHenNhap = ChuyenUtcSangNhap(yeuCau.LichHen),
            HienThiFormTuChoi = laChuNha
                && yeuCau.TrangThai is LichHenTrangThai.Moi or LichHenTrangThai.DaHenLich,
            HienThiFormDoiLich = laChuNha && yeuCau.TrangThai == LichHenTrangThai.DaHenLich,
            HienThiNutDuyetThueNgay = laChuNha
                && yeuCau.LoaiYeuCau == LichHenTrangThai.LoaiThueNgay
                && yeuCau.TrangThai is LichHenTrangThai.Moi or LichHenTrangThai.DaHenLich,
            HienThiNutLapHopDong = laChuNha
                && yeuCau.TrangThai == LichHenTrangThai.DaDuyet,
            HienThiNutDuyetSauKhiXem = laChuNha && yeuCau.LoaiYeuCau == LichHenTrangThai.LoaiXemPhong
                && yeuCau.TrangThai == LichHenTrangThai.DaHenLich && yeuCau.TrangThaiPhong == LichHenTrangThai.PhongTrong,
            HienThiNutHuy = yeuCau.TrangThai is LichHenTrangThai.Moi or LichHenTrangThai.DaHenLich,
            HienThiFormHuyDaDuyet = laChuNha && User.IsInRole("CHU_NHA")
                && yeuCau.TrangThai == LichHenTrangThai.DaDuyet,
            LichSu = lichSu
        };
        if (laChuNha && db is not null)
        {
            ViewData["RequestedOccupation"] = await db.KhachThues.AsNoTracking().Where(x => x.Id == yeuCau.KhachThueId).Select(x => x.NgheNghiep).FirstOrDefaultAsync(ct);
            ViewData["RequestedEmail"] = await db.TaiKhoans.AsNoTracking().Where(x => x.Id == yeuCau.TaiKhoanKhachId).Select(x => x.Email).FirstOrDefaultAsync(ct);
            ViewData["RequestedRoom"] = await db.PhongTros.AsNoTracking().SingleOrDefaultAsync(x => x.Id == yeuCau.PhongId, ct);
            ViewData["RequestedRoomImage"] = await db.AnhPhongs.AsNoTracking().Where(x => x.PhongId == yeuCau.PhongId).OrderBy(x => x.ThuTu).Select(x => x.DuongDanAnhNho ?? x.DuongDan).FirstOrDefaultAsync(ct);
            ViewData["RequestedListingId"] = await db.YeuCauThues.Where(x => x.Id == id).Select(x => x.TinDangId).SingleAsync(ct);
        }
        return View(view);
    }

    /// <summary>
    /// Compatibility entry point from appointment details to the S3-01 contract form.
    /// Ownership and approved status are checked here and again when saving the contract.
    /// </summary>
    [HttpGet("/LichHen/LapHopDong"), ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> LapHopDong(int id, CancellationToken ct)
    {
        if (await service.DocAsync(id, ct) is not { } yeuCau) return NotFound();
        if (!await service.ChuNhaCuaYeuCauAsync(AccountId, id, ct)) return Forbid();
        if (yeuCau.TrangThai != LichHenTrangThai.DaDuyet)
        {
            TempData["LichHenError"] = "Chỉ yêu cầu đã duyệt mới lập được hợp đồng.";
            return RedirectToAction(nameof(ChiTiet), new { id });
        }
        return RedirectToAction("Create", "HopDong", new { yeuCauId = id });
    }

    /// <summary>
    /// Advisory clash lookup behind the warning on the confirm form. It is deliberately a
    /// separate route rather than a filter on ChiTiet: the warning has to be re-checked every
    /// time the landlord moves the picker, and a failing lookup must never block confirming.
    /// </summary>
    [HttpGet("/LichHen/LichTrung")]
    public async Task<IActionResult> LichTrung(int id, DateTime? lichHen, CancellationToken ct)
    {
        if (await service.DocAsync(id, ct) is not { } yeuCau) return NotFound();
        if (!await service.ChuNhaCuaYeuCauAsync(AccountId, id, ct)) return Forbid();
        if (lichHen is not { } nhap) return Json(Array.Empty<LichHenService.LichTrung>());
        var list = await service.TimLichTrungAsync(yeuCau.PhongId, ChuyenNhapSangUtc(nhap)!.Value, id, ct);
        return Json(list.Select(x => new
        {
            x.YeuCauId, x.MaYeuCau, x.TenKhach, x.TenPhong,
            LichHenHienThoi = LichHenService.HienThoiGio(x.LichHen)
        }));
    }

    /// <summary>Parameter names match the input on the detail form so model binding works.</summary>
    [HttpPost("/LichHen/XacNhanLich"), ValidateAntiForgeryToken, ModuleAccess("YEU_CAU_THUE", write: true)]
    public async Task<IActionResult> XacNhanLich(int id, DateTime? LichHenNhap, CancellationToken ct)
    {
        try
        {
            if (LichHenNhap is not { } nhap) throw new InvalidOperationException("Hãy chọn ngày giờ hẹn.");
            await service.XacNhanLichAsync(id, AccountId, ChuyenNhapSangUtc(nhap)!.Value, ct);
            TempData["LichHenOk"] = "Đã xác nhận lịch hẹn.";
            try
            {
                var recipient = db is null || email is null ? null : await (from request in db.YeuCauThues.AsNoTracking()
                                       join tenant in db.KhachThues.AsNoTracking() on request.KhachThueId equals tenant.Id
                                       join account in db.TaiKhoans.AsNoTracking() on tenant.TaiKhoanId equals account.Id
                                       where request.Id == id && account.DangHoatDong && !account.IsDeleted
                                       select new { account.Email, tenant.HoTen, request.MaYeuCau }).SingleOrDefaultAsync(ct);
                if (recipient is null || email is null) throw new InvalidOperationException("Không có người nhận hoặc dịch vụ email.");
                await email.SendConfirmedAsync(recipient.Email, recipient.HoTen, recipient.MaYeuCau, nhap, id);
            }
            catch (Exception)
            {
                TempData["LichHenWarning"] = "Lịch hẹn đã được xác nhận nhưng chưa gửi được email cho khách. Hãy kiểm tra cấu hình SMTP.";
            }
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or LichHenConflictException)
        {
            TempData["LichHenError"] = ex.Message;
        }
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    /// <summary>
    /// Moves an already booked slot. The browser sends a naive datetime-local value in
    /// Vietnam time, so it is converted to UTC before the service compares it against the
    /// stored slot and the current clock.
    /// </summary>
    [HttpPost("/LichHen/DoiLich"), ValidateAntiForgeryToken, ModuleAccess("YEU_CAU_THUE", write: true)]
    public async Task<IActionResult> DoiLich(int id, DateTime? LichHenNhap, CancellationToken ct)
    {
        try
        {
            if (LichHenNhap is not { } nhap) throw new InvalidOperationException("Hãy chọn ngày giờ hẹn mới.");
            await service.DoiLichAsync(id, AccountId, ChuyenNhapSangUtc(nhap)!.Value, ct);
            TempData["LichHenOk"] = "Đã đổi lịch hẹn.";
            try
            {
                var recipient = db is null || email is null ? null : await (from request in db.YeuCauThues.AsNoTracking()
                    join tenant in db.KhachThues.AsNoTracking() on request.KhachThueId equals tenant.Id
                    join account in db.TaiKhoans.AsNoTracking() on tenant.TaiKhoanId equals account.Id
                    where request.Id == id && account.DangHoatDong && !account.IsDeleted
                    select new { account.Email, tenant.HoTen, request.MaYeuCau }).SingleOrDefaultAsync(ct);
                if (recipient is null || email is null) throw new InvalidOperationException("Không có người nhận hoặc dịch vụ email.");
                await email.SendRescheduledAsync(recipient.Email, recipient.HoTen, recipient.MaYeuCau, nhap, id);
            }
            catch (Exception)
            {
                TempData["LichHenWarning"] = "Lịch hẹn đã được đổi nhưng chưa gửi được email cho khách. Hãy kiểm tra cấu hình SMTP.";
            }
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (LichHenConflictException ex) { TempData["LichHenError"] = ex.Message; }
        catch (InvalidOperationException ex) { TempData["LichHenError"] = ex.Message; }
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost("/LichHen/DuyetThueNgay"), ValidateAntiForgeryToken, ModuleAccess("YEU_CAU_THUE", write: true)]
    public async Task<IActionResult> DuyetThueNgay(int id, CancellationToken ct)
    {
        try
        {
            await service.DuyetThueNgayAsync(id, AccountId, ct);
            TempData["LichHenOk"] = "Đã duyệt yêu cầu thuê ngay.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (LichHenConflictException ex) { TempData["LichHenError"] = ex.Message; }
        catch (InvalidOperationException ex) { TempData["LichHenError"] = ex.Message; }
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost("/LichHen/DuyetSauKhiXem"), ValidateAntiForgeryToken, ModuleAccess("YEU_CAU_THUE", write: true)]
    public async Task<IActionResult> DuyetSauKhiXem(int id, bool daDongYThue, CancellationToken ct)
    {
        try
        {
            if (!daDongYThue) throw new InvalidOperationException("Hãy xác nhận khách đã đồng ý thuê sau khi xem phòng.");
            await service.DuyetSauKhiXemAsync(id, AccountId, ct);
            TempData["LichHenOk"] = "Đã duyệt thuê sau khi xem phòng. Bạn có thể lập hợp đồng.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or LichHenConflictException) { TempData["LichHenError"] = ex.Message; }
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost("/LichHen/TuChoi"), ValidateAntiForgeryToken, ModuleAccess("YEU_CAU_THUE", write: true)]
    public async Task<IActionResult> TuChoi(int id, string? lyDo, string? ghiChu, CancellationToken ct)
    {
        try
        {
            await service.TuChoiAsync(id, AccountId, lyDo ?? "", ghiChu, ct);
            TempData["LichHenOk"] = "Đã từ chối yêu cầu.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or LichHenConflictException)
        {
            TempData["LichHenError"] = ex.Message;
        }
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost("/LichHen/Huy"), ValidateAntiForgeryToken, ModuleAccess("YEU_CAU_THUE", write: true)]
    public async Task<IActionResult> Huy(int id, CancellationToken ct)
    {
        try
        {
            await service.HuyAsync(id, AccountId, ct);
            TempData["LichHenOk"] = "Đã hủy yêu cầu.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or LichHenConflictException)
        {
            TempData["LichHenError"] = ex.Message;
        }
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost("/LichHen/HuyDaDuyet"), Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken,
        ModuleAccess("YEU_CAU_THUE", write: true)]
    public async Task<IActionResult> HuyDaDuyet(int id, HuyYeuCauDaDuyetInput input, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid) throw new InvalidOperationException("Nhập lý do từ 5 đến 500 ký tự và tải lại trang nếu thiếu thông tin phiên bản.");
            var changed = await service.HuyDaDuyetAsync(id, AccountId, input, ct);
            TempData["LichHenOk"] = changed
                ? "Đã hủy yêu cầu do khách không thuê và chuyển phòng về Trống. Tin nguồn đã tự động đăng lại với hạn 30 ngày."
                : "Yêu cầu đã được hủy trước đó. Không có thay đổi mới.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or LichHenConflictException)
        {
            TempData["LichHenError"] = ex.Message;
        }
        catch (DbException)
        {
            TempData["LichHenError"] = "Không thể hủy giữ chỗ do lỗi ghi dữ liệu. Không có thay đổi nào được lưu; vui lòng tải lại trang và thử lại.";
        }
        return RedirectToAction(nameof(ChiTiet), new { id });
    }
}
