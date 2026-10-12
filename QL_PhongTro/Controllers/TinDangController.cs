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

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class TinDangController(AppDbContext db, YeuCauThueService requests, DichVuService services,
    DichVuPhongService roomServices, TinDangExpirationService expiration, IAppointmentEmailSender? email = null) : Controller
{
    private int AccountId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Index(string? sapXep = "moi-nhat", int trang = 1)
    {
        await expiration.ExpireAsync();
        sapXep = sapXep is "gia-tang" or "gia-giam" ? sapXep : "moi-nhat";
        const int pageSize = 12;
        var query = from post in requests.PublicListings()
                    join room in db.PhongTros.AsNoTracking() on post.PhongId equals room.Id
                    select new { Post = post, room.GiaThue };
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        trang = Math.Clamp(trang, 1, pages);
        var ordered = sapXep switch
        {
            "gia-tang" => query.OrderBy(x => x.GiaThue).ThenByDescending(x => x.Post.Id),
            "gia-giam" => query.OrderByDescending(x => x.GiaThue).ThenByDescending(x => x.Post.Id),
            _ => query.OrderByDescending(x => x.Post.NgayDang).ThenByDescending(x => x.Post.Id)
        };
        var listings = await ordered.Skip((trang - 1) * pageSize).Take(pageSize)
            .Select(x => new TinDangDanhSachViewModel
            {
                Id = x.Post.Id,
                TieuDe = x.Post.TieuDe,
                GiaThue = x.GiaThue,
                AnhDaiDien = db.AnhPhongs.Where(image => image.PhongId == x.Post.PhongId)
                    .OrderBy(image => image.ThuTu)
                    .Select(image => image.DuongDanAnhNho)
                    .FirstOrDefault()
            }).ToListAsync();
        ViewData["SapXep"] = sapXep;
        ViewData["Trang"] = trang;
        ViewData["TongTrang"] = pages;
        ViewData["TongTin"] = total;
        return View(listings);
    }

    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ChiTiet(int id)
    {
        await expiration.ExpireAsync();
        var model = await Detail(id, new());
        if (model is not null) return View(model);

        // Not a public listing (DANG_HIEN_THI). Check if it's DA_CHO_THUE for public viewing.
        var tin = await db.TinDangs.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id);
        if (tin is not null && tin.TrangThai == "DA_CHO_THUE")
        {
            var room = await db.PhongTros.AsNoTracking().SingleAsync(p => p.Id == tin.PhongId);
            var detail = await GetListingAsync(id, requirePublic: false);
            if (detail is not null)
            {
                var installed = await requests.IsInstalled();
                var existing = installed && User.IsInRole("KHACH_THUE")
                    && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
                    ? await requests.FindOpenRequest(id, accountId) : null;
                model = new(tin, room, new(), requests.Today)
                {
                    OpenRequestId = existing?.Id,
                    PublicDetail = detail,
                    RequestModuleInstalled = installed,
                    TinConCongKhai = false // Hide request form for rented listings
                };
                return View(model);
            }
        }

        // Fallback: historical detail for tenant with request
        if (User.IsInRole("KHACH_THUE")
            && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var tenantAccountId)
            && await requests.IsInstalled()
            && await db.YeuCauThues.AsNoTracking().AnyAsync(request => request.TinDangId == id
                && db.KhachThues.Any(tenant => tenant.Id == request.KhachThueId && tenant.TaiKhoanId == tenantAccountId)))
        {
            model = await HistoricalDetail(id);
        }
        return model is null ? NotFound() : View(model);
    }

    [Authorize(Roles = "CHU_NHA,QUAN_LY,ADMIN"), ModuleAccess("TIN_DANG", write: true), HttpGet]
    public async Task<IActionResult> QuanLy()
    {
        if (AccountId == 0) return Forbid();
        await expiration.ExpireAsync();
        var now = DateTime.UtcNow;
        var isManager = User.IsInRole("QUAN_LY");
        var isAdmin = User.IsInRole("ADMIN");
        var rooms = await (from room in db.PhongTros.AsNoTracking()
                           join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
                           where (isAdmin || building.ChuNhaId == AccountId || (isManager && building.QuanLyId == AccountId))
                               && building.DangHoatDong
                           orderby building.TenToaNha, room.MaPhong
                           select new { Room = room, BuildingName = building.TenToaNha }).ToListAsync();
        var roomIds = rooms.Select(x => x.Room.Id).ToArray();
        var thumbnails = await db.AnhPhongs.AsNoTracking().Where(image => roomIds.Contains(image.PhongId))
            .OrderBy(image => image.ThuTu).ToListAsync();
        var thumbnailByRoom = thumbnails.GroupBy(image => image.PhongId)
            .ToDictionary(group => group.Key, group => group.First().DuongDanAnhNho ?? group.First().DuongDan);
        var latest = await db.TinDangs.AsNoTracking().Where(x => roomIds.Contains(x.PhongId))
            .GroupBy(x => x.PhongId).Select(g => g.OrderByDescending(x => x.Id).First()).ToListAsync();
        var listingByRoom = latest.ToDictionary(x => x.PhongId);
        return View(new TinDangQuanLyViewModel
        {
            DanhSach = rooms.Select(x =>
            {
                listingByRoom.TryGetValue(x.Room.Id, out var listing);
                return new TinDangQuanLyItemViewModel
                {
                    PhongId = x.Room.Id,
                    ToaNhaId = x.Room.ToaNhaId,
                    GiaThue = x.Room.GiaThue,
                    DienTich = x.Room.DienTich,
                    AnhDaiDien = thumbnailByRoom.GetValueOrDefault(x.Room.Id),
                    NgayDang = listing?.NgayDang,
                    MaPhong = x.Room.MaPhong,
                    TenToaNha = x.BuildingName,
                    TrangThaiPhong = x.Room.TrangThai,
                    TinDangId = listing?.Id,
                    TieuDe = listing?.TieuDe,
                    TrangThaiTin = listing?.TrangThai,
                    NgayHetHan = listing?.NgayHetHan,
                    DaHetHan = listing?.TrangThai == "TAM_AN" && listing.NgayHetHan is { } expires && expires < now
                };
            }).ToList()
        });
    }

    [Authorize(Roles = "CHU_NHA,QUAN_LY,ADMIN"), ModuleAccess("TIN_DANG", write: true), HttpGet]
    public async Task<IActionResult> Tao(int? phongId)
    {
        if (!phongId.HasValue)
        {
            ViewData["ChooseListingBuilding"] = true;
            var options = await PhongDangTinOptionsAsync();
            if (options.Count == 0) return View("KhongCoPhongDangTin");
            phongId = options[0].Id;
        }
        var model = await TaoModelAsync(phongId.Value);
        return model is null ? NotFound() : View(model);
    }

    [Authorize(Roles = "CHU_NHA,QUAN_LY"), ModuleAccess("TIN_DANG", write: true), HttpGet]
    public async Task<IActionResult> Sua(int id)
    {
        var listing = await db.TinDangs.AsNoTracking().SingleOrDefaultAsync(post => post.Id == id);
        if (listing is null) return NotFound();
        var room = await db.PhongTros.AsNoTracking().SingleAsync(room => room.Id == listing.PhongId);
        var building = await db.ToaNhas.AsNoTracking().SingleAsync(building => building.Id == room.ToaNhaId);
        if (building.ChuNhaId != AccountId && !(User.IsInRole("QUAN_LY") && building.QuanLyId == AccountId)) return Forbid();
        var model = await TaoModelAsync(listing.PhongId);
        return model is null ? NotFound() : View("Tao", model with
        {
            TinDangId = listing.Id,
            TrangThaiTin = listing.TrangThai,
            TieuDe = listing.TieuDe,
            NoiDung = listing.NoiDung
        });
    }

    private async Task<List<PhongDangTinOption>> PhongDangTinOptionsAsync(int? currentRoomId = null)
    {
        var isManager = User.IsInRole("QUAN_LY");
        return await (from room in db.PhongTros.AsNoTracking()
                      join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
                      where building.DangHoatDong
                          && (building.ChuNhaId == AccountId || (isManager && building.QuanLyId == AccountId))
                          && ((room.TrangThai == "TRONG"
                              && !db.TinDangs.Any(post => post.PhongId == room.Id && post.TrangThai == "DANG_HIEN_THI"))
                              || room.Id == currentRoomId)
                      orderby building.TenToaNha, room.MaPhong
                      select new PhongDangTinOption(room.Id, building.Id, building.TenToaNha, room.MaPhong, room.Tang, room.DienTich))
                      .ToListAsync();
    }

    [Authorize(Roles = "CHU_NHA,QUAN_LY,ADMIN"), ModuleAccess("TIN_DANG", write: true), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Tao(TaoTinDangViewModel form, string? intent)
    {
        var source = await TaoModelAsync(form.PhongId);
        if (source is null) return NotFound();
        TinDang? listing;
        if (form.TinDangId is { } listingId)
        {
            listing = await db.TinDangs.SingleOrDefaultAsync(x => x.Id == listingId && x.PhongId == form.PhongId);
            if (listing is null) return NotFound();
        }
        else
        {
            listing = await db.TinDangs.Where(x => x.PhongId == form.PhongId && x.TrangThai != "DANG_HIEN_THI")
                .OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        }
        if (source.TrangThaiPhong != LichHenTrangThai.PhongTrong)
            ModelState.AddModelError("", "Chỉ phòng đang trống mới được đăng tin cho thuê.");
        var isDraft = intent == "NHAP";
        var wasPublic = listing?.TrangThai == "DANG_HIEN_THI";
        var currentListingId = listing?.Id ?? 0;
        if (intent is not (null or "NHAP" or "DANG_HIEN_THI"))
            ModelState.AddModelError("", "Thao tác tin đăng không hợp lệ.");
        if (isDraft && wasPublic)
            ModelState.AddModelError("", "Tin đang hiển thị không thể chuyển trực tiếp về bản nháp. Hãy gỡ tin nếu muốn tạm ẩn.");
        if (!isDraft && await db.TinDangs.AnyAsync(x => x.PhongId == form.PhongId && x.TrangThai == "DANG_HIEN_THI" && x.Id != currentListingId))
            ModelState.AddModelError("", "Phòng này đã có một tin đang hiển thị.");
        if (!ModelState.IsValid)
            return View(source with { TinDangId = listing?.Id, TrangThaiTin = listing?.TrangThai, TieuDe = form.TieuDe, NoiDung = form.NoiDung });

        var now = DateTime.UtcNow;
        if (listing is null)
        {
            listing = new TinDang { PhongId = form.PhongId, NguoiDangId = AccountId, NgayTao = now };
            db.TinDangs.Add(listing);
        }
        listing.NguoiDangId = AccountId;
        listing.TieuDe = form.TieuDe.Trim();
        listing.NoiDung = string.IsNullOrWhiteSpace(form.NoiDung) ? null : form.NoiDung.Trim();
        if (!isDraft && !wasPublic)
        {
            listing.NgayDang = now;
            listing.NgayHetHan = now.AddDays(30);
        }
        else if (isDraft)
        {
            listing.NgayDang = null;
            listing.NgayHetHan = now.AddDays(30);
        }
        listing.TrangThai = isDraft ? "NHAP" : "DANG_HIEN_THI";
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Không thể đăng tin vì phòng vừa có tin khác được hiển thị.");
            return View(source with { TinDangId = listing.Id, TrangThaiTin = listing.TrangThai, TieuDe = form.TieuDe, NoiDung = form.NoiDung });
        }
        TempData["TinDangOk"] = isDraft ? "Đã lưu tin đăng ở trạng thái nháp." : wasPublic ? "Đã cập nhật tin đăng." : "Đã đăng tin cho thuê.";
        return isDraft ? RedirectToAction(nameof(QuanLy)) : RedirectToAction(nameof(ChiTiet), new { id = listing.Id });
    }

    [Authorize(Roles = "CHU_NHA,QUAN_LY,ADMIN"), ModuleAccess("TIN_DANG", write: true), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Go(int id)
    {
        var isManager = User.IsInRole("QUAN_LY");
        var isAdmin = User.IsInRole("ADMIN");
        var listing = await db.TinDangs.SingleOrDefaultAsync(post => post.Id == id
            && db.PhongTros.Any(room => room.Id == post.PhongId
                && db.ToaNhas.Any(building => building.Id == room.ToaNhaId
                    && (isAdmin || building.ChuNhaId == AccountId || (isManager && building.QuanLyId == AccountId)))));
        if (listing is null) return NotFound();
        if (listing.TrangThai == "DANG_HIEN_THI")
        {
            listing.TrangThai = "TAM_AN";
            await db.SaveChangesAsync();
            TempData["TinDangOk"] = "Đã gỡ tin khỏi danh sách công khai.";
        }
        return RedirectToAction(nameof(QuanLy));
    }

    [Authorize(Roles = "CHU_NHA,QUAN_LY"), ModuleAccess("TIN_DANG", write: true), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiTrangThai(int id, string trangThai, string trangThaiDaXem)
    {
        if (trangThai is not ("NHAP" or "DANG_HIEN_THI" or "TAM_AN" or "DA_CHO_THUE")) return BadRequest();
        var listing = await db.TinDangs.SingleOrDefaultAsync(x => x.Id == id);
        if (listing is null) return NotFound();
        var room = await db.PhongTros.SingleAsync(x => x.Id == listing.PhongId);
        var building = await db.ToaNhas.SingleAsync(x => x.Id == room.ToaNhaId);
        if (building.ChuNhaId != AccountId && !(User.IsInRole("QUAN_LY") && building.QuanLyId == AccountId)) return Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync();
        string? error = listing.TrangThai != trangThaiDaXem ? "Tin đã thay đổi. Hãy tải lại danh sách." : null;
        if (trangThai == "NHAP" && listing.TrangThai == "DANG_HIEN_THI") error = "Hãy tạm ẩn tin công khai trước khi chuyển về bản nháp.";
        if (trangThai == "DANG_HIEN_THI")
        {
            if (room.TrangThai != "TRONG" || !building.DangHoatDong) error = "Chỉ đăng công khai phòng trống trong tòa đang hoạt động.";
            if (await db.TinDangs.AnyAsync(x => x.PhongId == room.Id && x.Id != id && x.TrangThai == "DANG_HIEN_THI")) error = "Phòng đã có tin công khai khác.";
        }
        if (error is not null) { TempData["TinDangError"] = error; return RedirectToAction(nameof(QuanLy)); }
        if (trangThai == "DANG_HIEN_THI" && listing.TrangThai != trangThai)
        { listing.NgayDang = DateTime.UtcNow; listing.NgayHetHan = DateTime.UtcNow.AddDays(30); }
        listing.TrangThai = trangThai;
        try { await db.SaveChangesAsync(); await transaction.CommitAsync(); TempData["TinDangOk"] = "Đã cập nhật trạng thái tin đăng."; }
        catch (DbUpdateException) { await transaction.RollbackAsync(); TempData["TinDangError"] = "Không thể cập nhật. Hãy tải lại danh sách."; }
        return RedirectToAction(nameof(QuanLy));
    }
    private async Task<TaoTinDangViewModel?> TaoModelAsync(int roomId)
    {
        var isManager = User.IsInRole("QUAN_LY");
        var isAdmin = User.IsInRole("ADMIN");
        var source = await (from room in db.PhongTros.AsNoTracking()
                            join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
                            where room.Id == roomId
                                && (isAdmin || building.ChuNhaId == AccountId || (isManager && building.QuanLyId == AccountId))
                                && building.DangHoatDong
                            select new { Room = room, Building = building }).SingleOrDefaultAsync();
        if (source is null)
            return null;

        var now = DateTime.UtcNow;
        var saved = await db.TinDangs.AsNoTracking().Where(post => post.PhongId == roomId)
            .OrderByDescending(post => post.Id).FirstOrDefaultAsync();
        var ownerContact = await db.TaiKhoans.AsNoTracking().Where(account => account.Id == source.Building.ChuNhaId)
            .Select(account => new { account.HoTen, account.SoDienThoai }).SingleOrDefaultAsync();
        return new TaoTinDangViewModel
        {
            TenChuNha = ownerContact?.HoTen,
            DienThoaiChuNha = ownerContact?.SoDienThoai,
            SoNguoiToiDa = source.Room.SoNguoiToiDa,
            PhongOptions = await PhongDangTinOptionsAsync(roomId),
            DiaChi = string.Join(", ", new[] { source.Building.DiaChi, source.Building.PhuongXa, source.Building.QuanHuyen, source.Building.TinhThanh }.Where(value => !string.IsNullOrWhiteSpace(value))),
            TienCoc = source.Room.TienCocDuKien,
            Tang = source.Room.Tang,
            PhongId = source.Room.Id,
            TinDangId = saved?.Id,
            TrangThaiTin = saved?.TrangThai,
            MaPhong = source.Room.MaPhong,
            TenToaNha = source.Building.TenToaNha,
            ToaNhaId = source.Building.Id,
            DienTich = source.Room.DienTich,
            GiaThue = source.Room.GiaThue,
            TieuDe = saved?.TieuDe ?? "Cho thuê phòng " + source.Room.MaPhong,
            NoiDung = saved is null ? source.Room.MoTa : saved.NoiDung,
            TrangThaiPhong = source.Room.TrangThai,
            NgayHetHan = now.AddDays(30),
            Anh = await db.AnhPhongs.AsNoTracking()
                .Where(image => image.PhongId == source.Room.Id)
                .OrderBy(image => image.ThuTu)
                .Select(image => new AnhPhongChiTietViewModel
                {
                    DuongDan = image.DuongDan,
                    DuongDanAnhNho = image.DuongDanAnhNho,
                    MoTa = image.MoTa,
                    ThuTu = image.ThuTu
                }).ToListAsync(),
            DichVu = await GetPublicServicePricesAsync(source.Building.Id, source.Room.Id, source.Building.ChuNhaId)
        };
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
            if (request is not null)
            {
                try
                {
                    var recipient = await (from listing in db.TinDangs.AsNoTracking()
                        join room in db.PhongTros.AsNoTracking() on listing.PhongId equals room.Id
                        join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
                        join owner in db.TaiKhoans.AsNoTracking() on building.ChuNhaId equals owner.Id
                        join tenant in db.KhachThues.AsNoTracking() on request.KhachThueId equals tenant.Id
                        where listing.Id == request.TinDangId && owner.DangHoatDong && !owner.IsDeleted
                        select new { owner.Email, owner.HoTen, TenantName = tenant.HoTen, room.MaPhong }).SingleOrDefaultAsync();
                    if (recipient is null || email is null) throw new InvalidOperationException("Không có người nhận hoặc dịch vụ email.");
                    await email.SendNewRequestAsync(recipient.Email, recipient.HoTen, request.MaYeuCau, recipient.TenantName, recipient.MaPhong, request.LoaiYeuCau, request.NgayMongMuon, request.Id);
                }
                catch (Exception)
                {
                    TempData["Error"] = "Yêu cầu đã được lưu nhưng chưa gửi được email báo cho chủ nhà. Chủ nhà vẫn xem được yêu cầu trong hệ thống.";
                }
            }
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

    private async Task<ChiTietTinDangViewModel?> HistoricalDetail(int id)
    {
        var detail = await GetListingAsync(id, requirePublic: false);
        if (detail is null) return null;
        var post = await db.TinDangs.AsNoTracking().SingleAsync(item => item.Id == id);
        var room = await db.PhongTros.AsNoTracking().SingleAsync(item => item.Id == post.PhongId);
        return new(post, room, new(), requests.Today)
        {
            PublicDetail = detail,
            RequestModuleInstalled = await requests.IsInstalled(),
            TinConCongKhai = false
        };
    }

    [Authorize(Roles = "KHACH_THUE"), HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> YeuCau(int id, [FromServices] LichHenService appointments)
    {
        if (!await requests.IsInstalled()) return NotFound();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return Forbid();
        var request = await db.YeuCauThues.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id
            && db.KhachThues.Any(k => k.Id == r.KhachThueId && k.TaiKhoanId == accountId));
        if (request is null) return NotFound();
        var listing = await GetListingAsync(request.TinDangId, requirePublic: false);
        var history = await appointments.LichSuAsync(accountId, id, HttpContext.RequestAborted);
        return View(new ChiTietYeuCauKhachViewModel(request, listing, history));
    }
    [HttpGet("/api/tin-dang/{id:int}")]
    [Produces("application/json")]
    public async Task<IActionResult> ChiTietApi(int id)
    {
        var listing = await GetPublicListingAsync(id);
        if (listing is not null) return Ok(listing);

        // Allow DA_CHO_THUE for API consumers
        var tin = await db.TinDangs.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id);
        if (tin is not null && tin.TrangThai == "DA_CHO_THUE")
        {
            var detail = await GetListingAsync(id, requirePublic: false);
            return detail is not null ? Ok(detail) : NotFound();
        }
        return NotFound();
    }

    private async Task<TinDangChiTietViewModel?> GetPublicListingAsync(int id)
        => await GetListingAsync(id, requirePublic: true);

    private async Task<TinDangChiTietViewModel?> GetListingAsync(int id, bool requirePublic)
    {
        var now = DateTime.UtcNow;
        var result = await (
            from post in db.TinDangs.AsNoTracking()
            join room in db.PhongTros.AsNoTracking() on post.PhongId equals room.Id
            join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
            where post.Id == id
                && (!requirePublic || (post.TrangThai == "DANG_HIEN_THI"
                    && (post.NgayHetHan == null || post.NgayHetHan >= now)
                    && room.TrangThai == "TRONG"
                    && building.DangHoatDong))
            select new
            {
                RoomId = room.Id,
                BuildingId = building.Id,
                OwnerId = building.ChuNhaId,
                OwnerName = db.TaiKhoans.Where(a => a.Id == building.ChuNhaId).Select(a => a.HoTen).FirstOrDefault(),
                building.ThangMay, building.BaiDoXe, building.CameraAnNinh, building.BaoVe24h, building.KhuGiatSay, building.SanThuong,
                Listing = new TinDangChiTietViewModel
                {
                    Id = post.Id,
                    MaPhong = room.MaPhong,
                    TenToaNha = building.TenToaNha,
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
        var amenities = new List<string>();
        if (result.ThangMay) amenities.Add("Thang máy");
        if (result.BaiDoXe) amenities.Add("Chỗ để xe");
        if (result.CameraAnNinh) amenities.Add("Camera an ninh");
        if (result.BaoVe24h) amenities.Add("Bảo vệ 24 giờ");
        if (result.KhuGiatSay) amenities.Add("Khu giặt sấy");
        if (result.SanThuong) amenities.Add("Sân thượng");
        var servicePrices = await GetPublicServicePricesAsync(result.BuildingId, result.RoomId, result.OwnerId);
        return new TinDangChiTietViewModel
        {
            TenChuNha = result.OwnerName,
            TienNghi = amenities,
            Id = listing.Id,
            MaPhong = listing.MaPhong,
            TenToaNha = listing.TenToaNha,
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
                    MoTa = image.MoTa,
                    ThuTu = image.ThuTu
                })
                .ToListAsync(),
            DichVuTheoSuDung = servicePrices.Where(price => price.CachTinh != CachTinhDichVu.CoDinh).ToList(),
            KhoanCoDinh = servicePrices.Where(price => price.CachTinh == CachTinhDichVu.CoDinh).ToList()
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
