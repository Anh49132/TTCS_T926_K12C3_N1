using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA,ADMIN"), ModuleAccess("TAI_CHINH")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public partial class HoaDonDichVuController(AppDbContext db, DichVuService services, HoaDonDichVuService invoices) : Controller
{
    private int AccountId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private ITimeProvider Clock => HttpContext.RequestServices.GetRequiredService<ITimeProvider>();
    private IDataProtector ReviewProtector => HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("MonthlyInvoices.Review.v1");
    private sealed record ReviewSnapshot(int Actor, int Building, int Year, int Month, DateOnly Issue, DateOnly Due,
        DateTime ExpiresUtc, Dictionary<int, string> Candidates);
    private string ReviewToken(PhatHanhThangViewModel model) => ReviewProtector.Protect(JsonSerializer.Serialize(new ReviewSnapshot(
        AccountId, model.ToaNhaId, model.Nam, model.Thang, model.NgayPhatHanh, model.HanThanhToan, Clock.UtcNow.AddMinutes(30),
        model.DuKien.ToDictionary(x => x.HoaDon.HopDongId, HoaDonDichVuService.ReviewFingerprint))));

    [HttpGet, Authorize(Roles = "CHU_NHA")]
    public async Task<IActionResult> Monthly(int? toaNhaId, int? nam, int? thang, DateOnly? ngayPhatHanh, DateOnly? hanThanhToan)
    {
        var buildings = await db.ToaNhas.AsNoTracking().Where(x => x.ChuNhaId == AccountId && x.DangHoatDong)
            .OrderBy(x => x.TenToaNha).Select(x => new SelectListItem(x.TenToaNha, x.Id.ToString())).ToListAsync();
        var today = DateOnly.FromDateTime(HttpContext.RequestServices.GetRequiredService<ITimeProvider>().UtcNow.AddHours(7));
        var selected = toaNhaId ?? (buildings.Count > 0 ? int.Parse(buildings[0].Value) : 0);
        var model = new PhatHanhThangViewModel { ToaNhaId = selected, Nam = nam ?? today.Year, Thang = thang ?? today.Month,
            NgayPhatHanh = ngayPhatHanh ?? today, HanThanhToan = hanThanhToan ?? today.AddDays(7) };
        if (selected != 0)
        {
            try { model = await invoices.XemThangAsync(AccountId, selected, model.Nam, model.Thang, ngayPhatHanh, hanThanhToan); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        }
        model.ToaNhas = buildings;
        foreach (var item in model.ToaNhas) item.Selected = item.Value == model.ToaNhaId.ToString();
        if (model.SanSang && ModelState.IsValid) model.ReviewToken = ReviewToken(model);
        if (TempData["MonthlyResults"] is string results) model.KetQua = JsonSerializer.Deserialize<List<KetQuaPhongHoaDon>>(results) ?? [];
        return View(model);
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> IssueMonthly(int toaNhaId, int nam, int thang, int? hopDongId,
        DateOnly? ngayPhatHanh, DateOnly? hanThanhToan, string? reviewToken, bool xacNhan, List<int>? selectedIds)
    {
        if (!ModelState.IsValid) return BadRequest();
        try
        {
            if (!await services.SoHuuToaNhaAsync(AccountId, toaNhaId)) return Forbid();
            if (!xacNhan) throw new InvalidOperationException("Hãy xác nhận danh sách và ngày thanh toán trước khi phát hành.");
            ReviewSnapshot? review;
            try { review = JsonSerializer.Deserialize<ReviewSnapshot>(ReviewProtector.Unprotect(reviewToken ?? "")); }
            catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
            { throw new InvalidOperationException("Bản kiểm tra không hợp lệ. Hãy tải lại trước khi phát hành."); }
            if (review is null || review.Actor != AccountId || review.Building != toaNhaId || review.Year != nam || review.Month != thang
                || review.ExpiresUtc < Clock.UtcNow || (ngayPhatHanh.HasValue && ngayPhatHanh != review.Issue)
                || (hanThanhToan.HasValue && hanThanhToan != review.Due))
                throw new InvalidOperationException("Kỳ hoặc ngày thanh toán đã thay đổi. Hãy kiểm tra lại trước khi phát hành.");
            ngayPhatHanh = review.Issue; hanThanhToan = review.Due;
            var chosen = hopDongId.HasValue ? new List<int> { hopDongId.Value } : selectedIds ?? [];
            if (chosen.Count == 0 && review.Candidates.Count > 0) throw new InvalidOperationException("Hãy chọn ít nhất một phòng để phát hành.");
            if (chosen.Any(id => !review.Candidates.ContainsKey(id))) return Forbid();
            var result = await invoices.PhatHanhDanhSachAsync(AccountId, toaNhaId, nam, thang, chosen, ngayPhatHanh, hanThanhToan, review.Candidates, taoNhap: true);
            // Keep reports server-side: a 50-room report must not fill the authentication/request cookies.
            HttpContext.RequestServices.GetRequiredService<IMemoryCache>().Set("InvoiceRun:" + result.MaLanChay, result, TimeSpan.FromHours(24));
            return RedirectToAction(nameof(Results), new { runId = result.MaLanChay });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["MonthlyError"] = ex.Message; }
        catch (OverflowException) { TempData["MonthlyError"] = "Số tiền vượt giới hạn. Hãy kiểm tra chỉ số và đơn giá."; }
        catch (DbUpdateException) { TempData["MonthlyError"] = "Dữ liệu đã thay đổi. Hãy kiểm tra lại kỳ hóa đơn."; }
        catch (Microsoft.Data.Sqlite.SqliteException) { TempData["MonthlyError"] = "Chưa thể phát hành hóa đơn. Hãy kiểm tra thiết lập hoặc thử lại."; }
        return RedirectToAction(nameof(Monthly), new { toaNhaId, nam, thang, ngayPhatHanh = ngayPhatHanh?.ToString("yyyy-MM-dd"), hanThanhToan = hanThanhToan?.ToString("yyyy-MM-dd") });
    }

    private KetQuaPhatHanhThang? FindRun(string? runId) => runId is not null && Guid.TryParseExact(runId, "N", out _)
        ? HttpContext.RequestServices.GetRequiredService<IMemoryCache>().Get<KetQuaPhatHanhThang>("InvoiceRun:" + runId) : null;

    [HttpGet, Authorize(Roles = "CHU_NHA")]
    public async Task<IActionResult> Results(string? runId)
    {
        var run = FindRun(runId);
        if (run is null) return NotFound("Báo cáo lần chạy đã hết hạn. Hãy mở lại kỳ hóa đơn để xem dữ liệu hiện tại.");
        if (run.NguoiThucHienId != AccountId || !await services.SoHuuToaNhaAsync(AccountId, run.ToaNhaId)) return Forbid();
        return View(run);
    }

    [HttpGet, Authorize(Roles = "CHU_NHA")]
    public async Task<IActionResult> ExportResults(string? runId)
    {
        var run = FindRun(runId);
        if (run is null) return NotFound();
        if (run.NguoiThucHienId != AccountId || !await services.SoHuuToaNhaAsync(AccountId, run.ToaNhaId)) return Forbid();
        static string Cell(string value) => "\"" + ((value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') ? "'" : "") + value.Replace("\"", "\"\"") + "\"";
        var lines = new List<string> { "Phòng,Khách thuê,Kết quả,Mã hóa đơn,Tổng tiền mới,Ngày phát hành,Hạn thanh toán,Lý do" };
        lines.AddRange(run.Phongs.Select(x => string.Join(",", new[] { x.MaPhong, x.TenKhach,
            x.TrangThai == "NHAP" ? "Nháp" : x.TrangThai == "DA_PHAT_HANH" ? "Đã phát hành" : x.TrangThai == "DA_CO_HOA_DON" ? "Đã có hóa đơn" : x.ThieuChiSo ? "Thiếu chỉ số" : "Bị bỏ qua",
            x.MaHoaDon, x.TrangThai is "NHAP" or "DA_PHAT_HANH" ? x.TongTien.ToString(System.Globalization.CultureInfo.InvariantCulture) : "",
            run.NgayPhatHanh.ToString("dd/MM/yyyy"), run.HanThanhToan.ToString("dd/MM/yyyy"), x.LyDo }.Select(Cell))));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join("\r\n", lines))).ToArray(), "text/csv; charset=utf-8", $"ket-qua-hoa-don-{run.Nam}{run.Thang:00}-{run.MaLanChay[..8]}.csv");
    }

    private async Task<ChiTietHoaDonViewModel> InvoiceViewAsync(QL_PhongTro.Models.HoaDon invoice, bool preview, string reviewToken = "")
    {
        var context = await (from h in db.HopDongs.AsNoTracking()
            join p in db.PhongTros on h.PhongId equals p.Id
            join t in db.ToaNhas on p.ToaNhaId equals t.Id
            where h.Id == invoice.HopDongId
            select new { p.MaPhong, p.ToaNhaId, t.TenToaNha, h.KhachDungTenId }).SingleAsync();
        var tenant = await db.KhachThues.AsNoTracking().Where(x => x.Id == context.KhachDungTenId).Select(x => x.HoTen).SingleOrDefaultAsync();
        return new() { HoaDon = invoice, XemTruoc = preview, ReviewToken = reviewToken, ToaNhaId = context.ToaNhaId,
            MaPhong = context.MaPhong, TenToaNha = context.TenToaNha, TenKhach = tenant ?? "Chưa có thông tin" };
    }

    [HttpGet, Authorize(Roles = "CHU_NHA")]
    public async Task<IActionResult> Preview(int toaNhaId, int hopDongId, int nam, int thang, DateOnly? ngayPhatHanh, DateOnly? hanThanhToan)
    {
        if (!ModelState.IsValid) return BadRequest();
        var belongs = await (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id
            where h.Id == hopDongId && p.ToaNhaId == toaNhaId select h.Id).AnyAsync();
        if (!belongs || !await services.SoHuuToaNhaAsync(AccountId, toaNhaId)) return Forbid();
        try
        {
            var preview = await invoices.XemThangAsync(AccountId, toaNhaId, nam, thang, ngayPhatHanh, hanThanhToan);
            var row = preview.DuKien.SingleOrDefault(x => x.HoaDon.HopDongId == hopDongId);
            if (row is null)
            {
                TempData["MonthlyError"] = "Phòng chưa đủ dữ liệu hoặc đã có hóa đơn trong kỳ. Hãy kiểm tra lại danh sách.";
                return RedirectToAction(nameof(Monthly), new { toaNhaId, nam, thang });
            }
            preview.DuKien = [row];
            return View("Details", await InvoiceViewAsync(row.HoaDon, true, ReviewToken(preview)));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["MonthlyError"] = ex.Message; return RedirectToAction(nameof(Monthly), new { toaNhaId, nam, thang }); }
    }

    private async Task FillAsync(LapHoaDonDichVuViewModel model)
    {
        model.SanSang = await invoices.SanSangAsync();
        if (!model.SanSang) return;
        model.HopDongs = await (from h in db.HopDongs
                                join p in db.PhongTros on h.PhongId equals p.Id
                                where p.ToaNhaId == model.ToaNhaId && h.TrangThai == "DANG_HIEU_LUC"
                                orderby p.MaPhong
                                select new SelectListItem(h.MaHopDong + " · " + p.MaPhong, h.Id.ToString())).ToListAsync();
        var date = model.NgayApDung ?? DichVuService.HomNay();
        model.DonGias.Clear();
        model.HopDongId ??= model.HopDongs.Count > 0 ? int.Parse(model.HopDongs[0].Value) : null;
        var roomId = await (from h in db.HopDongs
                            join p in db.PhongTros on h.PhongId equals p.Id
                            where h.Id == model.HopDongId && p.ToaNhaId == model.ToaNhaId && h.TrangThai == "DANG_HIEU_LUC"
                            select (int?)p.Id).SingleOrDefaultAsync();
        if (roomId.HasValue && model.HopDongId.HasValue)
        {
            try
            {
                var count = await invoices.LaySoNguoiAsync(AccountId, model.HopDongId.Value, model.ToaNhaId, date);
                model.SoNguoi = count.SoNguoi; model.NgayChotSoNguoi = count.NgayChot; date = count.NgayChot;
                model.PhienBanPhong ??= count.PhienBanPhong;
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); model.SoNguoi = 0; }
        }
        var contractServices = await db.HopDongDichVus.Where(x => x.HopDongId == model.HopDongId).Select(x => x.DichVuId).ToListAsync();
        foreach (var id in (await services.DanhSachAsync(AccountId, model.ToaNhaId)).Select(x => x.DichVuId).Distinct()
            .Where(id => contractServices.Count == 0 || contractServices.Contains(id)))
        {
            var price = roomId.HasValue ? await new DichVuPhongService(db, services).LayGiaHoaDonAsync(AccountId, roomId.Value, id, date) : null;
            if (price is not null) model.DonGias.Add(price);
        }
        model.DaPhatHanh = await (from hd in db.HoaDons
                                  join h in db.HopDongs on hd.HopDongId equals h.Id
                                  join p in db.PhongTros on h.PhongId equals p.Id
                                  join t in db.ToaNhas on p.ToaNhaId equals t.Id
                                  where p.ToaNhaId == model.ToaNhaId && (t.ChuNhaId == AccountId || User.IsInRole("ADMIN"))
                                  orderby hd.Id descending
                                  select new HoaDonGanDayViewModel
                                  {
                                      Id = hd.Id,
                                      MaHoaDon = hd.MaHoaDon,
                                      MaPhong = p.MaPhong,
                                      TenToaNha = t.TenToaNha,
                                      Thang = hd.Thang,
                                      Nam = hd.Nam,
                                      NgayChot = hd.NgayChot,
                                      TongTien = hd.TongTien
                                  }).Take(30).ToListAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Index(int toaNhaId, DateOnly? ngayApDung, int? hopDongId)
    {
        if (!await services.SoHuuToaNhaAsync(AccountId, toaNhaId)) return Forbid();
        var model = new LapHoaDonDichVuViewModel { ToaNhaId = toaNhaId, HopDongId = hopDongId, NgayApDung = ngayApDung ?? DichVuService.HomNay() };
        await FillAsync(model);
        model.Dong = model.DonGias.Select(x => new DongDichVuInput { Chon = true, DichVuId = x.DichVuId, CauHinhId = x.CauHinhId, DonGiaDaXem = x.DonGia }).ToList();
        return View(model);
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> Issue(LapHoaDonDichVuViewModel model)
    {
        ModelState.Remove(nameof(model.SoNguoi));
        if (!await services.SoHuuToaNhaAsync(AccountId, model.ToaNhaId)) return Forbid();
        try
        {
            if (ModelState.IsValid)
            {
                var id = await invoices.PhatHanhAsync(AccountId, model, taoNhap: true);
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        catch (ValidationException ex) { ModelState.AddModelError("", ex.Message); }
        catch (OverflowException) { ModelState.AddModelError("", "Số tiền vượt giới hạn cho phép. Hãy kiểm tra đơn giá và chỉ số."); }
        catch (DbUpdateException) { ModelState.AddModelError("", "Chưa phát hành được hóa đơn: dữ liệu vừa thay đổi hoặc đã có hóa đơn cho kỳ này. Hãy tải lại."); }
        catch (Microsoft.Data.Sqlite.SqliteException) { ModelState.AddModelError("", "Dữ liệu đang được cập nhật. Hãy tải lại bản xem trước và thử phát hành lại."); }
        // Display refreshed prices but require a new GET before a stale price submission can be accepted.
        await FillAsync(model);
        return View("Index", model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var context = await (from hd in db.HoaDons
                             join h in db.HopDongs on hd.HopDongId equals h.Id
                             join p in db.PhongTros on h.PhongId equals p.Id
                             where hd.Id == id
                             select new { p.ToaNhaId }).SingleOrDefaultAsync();
        if (context is null) return NotFound();
        if (!await services.SoHuuToaNhaAsync(AccountId, context.ToaNhaId)) return Forbid();
        ViewData["ToaNhaId"] = context.ToaNhaId;
        var model = await InvoiceViewAsync(await db.HoaDons.AsNoTracking().Include(x => x.ChiTiet).SingleAsync(x => x.Id == id), false);
        await invoices.FillRelationsAsync(model);
        model.LichSuNhap = User.IsInRole("CHU_NHA") ? await invoices.LichSuNhapAsync(AccountId, id) : [];
        var collection = HttpContext.RequestServices.GetRequiredService<GhiThuHoaDonService>();
        model.ThanhToan = await collection.ChiTietAsync(AccountId, id);
        model.BaoCaoChoXacNhan = await db.ThanhToans.AsNoTracking()
            .Where(x => x.HoaDonId == id && x.TrangThai == "CHO_XAC_NHAN")
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        model.HomNay = collection.HomNay;
        model.TenNguoiGhiThu = await db.TaiKhoans.Where(x => x.Id == AccountId).Select(x => x.HoTen).SingleAsync();
        if (model.HoaDon.TrangThai == "DA_PHAT_HANH") model.LyDoChanHuy = await invoices.LyDoChanHuyAsync(id);
        return View(model);
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> CreateDraft(int toaNhaId, int hopDongId, int nam, int thang, DateOnly? ngayPhatHanh, DateOnly? hanThanhToan)
    {
        if (!ModelState.IsValid) return BadRequest();
        try { return RedirectToAction(nameof(Details), new { id = await invoices.TaoNhapThangAsync(AccountId, toaNhaId, hopDongId, nam, thang, ngayPhatHanh, hanThanhToan) }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException or DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        { TempData["MonthlyError"] = "Chưa tạo được bản nháp. Hãy kiểm tra dữ liệu và tải lại kỳ hóa đơn."; }
        return RedirectToAction(nameof(Monthly), new { toaNhaId, nam, thang });
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> PublishDraft(PhatHanhNhapViewModel model)
    {
        if (!ModelState.IsValid) { TempData["DraftError"] = "Ngày phát hành hoặc hạn thanh toán không hợp lệ."; return RedirectToAction(nameof(Details), new { id = model.Id }); }
        try { await invoices.PhatHanhNhapAsync(AccountId, model); TempData["DraftSuccess"] = "Đã phát hành hóa đơn và tạo thông báo cho khách. Nội dung hóa đơn đã được khóa."; }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["DraftError"] = ex.Message; }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException or OverflowException)
        { TempData["DraftError"] = "Phát hành không thành công. Hãy tải lại hóa đơn và kiểm tra dữ liệu; chưa tạo thông báo mới."; }
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> PublishMonthlyDrafts(int toaNhaId, int nam, int thang,
        List<int>? selectedInvoiceIds, Dictionary<int, int>? versions, DateOnly ngayPhatHanh,
        DateOnly hanThanhToan, bool xacNhan)
    {
        if (!ModelState.IsValid || selectedInvoiceIds is null || versions is null) return BadRequest();
        if (selectedInvoiceIds.Count is < 1 or > 50 || selectedInvoiceIds.Count != selectedInvoiceIds.Distinct().Count() ||
            selectedInvoiceIds.Any(id => !versions.ContainsKey(id))) return BadRequest();
        try
        {
            var start = Clock.UtcNow;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var rows = await (from bill in db.HoaDons.AsNoTracking()
                join c in db.HopDongs on bill.HopDongId equals c.Id
                join r in db.PhongTros on c.PhongId equals r.Id
                join b in db.ToaNhas on r.ToaNhaId equals b.Id
                join tenant in db.KhachThues on c.KhachDungTenId equals tenant.Id
                where selectedInvoiceIds.Contains(bill.Id) && b.ChuNhaId == AccountId && b.Id == toaNhaId && bill.Nam == nam && bill.Thang == thang
                select new { Bill = bill, r.MaPhong, tenant.HoTen, b.TenToaNha }).ToListAsync();
            if (rows.Count != selectedInvoiceIds.Count) return Forbid();
            string? failure = null;
            var published = new List<int>();
            try { await invoices.PhatHanhNhieuNhapAsync(AccountId, toaNhaId, nam, thang,
                selectedInvoiceIds.ToDictionary(id => id, id => versions[id]), ngayPhatHanh, hanThanhToan, xacNhan, published); }
            catch (InvalidOperationException ex) { failure = ex.Message; }
            catch (Exception ex) when(ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException or OverflowException)
            { failure = "Toàn bộ đợt phát hành đã được hoàn tác. Hãy kiểm tra lại dữ liệu."; }
            watch.Stop();
            var run = new KetQuaPhatHanhThang(rows.Select(r => new KetQuaPhongHoaDon(r.Bill.HopDongId,r.MaPhong,
                published.Contains(r.Bill.Id) ? "DA_PHAT_HANH" : failure != null && r.Bill.TrangThai != "DA_PHAT_HANH" ? "THAT_BAI" : "DA_CO_HOA_DON",
                published.Contains(r.Bill.Id) ? "Đã tạo thông báo cho khách." : failure ?? "Đã phát hành trước đó; giữ nguyên.",r.Bill.Id)
                { MaHoaDon=r.Bill.MaHoaDon,TenKhach=r.HoTen,TongTien=r.Bill.TongTien }).ToList())
                { LaPhatHanh=true,NguoiThucHienId=AccountId,NguoiThucHien=User.Identity?.Name ?? "",
                  ToaNhaId=toaNhaId,TenToaNha=rows[0].TenToaNha,Nam=nam,Thang=thang,NgayPhatHanh=ngayPhatHanh,
                  HanThanhToan=hanThanhToan,BatDauUtc=start,HoanTatUtc=Clock.UtcNow,SoGiay=watch.Elapsed.TotalSeconds };
            HttpContext.RequestServices.GetRequiredService<IMemoryCache>().Set("InvoiceRun:"+run.MaLanChay,run,TimeSpan.FromHours(24));
            return RedirectToAction(nameof(Results),new {runId=run.MaLanChay});
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["MonthlyError"] = "Chưa phát hành hóa đơn nào trong lần này. " + ex.Message; }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException or OverflowException)
        { TempData["MonthlyError"] = "Phát hành không thành công; toàn bộ thay đổi đã được hoàn tác. Hãy kiểm tra lại danh sách."; }
        return RedirectToAction(nameof(Monthly), new { toaNhaId, nam, thang,
            ngayPhatHanh = ngayPhatHanh.ToString("yyyy-MM-dd"), hanThanhToan = hanThanhToan.ToString("yyyy-MM-dd") });
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write: true)]
    public async Task<IActionResult> SaveDraft(SuaHoaDonNhapViewModel model)
    {
        if (!ModelState.IsValid) { TempData["DraftError"] = "Số tiền/chỉ số không hợp lệ. Hãy kiểm tra định dạng nhập."; return RedirectToAction(nameof(Details), new { id = model.Id }); }
        try { await invoices.LuuNhapAsync(AccountId, model); TempData["DraftSuccess"] = "Đã lưu bản nháp và tính lại tổng tiền. Chưa gửi thông báo cho khách."; }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["DraftError"] = ex.Message; }
        catch (Exception ex) when (ex is OverflowException or DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        { TempData["DraftError"] = "Chưa lưu được hóa đơn. Số tiền vượt giới hạn hoặc dữ liệu đang thay đổi; hãy tải lại."; }
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }
}
