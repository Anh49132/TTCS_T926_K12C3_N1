using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;

[Authorize(Roles = "KHACH_THUE"), ModuleAccess("TAI_CHINH")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ThongBaoController(
    AppDbContext db,
    QL_PhongTro.Services.HoaDonDichVuService invoices,
    QL_PhongTro.Services.ITimeProvider clock,
    QL_PhongTro.Services.IBaoCaoThanhToanService paymentReports) : Controller
{
    private int Actor => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public IActionResult DanhSach(string? ky, string? trangThai, int page = 1, string? timKiem = null,
        string baoThanhToan = "TAT_CA", int pageSize = 10, string nhom = "TAT_CA", string? tinhTrang = null)
    {
        var status = tinhTrang ?? (string.IsNullOrEmpty(trangThai) ? TrangThaiThanhToanHoaDon.TatCa : trangThai);
        var error = ValidateInvoiceFilters(ky, status, page, out _);
        error ??= ValidateListOptions(timKiem, baoThanhToan, pageSize, nhom);
        if (error is not null) Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(new HoaDonKhachDanhSachPageViewModel(ky, status, page, error, timKiem, baoThanhToan, pageSize, nhom));
    }

    [HttpGet]
    public async Task<IActionResult> DanhSachDuLieu(
        string? ky,
        string? trangThai,
        int page = 1,
        CancellationToken cancellationToken = default,
        string? timKiem = null, string baoThanhToan = "TAT_CA", int pageSize = 10, string nhom = "TAT_CA", string? tinhTrang = null)
    {
        var status = string.IsNullOrEmpty(trangThai) ? TrangThaiThanhToanHoaDon.TatCa : trangThai;
        var error = ValidateInvoiceFilters(ky, status, page, out var selectedMonth);
        error ??= ValidateListOptions(timKiem, baoThanhToan, pageSize, nhom);
        if (tinhTrang is not null) error ??= ValidateInvoiceFilters(null, tinhTrang, 1, out _);
        if (error is not null) return BadRequest(new { message = error });
        if (!await QL_PhongTro.Services.InvoiceNotificationDispatcher.IsInstalledAsync(db, cancellationToken))
            return NotFound(new { message = "Chưa có dữ liệu hóa đơn." });

        var invoices = await (from bill in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs.AsNoTracking() on bill.HopDongId equals contract.Id
            join profile in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals profile.Id
            join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
            where profile.TaiKhoanId == Actor && bill.TrangThai == "DA_PHAT_HANH"
            select new
            {
                bill.Id,
                room.MaPhong,
                bill.MaHoaDon,
                bill.Thang,
                bill.Nam,
                bill.HanThanhToan,
                TongCong = db.ChiTietHoaDons.AsNoTracking().Where(line => line.HoaDonId == bill.Id)
                    .Sum(line => (long?)(line.LoaiKhoan == "GIAM_TRU" ? -line.ThanhTien : line.ThanhTien)) ?? 0L,
                DaThanhToan = db.ThanhToans.AsNoTracking()
                    .Where(payment => payment.HoaDonId == bill.Id && payment.TrangThai == "DA_XAC_NHAN")
                    .Sum(payment => (long?)payment.SoTien) ?? 0L,
                ChoXacNhan = db.ThanhToans.AsNoTracking()
                    .Any(payment => payment.HoaDonId == bill.Id && payment.TrangThai == "CHO_XAC_NHAN"),
                SoTienChoXacNhan = db.ThanhToans.AsNoTracking()
                    .Where(payment => payment.HoaDonId == bill.Id && payment.TrangThai == "CHO_XAC_NHAN")
                    .Sum(payment => (long?)payment.SoTien) ?? 0L
            }).ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        var offsets=await QL_PhongTro.Services.CanTruHoaDonQueries.LayAsync(db,invoices.Select(x=>x.Id));
        var summaries = invoices.Select(invoice =>
        {
            var remaining = Math.Max(0L, checked(invoice.TongCong - invoice.DaThanhToan-offsets.GetValueOrDefault(invoice.Id)));
            var overdue = remaining > 0 && today > invoice.HanThanhToan;
            var paymentStatus = invoice.ChoXacNhan
                ? TrangThaiThanhToanHoaDon.ChoXacNhan
                : remaining == 0
                ? TrangThaiThanhToanHoaDon.DaThanhToan
                : overdue
                    ? TrangThaiThanhToanHoaDon.QuaHan
                    : invoice.DaThanhToan > 0 || offsets.GetValueOrDefault(invoice.Id)>0
                        ? TrangThaiThanhToanHoaDon.ThanhToanMotPhan
                        : TrangThaiThanhToanHoaDon.ChuaThanhToan;
            var lateDays = overdue ? today.DayNumber - invoice.HanThanhToan.DayNumber : 0;
            var paymentStatusLabel = paymentStatus switch
            {
                TrangThaiThanhToanHoaDon.ChoXacNhan => "Chờ xác nhận",
                TrangThaiThanhToanHoaDon.DaThanhToan => "Đã thanh toán",
                TrangThaiThanhToanHoaDon.QuaHan => "Quá hạn",
                TrangThaiThanhToanHoaDon.ThanhToanMotPhan => "Thanh toán một phần",
                _ => "Chưa thanh toán"
            };
            return new
            {
                invoice.Id,
                invoice.MaPhong,
                invoice.DaThanhToan,
                DaCanTru = offsets.GetValueOrDefault(invoice.Id),
                invoice.ChoXacNhan,
                invoice.SoTienChoXacNhan,
                invoice.MaHoaDon,
                invoice.Thang,
                invoice.Nam,
                invoice.HanThanhToan,
                invoice.TongCong,
                SoConPhaiTra = remaining,
                TrangThai = paymentStatus,
                TenTrangThai = paymentStatusLabel,
                QuaHan = overdue && !invoice.ChoXacNhan,
                SoNgayTre = lateDays
            };
        }).ToList();

        var totalCount = summaries.Count;
        var overview = new TongQuanHoaDonKhach(totalCount, summaries.Count(x => x.SoConPhaiTra > 0),
            summaries.Sum(x => x.SoConPhaiTra), summaries.Count(x => x.SoNgayTre > 0), summaries.Count(x => x.SoConPhaiTra == 0));
        if (selectedMonth is { } month)
            summaries = summaries.Where(invoice => invoice.Nam == month.Year && invoice.Thang == month.Month).ToList();
        if (status != TrangThaiThanhToanHoaDon.TatCa)
            summaries = summaries.Where(invoice => invoice.TrangThai == status).ToList();
        if (tinhTrang is not null and not "TAT_CA")
            summaries = summaries.Where(x => tinhTrang switch
            {
                "DA_THANH_TOAN" => x.SoConPhaiTra == 0,
                "THANH_TOAN_MOT_PHAN" => x.SoConPhaiTra > 0 && (x.DaThanhToan > 0 || x.DaCanTru > 0),
                "CHUA_THANH_TOAN" => x.SoConPhaiTra > 0 && x.DaThanhToan == 0 && x.DaCanTru == 0,
                "QUA_HAN" => x.SoNgayTre > 0,
                "CHO_XAC_NHAN" => x.ChoXacNhan,
                _ => false
            }).ToList();
        if (!string.IsNullOrWhiteSpace(timKiem))
            summaries = summaries.Where(x => x.MaHoaDon.Contains(timKiem.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (baoThanhToan != "TAT_CA") summaries = summaries.Where(x => x.ChoXacNhan == (baoThanhToan == "CHO_XAC_NHAN")).ToList();
        if (nhom != "TAT_CA") summaries = summaries.Where(x => (x.SoConPhaiTra > 0) == (nhom == "CAN_THANH_TOAN")).ToList();

        var filteredCount = summaries.Count;
        var pages = Math.Max(1, (filteredCount + pageSize - 1) / pageSize);
        page = Math.Min(page, pages);
        var rows = summaries.OrderByDescending(invoice => invoice.Nam)
            .ThenByDescending(invoice => invoice.Thang)
            .ThenByDescending(invoice => invoice.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(invoice => new HoaDonKhachDanhSachItem(
                invoice.MaHoaDon,
                invoice.Thang,
                invoice.Nam,
                invoice.TongCong,
                invoice.SoConPhaiTra,
                invoice.HanThanhToan.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN")),
                invoice.TrangThai,
                invoice.TenTrangThai,
                invoice.QuaHan,
                invoice.SoNgayTre, invoice.Id, invoice.MaPhong, invoice.DaThanhToan, invoice.DaCanTru,
                invoice.ChoXacNhan, invoice.SoTienChoXacNhan))
            .ToArray();

        return Ok(new HoaDonKhachDanhSachResponse(totalCount, filteredCount, pages, page, rows, overview, pageSize));
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        if (!await QL_PhongTro.Services.InvoiceNotificationDispatcher.IsInstalledAsync(db)) return NotFound();
        page = Math.Max(1, page);
        var published = db.HoaDons.Where(x => x.TrangThai == "DA_PHAT_HANH" || x.TrangThai == "DA_HUY").Select(x => x.Id);
        var query = db.ThongBaoHoaDons.AsNoTracking().Where(x => x.NguoiNhanId == Actor && published.Contains(x.HoaDonId));
        var total = await query.CountAsync();
        var pages = Math.Max(1, (total + 19) / 20); page = Math.Min(page, pages);
        ViewData["Page"] = page; ViewData["Pages"] = pages;
        var rows = await query.OrderByDescending(x => x.NgayTao).ThenByDescending(x => x.Id).Skip((page - 1) * 20).Take(20).ToListAsync();
        var ids = rows.Select(x => x.HoaDonId).ToList();
        ViewData["CancelledIds"] = (await db.HoaDons.Where(x => ids.Contains(x.Id) && x.TrangThai == "DA_HUY").Select(x => x.Id).ToListAsync()).ToHashSet();
        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(int id)
    {
        if (!await QL_PhongTro.Services.InvoiceNotificationDispatcher.IsInstalledAsync(db)) return NotFound();
        if (!await db.ThongBaoHoaDons.AnyAsync(x => x.Id == id && x.NguoiNhanId == Actor)) return Forbid();
        var now = HttpContext.RequestServices.GetRequiredService<QL_PhongTro.Services.ITimeProvider>().UtcNow;
        await db.ThongBaoHoaDons.Where(x => x.Id == id && x.NguoiNhanId == Actor && x.NgayDoc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.NgayDoc, now));
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> HoaDon(int id, CancellationToken cancellationToken)
    {
        if (!await QL_PhongTro.Services.InvoiceNotificationDispatcher.IsInstalledAsync(db, cancellationToken)) return NotFound();
        if (!await (from bill in db.HoaDons join contract in db.HopDongs on bill.HopDongId equals contract.Id
            join profile in db.KhachThues on contract.KhachDungTenId equals profile.Id
            where bill.Id == id && profile.TaiKhoanId == Actor select bill.Id).AnyAsync(cancellationToken)) return Forbid();
        var invoice = await db.HoaDons.AsNoTracking().Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == id && (x.TrangThai == "DA_PHAT_HANH" || x.TrangThai == "DA_HUY"), cancellationToken);
        if (invoice is null) return NotFound();
        var context = await (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id join t in db.ToaNhas on p.ToaNhaId equals t.Id
            where h.Id == invoice.HopDongId select new { p.MaPhong, p.ToaNhaId, t.TenToaNha, h.KhachDungTenId }).SingleAsync(cancellationToken);
        var tenant = await db.KhachThues.Where(x => x.Id == context.KhachDungTenId).Select(x => x.HoTen).SingleOrDefaultAsync(cancellationToken);
        var total = QL_PhongTro.Services.HoaDonDichVuService.TongCong(invoice.ChiTiet);
        var paid = await db.ThanhToans.AsNoTracking()
            .Where(x => x.HoaDonId == invoice.Id && x.TrangThai == "DA_XAC_NHAN")
            .SumAsync(x => (long?)x.SoTien, cancellationToken) ?? 0L;
        var offsets=await QL_PhongTro.Services.CanTruHoaDonQueries.LayAsync(db,[invoice.Id]);
        var offset=offsets.GetValueOrDefault(invoice.Id);
        var remaining = Math.Max(0L, checked(total - paid-offset));
        var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        var overdue = invoice.TrangThai == "DA_PHAT_HANH" && remaining > 0 && today > invoice.HanThanhToan;
        var model = new ChiTietHoaDonViewModel
        {
            HoaDon = invoice,
            KhachXem = true,
            MaPhong = context.MaPhong,
            ToaNhaId = context.ToaNhaId,
            TenToaNha = context.TenToaNha,
            TenKhach = tenant ?? "",
            TongCong = total,
            SoDaThanhToan = paid,SoDaCanTru=offset,
            SoConPhaiTra = remaining,
            HomNay = DateOnly.FromDateTime(clock.UtcNow.AddHours(7)),
            QuaHan = overdue,
            SoNgayTre = overdue ? today.DayNumber - invoice.HanThanhToan.DayNumber : 0,
            BaoCaoChoXacNhan = await paymentReports.LayBaoCaoChoXacNhanAsync(Actor, invoice.Id, cancellationToken)
        };
        await invoices.FillRelationsAsync(model, Actor);
        model.ThanhToanRows = await QL_PhongTro.Services.PaymentWorkflow.HistoryAsync(db, id, cancellationToken);
        return View("~/Views/HoaDonDichVu/Details.cshtml", model);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024),
     RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> BaoCaoThanhToan(int hoaDonId, decimal? soTien, DateOnly? ngayThanhToan,
        string? hinhThucThanhToan, IFormFile? anhChungTu, CancellationToken cancellationToken, string? ghiChu = null)
    {
        try
        {
            await paymentReports.GuiCoGhiChuAsync(Actor, hoaDonId, soTien, ngayThanhToan, hinhThucThanhToan, anhChungTu, ghiChu, cancellationToken);
            TempData["PaymentReportSuccess"] = "Đã gửi báo cáo thanh toán. Hóa đơn đang chờ chủ nhà xác nhận.";
        }
        catch (GhiThuValidationException ex)
        {
            TempData["PaymentReportError"] = ex.Errors.Values.SelectMany(x => x).FirstOrDefault()
                ?? "Thông tin báo cáo thanh toán chưa hợp lệ.";
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(nameof(HoaDon), new { id = hoaDonId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HuyBaoCaoThanhToan(int hoaDonId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await paymentReports.HuyAsync(Actor, hoaDonId, cancellationToken);
            TempData[result.DaHuy ? "PaymentReportSuccess" : "PaymentReportError"] = result.DaHuy
                ? "Đã hủy báo cáo thanh toán. Bạn có thể gửi báo cáo mới."
                : "Báo cáo không còn ở trạng thái chờ xác nhận nên không thể hủy.";
            return RedirectToAction(nameof(HoaDon), new { id = hoaDonId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    public async Task<IActionResult> ChiTiet(string maHoaDon, string? ky = null, string? trangThai = null, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(maHoaDon)) return NotFound();
        var id = await (from bill in db.HoaDons.AsNoTracking() join contract in db.HopDongs on bill.HopDongId equals contract.Id
            join profile in db.KhachThues on contract.KhachDungTenId equals profile.Id
            where bill.MaHoaDon == maHoaDon && profile.TaiKhoanId == Actor
                && (bill.TrangThai == "DA_PHAT_HANH" || bill.TrangThai == "DA_HUY") select (int?)bill.Id).SingleOrDefaultAsync();
        return id.HasValue ? RedirectToAction(nameof(HoaDon), new { id = id.Value }) : Forbid();
    }

    [HttpGet]
    public async Task<IActionResult> ChiTietDuLieu(string maHoaDon, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(maHoaDon)
            || !await QL_PhongTro.Services.InvoiceNotificationDispatcher.IsInstalledAsync(db, cancellationToken))
            return NotFound(new { message = "Không tìm thấy hóa đơn." });

        var invoice = await (from bill in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs.AsNoTracking() on bill.HopDongId equals contract.Id
            join profile in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals profile.Id
            join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
            where bill.MaHoaDon == maHoaDon && profile.TaiKhoanId == Actor
                && (bill.TrangThai == "DA_PHAT_HANH" || bill.TrangThai == "DA_HUY")
            select new { Bill = bill, room.MaPhong }).SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) return NotFound(new { message = "Không tìm thấy hóa đơn." });

        var culture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var lines = await db.ChiTietHoaDons.AsNoTracking().Where(x => x.HoaDonId == invoice.Bill.Id)
            .OrderBy(x => x.SoThuTu).Select(x => new ChiTietHoaDonKhachLineViewModel(
                x.TenKhoan,
                x.ChiSoDau,
                x.ChiSoCuoi,
                x.SoLuong,
                x.DonViTinh,
                x.LoaiKhoan,
                x.DonGia,
                x.ThanhTien))
            .ToListAsync(cancellationToken);

        var total = lines.Aggregate(0L, (sum, line) =>
            checked(sum + (line.LoaiKhoan == "GIAM_TRU" ? -line.ThanhTien : line.ThanhTien)));
        var paid = await db.ThanhToans.AsNoTracking()
            .Where(x => x.HoaDonId == invoice.Bill.Id && x.TrangThai == "DA_XAC_NHAN")
            .SumAsync(x => (long?)x.SoTien, cancellationToken) ?? 0L;
        var offsets=await QL_PhongTro.Services.CanTruHoaDonQueries.LayAsync(db,[invoice.Bill.Id]);
        var offset=offsets.GetValueOrDefault(invoice.Bill.Id);
        var remaining = Math.Max(0L, checked(total - paid-offset));
        var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        var overdue = invoice.Bill.TrangThai == "DA_PHAT_HANH"
            && remaining > 0
            && today > invoice.Bill.HanThanhToan;
        var daysLate = overdue ? today.DayNumber - invoice.Bill.HanThanhToan.DayNumber : 0;
        var pendingReport = await paymentReports.LayBaoCaoChoXacNhanAsync(Actor, invoice.Bill.Id, cancellationToken);

        return Ok(new ChiTietHoaDonKhachViewModel(
            invoice.Bill.MaHoaDon,
            invoice.Bill.Thang,
            invoice.Bill.Nam,
            invoice.MaPhong,
            lines.Select(x => new ChiTietHoaDonKhachLineResponse(
                x.TenKhoan,
                x.ChiSoDau.HasValue && x.ChiSoCuoi.HasValue ? x.ChiSoDau.Value.ToString("0.###", culture) : null,
                x.ChiSoDau.HasValue && x.ChiSoCuoi.HasValue ? x.ChiSoCuoi.Value.ToString("0.###", culture) : null,
                (x.ChiSoDau.HasValue && x.ChiSoCuoi.HasValue
                    ? x.ChiSoCuoi.Value - x.ChiSoDau.Value
                    : x.SoLuong).ToString("0.###", culture),
                x.DonViTinh ?? (x.LoaiKhoan == "TIEN_PHONG" ? "tháng" : ""),
                x.DonGia.ToString("N0", culture),
                x.ThanhTien.ToString("N0", culture),
                x.LoaiKhoan)).ToArray(),
            total,
            paid,
            remaining,
            invoice.Bill.HanThanhToan.ToString("dd/MM/yyyy", culture),
            overdue,
            daysLate,
            invoice.Bill.Id,
            invoice.Bill.TrangThai == "DA_PHAT_HANH" && remaining > 0 && pendingReport is null,
            pendingReport is not null,
            pendingReport?.SoTien,
            pendingReport?.NgayThanhToan.ToString("dd/MM/yyyy"),
            pendingReport?.HinhThucBaoCao,
            today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    private static string? ValidateListOptions(string? search, string report, int size, string group)
    {
        if (search?.Length > 100) return "Từ khóa tối đa 100 ký tự.";
        if (size is not (10 or 20 or 50)) return "Số dòng mỗi trang không hợp lệ.";
        if (report is not ("TAT_CA" or "CHO_XAC_NHAN" or "KHONG_CHO")) return "Bộ lọc báo thanh toán không hợp lệ.";
        if (group is not ("TAT_CA" or "CAN_THANH_TOAN" or "DA_THANH_TOAN")) return "Nhóm hóa đơn không hợp lệ.";
        return null;
    }

    private static string? ValidateInvoiceFilters(
        string? period,
        string status,
        int page,
        out DateOnly? selectedMonth)
    {
        selectedMonth = null;
        if (!string.IsNullOrEmpty(period))
        {
            if (period.Length != 7 || period[4] != '-'
                || !period.Take(4).All(char.IsAsciiDigit)
                || !period.Skip(5).All(char.IsAsciiDigit)
                || !DateOnly.TryParseExact(period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var month))
                return "Kỳ hóa đơn phải đúng định dạng yyyy-MM.";
            selectedMonth = month;
        }

        if (status is not (TrangThaiThanhToanHoaDon.TatCa
            or TrangThaiThanhToanHoaDon.ChuaThanhToan
            or TrangThaiThanhToanHoaDon.ThanhToanMotPhan
            or TrangThaiThanhToanHoaDon.DaThanhToan
            or TrangThaiThanhToanHoaDon.QuaHan
            or TrangThaiThanhToanHoaDon.ChoXacNhan))
            return "Trạng thái thanh toán không hợp lệ.";

        if (page < 1) return "Số trang phải lớn hơn 0.";
        return null;
    }
}
