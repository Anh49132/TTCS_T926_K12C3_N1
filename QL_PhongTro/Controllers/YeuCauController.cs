using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore; using QL_PhongTro.Authorization; using QL_PhongTro.Data; using QL_PhongTro.Models; using QL_PhongTro.Services; using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;
[Authorize(Roles = "CHU_NHA,QUAN_LY,ADMIN,KHACH_THUE")][ModuleAccess("YEU_CAU_THUE")][ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class YeuCauController(AppDbContext db, ITimeProvider clock, YeuCauThueService requests, LichHenService appointments) : Controller
{
    [HttpGet] public async Task<IActionResult> Index(string? trangThai, int? toaNhaId)
    {
        var accountId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0; if (accountId == 0) return Forbid();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var laKhachThue = role == LichHenTrangThai.VaiTroKhachThue;
        var visibleBuildings = db.ToaNhas.AsNoTracking().Where(x => x.DangHoatDong &&
            (role == "ADMIN" || x.ChuNhaId == accountId || (role == "QUAN_LY" && x.QuanLyId == accountId)));
        var buildings = await visibleBuildings.OrderBy(x => x.TenToaNha).Select(x => new SelectListItem(x.TenToaNha, x.Id.ToString())).ToListAsync();
        var validBuilding = toaNhaId.HasValue && buildings.Any(x => x.Value == toaNhaId.Value.ToString()) ? toaNhaId : null;
        var validStatus = Models.TrangThaiYeuCau.Labels.ContainsKey(trangThai ?? "") ? trangThai : null;
        if (!await requests.IsInstalled())
            return View(new DanhSachYeuCauViewModel { LaKhachThue = laKhachThue, TrangThai = validStatus, ToaNhaId = validBuilding, ToaNhaOptions = buildings });
        if (laKhachThue)
        {
            ViewData["NowUtc"] = clock.UtcNow;
            var tenantRequests = await (from request in db.YeuCauThues.AsNoTracking()
                                        join tenant in db.KhachThues.AsNoTracking() on request.KhachThueId equals tenant.Id
                                        join listing in db.TinDangs.AsNoTracking() on request.TinDangId equals listing.Id
                                        join room in db.PhongTros.AsNoTracking() on listing.PhongId equals room.Id
                                        where tenant.TaiKhoanId == accountId
                                        orderby request.NgayTao descending, request.Id descending
                                        select new
                                        {
                                            request.Id,
                                            request.MaYeuCau,
                                            request.TinDangId,
                                            room.MaPhong,
                                            request.LoaiYeuCau,
                                            TenToaNha = db.ToaNhas.Where(b => b.Id == room.ToaNhaId).Select(b => b.TenToaNha).FirstOrDefault(),
                                            request.NgayTao,
                                            request.TrangThai,
                                            request.LichHen,
                                            request.LyDoTuChoi
                                        }).ToListAsync();

            var tenantItems = tenantRequests.Select(request => new YeuCauThueItemViewModel(
                request.Id,
                request.MaYeuCau,
                request.TinDangId,
                request.MaPhong,
                request.NgayTao,
                request.TrangThai,
                TrangThaiYeuCau.Labels.GetValueOrDefault(request.TrangThai, request.TrangThai),
                request.LichHen,
                request.TrangThai == LichHenTrangThai.TuChoi
                    ? string.IsNullOrWhiteSpace(request.LyDoTuChoi)
                        ? "Không có lý do"
                        : LichHenService.LyDoLabel(request.LyDoTuChoi)
                    : null) { LoaiYeuCau = request.LoaiYeuCau, TenToaNha = request.TenToaNha ?? "" }).ToList();

            return View(new DanhSachYeuCauViewModel
            {
                YeuCausCuaKhach = tenantItems,
                LaKhachThue = true,
                TrangThai = validStatus,
                ToaNhaOptions = buildings
            });
        }
        var buildingIds = buildings.Select(x => int.Parse(x.Value)).ToArray();
        var query = from request in db.YeuCauThues.AsNoTracking()
                    join listing in db.TinDangs.AsNoTracking() on request.TinDangId equals listing.Id
                    join room in db.PhongTros.AsNoTracking() on listing.PhongId equals room.Id
                    join tenant in db.KhachThues.AsNoTracking() on request.KhachThueId equals tenant.Id
                    join account in db.TaiKhoans.AsNoTracking() on tenant.TaiKhoanId equals account.Id into accounts
                    from account in accounts.DefaultIfEmpty()
                    where (laKhachThue ? tenant.TaiKhoanId == accountId : buildingIds.Contains(room.ToaNhaId))
                        && (laKhachThue || !validBuilding.HasValue || room.ToaNhaId == validBuilding)
                    orderby request.NgayTao descending, request.Id descending
                    select new YeuCauListItemViewModel
                    {
                        Id = request.Id,
                        MaYeuCau = request.MaYeuCau,
                        TenKhach = tenant.HoTen,
                        SoDienThoai = tenant.SoDienThoai ?? (account == null ? "" : account.SoDienThoai),
                        Email = account == null ? null : account.Email,
                        NgheNghiep = tenant.NgheNghiep,
                        DienTich = room.DienTich,
                        GiaThue = room.GiaThue,
                        SoNguoiDuKien = request.SoNguoiDuKien,
                        AnhPhong = db.AnhPhongs.Where(photo => photo.PhongId == room.Id).OrderBy(photo => photo.ThuTu).Select(photo => photo.DuongDanAnhNho ?? photo.DuongDan).FirstOrDefault(),
                        MaPhong = room.MaPhong,
                        TenToaNha = db.ToaNhas.Where(b => b.Id == room.ToaNhaId).Select(b => b.TenToaNha).FirstOrDefault() ?? "",
                        LichHen = request.LichHen,
                        DaLapHopDong = db.HopDongs.Any(h => h.YeuCauThueId == request.Id && h.TrangThai != "DA_HUY"),
                        LoaiYeuCau = request.LoaiYeuCau,
                        NgayMongMuon = request.NgayMongMuon,
                        TrangThai = request.TrangThai,
                        LoiNhan = request.LoiNhan,
                        NgayTao = request.NgayTao
                    };
        var items = await query.ToListAsync();
        var nowUtc = clock.UtcNow;
        foreach (var item in items)
        {
            var createdUtc = item.NgayTao.Kind == DateTimeKind.Utc ? item.NgayTao : DateTime.SpecifyKind(item.NgayTao, DateTimeKind.Utc);
            item.QuaHanChuaXuLy = TrangThaiYeuCau.ChuaXuLy(item.TrangThai) && nowUtc - createdUtc >= TimeSpan.FromHours(24);
        }
        return View(new DanhSachYeuCauViewModel { YeuCaus = items.Where(x => validStatus == null || x.TrangThai == validStatus).ToList(), TongHop = items, NowUtc = nowUtc, LaKhachThue = laKhachThue, TrangThai = validStatus, ToaNhaId = validBuilding, ToaNhaOptions = buildings });
    }

    [HttpPost, Authorize(Roles = "CHU_NHA"), ModuleAccess("YEU_CAU_THUE", write: true), ValidateAntiForgeryToken]
    public async Task<IActionResult> XuLyNhanh(int id, string thaoTac, bool sauKhiXem, bool daDongYThue, string? lyDo, string? ghiChu, CancellationToken ct)
    {
        var accountId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : 0;
        if (accountId == 0) return Forbid();
        try
        {
            if (thaoTac == "TU_CHOI") await appointments.TuChoiAsync(id, accountId, lyDo ?? "", ghiChu, ct);
            else if (thaoTac == "DUYET")
            {
                if (sauKhiXem)
                {
                    if (!daDongYThue) throw new InvalidOperationException("Hãy xác nhận khách đã đồng ý thuê sau khi xem phòng.");
                    await appointments.DuyetSauKhiXemAsync(id, accountId, ct);
                }
                else await appointments.DuyetThueNgayAsync(id, accountId, ct);
            }
            else return BadRequest();
            TempData["YeuCauOk"] = thaoTac == "DUYET" ? "Đã duyệt yêu cầu." : "Đã từ chối yêu cầu.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is InvalidOperationException or LichHenConflictException) { TempData["YeuCauError"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("/YeuCau/MoTinDang/{id:int}"), Authorize(Roles = "KHACH_THUE")]
    public async Task<IActionResult> MoTinDang(int id)
    {
        var accountId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var parsedId) ? parsedId : 0;
        if (accountId == 0) return Forbid();

        var listingId = await (from request in db.YeuCauThues.AsNoTracking()
                               join tenant in db.KhachThues.AsNoTracking() on request.KhachThueId equals tenant.Id
                               where request.Id == id && tenant.TaiKhoanId == accountId
                               select (int?)request.TinDangId).SingleOrDefaultAsync();
        if (listingId is null) return NotFound();

        return RedirectToAction("ChiTiet", "TinDang", new { id = listingId.Value });
    }

    [HttpPost("/YeuCau/Huy/{id:int}"), Authorize(Roles = "KHACH_THUE"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Huy(int id, CancellationToken ct)
    {
        var accountId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var parsedId) ? parsedId : 0;
        if (accountId == 0) return Forbid();

        try
        {
            await appointments.HuyAsync(id, accountId, ct);
            TempData["Success"] = "Đã huỷ yêu cầu.";
        }
        catch (Exception ex) when (ex is KeyNotFoundException or UnauthorizedAccessException)
        {
            TempData["Error"] = "Không thể huỷ yêu cầu này. Vui lòng tải lại danh sách.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or LichHenConflictException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
