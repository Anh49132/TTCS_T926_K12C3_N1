using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class TinDangController(AppDbContext db, YeuCauThueService requests, DichVuService services, DichVuPhongService roomServices) : Controller
{
    [Authorize(Roles = "CHU_NHA"), ModuleAccess("PHONG_TRO", write: true), HttpGet]
    public async Task<IActionResult> Tao(int phongId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
            return Forbid();

        var model = await LoadDraftFormAsync(phongId, ownerId);
        if (model is null)
            return NotFound();
        if (model.TrangThaiPhong != "TRONG")
        {
            TempData["RoomWarning"] = "Phòng đang được thuê. Chỉ có thể tạo tin từ phòng trống.";
            return RedirectToAction("Index", "PhongTro", new { toaNhaId = model.ToaNhaId });
        }

        return View(model);
    }

    [Authorize(Roles = "CHU_NHA"), ModuleAccess("PHONG_TRO", write: true), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Tao(TaoTinDangViewModel form)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
            return Forbid();

        var model = await LoadDraftFormAsync(form.PhongId, ownerId);
        if (model is null)
            return NotFound();
        if (model.TrangThaiPhong != "TRONG")
        {
            TempData["RoomWarning"] = "Phòng đang được thuê. Chỉ có thể tạo tin từ phòng trống.";
            return RedirectToAction("Index", "PhongTro", new { toaNhaId = model.ToaNhaId });
        }

        if (!ModelState.IsValid)
        {
            model.TieuDe = form.TieuDe;
            model.MoTaThem = form.MoTaThem;
            return View(model);
        }

        var now = DateTime.UtcNow;
        db.TinDangs.Add(new TinDang
        {
            PhongId = form.PhongId,
            NguoiDangId = ownerId,
            TieuDe = form.TieuDe.Trim(),
            NoiDung = string.IsNullOrWhiteSpace(form.MoTaThem) ? null : form.MoTaThem.Trim(),
            NgayHetHan = now.AddDays(30),
            TrangThai = "NHAP",
            NgayTao = now
        });
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã lưu tin đăng ở trạng thái nháp.";
        return RedirectToAction("Index", "PhongTro", new { toaNhaId = model.ToaNhaId });
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await requests.IsInstalled()) return View("ChuaCaiDat");
        return View(await requests.PublicListings().OrderByDescending(t => t.NgayDang).Take(100).ToListAsync());
    }

    [Authorize(Roles = "CHU_NHA"), ModuleAccess("PHONG_TRO", write: true), HttpGet]
    public async Task<IActionResult> QuanLy()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
            return Forbid();

        var now = DateTime.UtcNow;
        var rows = await (
            from listing in db.TinDangs.AsNoTracking()
            join room in db.PhongTros.AsNoTracking() on listing.PhongId equals room.Id
            join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
            where building.ChuNhaId == ownerId
            orderby listing.NgayTao descending
            select new
            {
                Id = listing.Id,
                TieuDe = listing.TieuDe,
                MaPhong = room.MaPhong,
                TrangThai = listing.TrangThai,
                NgayHetHan = listing.NgayHetHan
            }).ToListAsync();
        var listings = rows.Select(listing => new TinDangQuanLyItemViewModel
        {
            Id = listing.Id,
            TieuDe = listing.TieuDe,
            MaPhong = listing.MaPhong,
            TrangThai = listing.TrangThai,
            DaTuDongAnDoQuaHan = listing.TrangThai == "TAM_AN"
                && listing.NgayHetHan is { } expiry
                && expiry < now
        }).ToArray();

        return View(new DanhSachTinDangQuanLyViewModel { TinDangs = listings });
    }

    [Authorize(Roles = "CHU_NHA"), ModuleAccess("PHONG_TRO", write: true), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatTrangThai(int id, string trangThai)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
            return Forbid();

        if (trangThai is not ("NHAP" or "DANG_HIEN_THI" or "TAM_AN" or "DA_CHO_THUE"))
        {
            TempData["Error"] = "Trạng thái tin đăng không hợp lệ.";
            return RedirectToAction(nameof(QuanLy));
        }

        var listing = await (
            from post in db.TinDangs
            join room in db.PhongTros on post.PhongId equals room.Id
            join building in db.ToaNhas on room.ToaNhaId equals building.Id
            where post.Id == id && building.ChuNhaId == ownerId
            select post).SingleOrDefaultAsync();

        if (listing is null)
            return NotFound();

        if (trangThai == "DANG_HIEN_THI" &&
            await db.TinDangs.AnyAsync(other => other.PhongId == listing.PhongId
                && other.Id != listing.Id && other.TrangThai == "DANG_HIEN_THI"))
        {
            TempData["Error"] = "Không thể hiển thị tin này vì phòng đã có một tin khác đang hiển thị. Hãy tạm ẩn tin hiện tại trước.";
            return RedirectToAction(nameof(QuanLy));
        }

        var changed = listing.TrangThai != trangThai;
        listing.TrangThai = trangThai;
        if (changed && trangThai == "DANG_HIEN_THI")
            listing.NgayDang = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            TempData["Error"] = "Không thể hiển thị tin này vì phòng đã có một tin khác đang hiển thị. Hãy tạm ẩn tin hiện tại trước.";
            return RedirectToAction(nameof(QuanLy));
        }

        TempData["Success"] = $"Đã cập nhật trạng thái tin đăng thành “{TrangThaiLabel(trangThai)}”.";
        return RedirectToAction(nameof(QuanLy));
    }

    private static string TrangThaiLabel(string trangThai) => trangThai switch
    {
        "NHAP" => "Nháp",
        "DANG_HIEN_THI" => "Đang hiển thị",
        "TAM_AN" => "Tạm ẩn",
        "DA_CHO_THUE" => "Đã cho thuê",
        _ => trangThai
    };

    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ChiTiet(int id)
    {
        var model = await Detail(id, new());
        return model is null ? NotFound() : View(model);
    }

    [Authorize(Roles = "KHACH_THUE"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GuiYeuCau(int id, [Bind(Prefix = "Form")] GuiYeuCauViewModel form)
    {
        if (!await requests.IsInstalled()) return View("ChuaCaiDat");
        var model = await Detail(id, form);
        if (model is null) return NotFound();
        if (form.NgayMongMuon is not null && requests.ValidateDesiredDate(form.NgayMongMuon) is { } dateError)
            ModelState.AddModelError("Form.NgayMongMuon", dateError);
        if (!ModelState.IsValid) return View("ChiTiet", model);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return Forbid();
        try
        {
            var request = await requests.Send(id, accountId, form);
            return request is null ? NotFound() : RedirectToAction(nameof(ThanhCong), new { id = request.Id });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (OpenRequestExistsException error)
        {
            ModelState.AddModelError("", error.Message);
            return View("ChiTiet", model with { OpenRequestId = error.RequestId });
        }
        catch (RoomCapacityException error)
        {
            ModelState.AddModelError("Form.SoNguoiDuKien", error.Message);
            // Reflect the capacity read inside the transaction if it changed after Detail.
            model.Phong.SoNguoiToiDa = error.Maximum;
            return View("ChiTiet", model);
        }
        catch (DesiredDateException error)
        {
            ModelState.AddModelError("Form.NgayMongMuon", error.Message);
            return View("ChiTiet", model with { Today = requests.Today });
        }
        catch (RequestCodeExhaustedException error)
        {
            ModelState.AddModelError("", error.Message);
            return View("ChiTiet", model);
        }
    }

    [Authorize(Roles = "KHACH_THUE"), HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ThanhCong(int id)
    {
        if (!await requests.IsInstalled()) return NotFound();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return Forbid();
        var request = await db.YeuCauThues.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id
            && db.KhachThues.Any(k => k.Id == r.KhachThueId && k.TaiKhoanId == accountId));
        return request is null ? NotFound() : View(request);
    }

    private async Task<ChiTietTinDangViewModel?> Detail(int id, GuiYeuCauViewModel form)
    {
        var publicDetail = await GetPublicListingAsync(id);
        if (publicDetail is null) return null;
        var tin = await db.TinDangs.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id);
        if (tin is null) return null;
        var room = await db.PhongTros.AsNoTracking().SingleAsync(p => p.Id == tin.PhongId);
        var installed = await requests.IsInstalled();
        var existing = installed && User.IsInRole("KHACH_THUE")
            && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
            ? await requests.FindOpenRequest(id, accountId) : null;
        return new(tin, room, form, requests.Today) { OpenRequestId = existing?.Id, PublicDetail = publicDetail, RequestModuleInstalled = installed };
    }

    [Authorize(Roles = "KHACH_THUE"), HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> YeuCau(int id)
    {
        if (!await requests.IsInstalled()) return NotFound();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return Forbid();
        var request = await db.YeuCauThues.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id
            && db.KhachThues.Any(k => k.Id == r.KhachThueId && k.TaiKhoanId == accountId));
        return request is null ? NotFound() : View(request);
    }
    [HttpGet("/api/tin-dang/{id:int}")]
    [Produces("application/json")]
    public async Task<IActionResult> ChiTietApi(int id)
    {
        var listing = await GetPublicListingAsync(id);
        return listing is null ? NotFound() : Ok(listing);
    }

    private async Task<TinDangChiTietViewModel?> GetPublicListingAsync(int id)
    {
        var now = DateTime.UtcNow;
        var result = await (
            from post in db.TinDangs.AsNoTracking()
            join room in db.PhongTros.AsNoTracking() on post.PhongId equals room.Id
            join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
            where post.Id == id
                && post.TrangThai == "DANG_HIEN_THI"
                && (post.NgayHetHan == null || post.NgayHetHan > now)
                && room.TrangThai == "TRONG"
                && building.DangHoatDong
            select new
            {
                RoomId = room.Id,
                BuildingId = building.Id,
                OwnerId = building.ChuNhaId,
                Listing = new TinDangChiTietViewModel
                {
                    Id = post.Id,
                    TieuDe = post.TieuDe,
                    MoTa = post.NoiDung ?? room.MoTa,
                    GiaThue = room.GiaThue,
                    DienTich = room.DienTich,
                    SoNguoiToiDa = room.SoNguoiToiDa,
                    TienCocDuKien = room.TienCocDuKien,
                    DiaChi = building.DiaChi,
                    PhuongXa = building.PhuongXa,
                    QuanHuyen = building.QuanHuyen,
                    TinhThanh = building.TinhThanh
                }
            }).SingleOrDefaultAsync();

        if (result is null)
            return null;

        var listing = result.Listing;
        var servicePrices = await GetPublicServicePricesAsync(result.BuildingId, result.RoomId, result.OwnerId);
        return new TinDangChiTietViewModel
        {
            Id = listing.Id,
            TieuDe = listing.TieuDe,
            MoTa = listing.MoTa,
            GiaThue = listing.GiaThue,
            DienTich = listing.DienTich,
            SoNguoiToiDa = listing.SoNguoiToiDa,
            TienCocDuKien = listing.TienCocDuKien,
            DiaChi = listing.DiaChi,
            PhuongXa = listing.PhuongXa,
            QuanHuyen = listing.QuanHuyen,
            TinhThanh = listing.TinhThanh,
            Anh = await db.AnhPhongs.AsNoTracking()
                .Where(image => image.PhongId == result.RoomId)
                .OrderBy(image => image.ThuTu)
                .Select(image => new AnhPhongChiTietViewModel
                {
                    DuongDan = image.DuongDan,
                    DuongDanAnhNho = image.DuongDanAnhNho,
                    MoTa = image.MoTa
                })
                .ToListAsync(),
            DichVuTheoSuDung = servicePrices.Where(price => price.CachTinh != CachTinhDichVu.CoDinh).ToList(),
            KhoanCoDinh = servicePrices.Where(price => price.CachTinh == CachTinhDichVu.CoDinh).ToList()
        };
    }

    private async Task<TaoTinDangViewModel?> LoadDraftFormAsync(int roomId, int ownerId)
    {
        var source = await (
            from roomEntity in db.PhongTros.AsNoTracking()
            join building in db.ToaNhas.AsNoTracking() on roomEntity.ToaNhaId equals building.Id
            where roomEntity.Id == roomId && building.ChuNhaId == ownerId
            select new { Room = roomEntity, BuildingId = building.Id }
        ).SingleOrDefaultAsync();
        if (source is null)
            return null;

        var room = source.Room;
        return new TaoTinDangViewModel
        {
            PhongId = room.Id,
            ToaNhaId = source.BuildingId,
            MaPhong = room.MaPhong,
            TrangThaiPhong = room.TrangThai,
            DienTich = room.DienTich,
            GiaThue = room.GiaThue,
            TieuDe = $"Cho thuê phòng {room.MaPhong}",
            MoTaThem = null,
            NgayHetHan = DateTime.UtcNow.AddDays(30),
            Anh = await db.AnhPhongs.AsNoTracking()
                .Where(image => image.PhongId == room.Id)
                .OrderBy(image => image.ThuTu)
                .Select(image => new AnhPhongChiTietViewModel
                {
                    DuongDan = image.DuongDan,
                    DuongDanAnhNho = image.DuongDanAnhNho,
                    MoTa = image.MoTa
                })
                .ToListAsync(),
            DichVu = await GetPublicServicePricesAsync(source.BuildingId, room.Id, ownerId)
        };
    }

    private async Task<List<DichVuTinChiTietViewModel>> GetPublicServicePricesAsync(int buildingId, int roomId, int ownerId)
    {
        if (!await services.SanSangAsync() || !await services.SoHuuToaNhaAsync(ownerId, buildingId))
            return [];

        var today = DichVuService.HomNay();
        var servicesForBuilding = await db.DichVuToaNhas.AsNoTracking()
            .Include(item => item.DichVu)
            .Where(item => item.ToaNhaId == buildingId)
            .OrderBy(item => item.DichVu.TenDichVu)
            .ToListAsync();

        var result = new List<DichVuTinChiTietViewModel>();
        foreach (var service in servicesForBuilding)
        {
            var price = await roomServices.LayGiaHoaDonAsync(ownerId, roomId, service.DichVuId, today);
            if (price is null || string.IsNullOrWhiteSpace(price.TenDichVu)
                || string.IsNullOrWhiteSpace(price.DonViTinh)
                || !CachTinhDichVu.HopLe(price.CachTinh)
                || price.DonGia < 0)
                continue;

            var isUtility = service.DichVu.MaDichVu.Equals("DIEN", StringComparison.OrdinalIgnoreCase)
                || service.DichVu.MaDichVu.Equals("NUOC", StringComparison.OrdinalIgnoreCase);
            if (isUtility && price.DonGia == 0)
                continue;

            result.Add(new DichVuTinChiTietViewModel
            {
                TenDichVu = price.TenDichVu.Trim(),
                CachTinh = price.CachTinh,
                DonViTinh = price.DonViTinh.Trim(),
                DonGia = price.DonGia
            });
        }

        return result;
    }
}
