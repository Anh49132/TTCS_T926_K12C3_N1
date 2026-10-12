using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
using QL_PhongTro.Services;
using QL_PhongTro.Authorization;

namespace QL_PhongTro.Controllers;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class HoSoController(AppDbContext db, GiayToImageStore images, HoSoAccess access, PermissionService permissions, ITimeProvider? clock = null) : Controller
{
    private async Task<TaiKhoan?> CurrentViewerAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        return await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.DangHoatDong && !x.IsDeleted);
    }
    private async Task<TaiKhoan?> CurrentTenantAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return null;
        return await db.TaiKhoans.SingleOrDefaultAsync(x => x.Id == id && x.DangHoatDong && !x.IsDeleted && x.VaiTro == "KHACH_THUE");
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null)
        {
            var viewer = await CurrentViewerAsync();
            return viewer?.VaiTro == "CHU_NHA" ? RedirectToAction(nameof(DanhSach)) : Forbid();
        }
        var profile = await db.KhachThues.AsNoTracking().SingleOrDefaultAsync(x => x.TaiKhoanId == tenant.Id);
        return View(new HoSoViewModel
        {
            CoHoSo = profile is not null,
            HoTen = profile?.HoTen ?? tenant.HoTen,
            NgaySinh = profile?.NgaySinh,
            CanCuocDaLuu = HoSoAccess.Mask(profile?.SoGiayTo),
            QueQuan = profile?.QueQuan ?? string.Empty,
            NgheNghiep = profile?.NgheNghiep ?? string.Empty,
            CoAnhMatTruoc = profile?.AnhGiayToTruoc is not null,
            CoAnhMatSau = profile?.AnhGiayToSau is not null
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> Index(HoSoViewModel model)
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return Forbid();
        var profile = await db.KhachThues.SingleOrDefaultAsync(x => x.TaiKhoanId == tenant.Id);
        model.CoHoSo = profile is not null;
        model.CoAnhMatTruoc = profile?.AnhGiayToTruoc is not null;
        model.CoAnhMatSau = profile?.AnhGiayToSau is not null;
        model.CanCuocDaLuu = HoSoAccess.Mask(profile?.SoGiayTo);
        if (string.IsNullOrEmpty(model.SoCanCuoc) && string.IsNullOrEmpty(profile?.SoGiayTo))
            ModelState.AddModelError(nameof(model.SoCanCuoc), "Vui lòng nhập số căn cước.");
        async Task<PreparedImage?> ValidateImage(IFormFile? file, string field)
        {
            try { return await images.PrepareAsync(file); }
            catch (InvalidDataException ex) { ModelState.AddModelError(field, ex.Message); return null; }
        }
        var front = await ValidateImage(model.AnhMatTruoc, nameof(model.AnhMatTruoc));
        var back = await ValidateImage(model.AnhMatSau, nameof(model.AnhMatSau));
        if (!ModelState.IsValid) return InvalidForm(model);
        var isNew = profile is null;
        if (profile is null)
        {
            profile = new KhachThue { TaiKhoanId = tenant.Id, NgayTao = DateTime.UtcNow };
            db.KhachThues.Add(profile);
        }
        profile.HoTen = model.HoTen.Trim();
        profile.SoDienThoai = tenant.SoDienThoai;
        profile.NgaySinh = model.NgaySinh;
        if (!string.IsNullOrEmpty(model.SoCanCuoc)) profile.SoGiayTo = model.SoCanCuoc;
        profile.QueQuan = model.QueQuan.Trim();
        profile.NgheNghiep = model.NgheNghiep.Trim();
        var oldFront = profile.AnhGiayToTruoc;
        var oldBack = profile.AnhGiayToSau;
        string? newFront = null, newBack = null;
        try
        {
            if (front is not null) profile.AnhGiayToTruoc = newFront = await images.SaveAsync(front);
            if (back is not null) profile.AnhGiayToSau = newBack = await images.SaveAsync(back);
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException)
        {
            images.Delete(newFront);
            images.Delete(newBack);
            ModelState.AddModelError("", "Không thể lưu hồ sơ lúc này. Vui lòng thử lại và chọn lại ảnh.");
            return InvalidForm(model);
        }
        if (newFront is not null) images.Delete(oldFront);
        if (newBack is not null) images.Delete(oldBack);
        TempData["Success"] = isNew
            ? "Đã tạo hồ sơ cá nhân thành công."
            : "Đã cập nhật hồ sơ cá nhân thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Xoa()
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return Forbid();
        var profile = await db.KhachThues.SingleOrDefaultAsync(x => x.TaiKhoanId == tenant.Id);
        if (profile is null)
        {
            TempData["Error"] = "Bạn chưa có hồ sơ để xóa.";
            return RedirectToAction(nameof(Index));
        }

        db.KhachThues.Remove(profile);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteExtendedErrorCode: 787 })
        {
            TempData["Error"] = "Không thể xóa hồ sơ đang được hợp đồng hoặc dữ liệu khác sử dụng.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Không thể xóa hồ sơ lúc này. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }

        images.Delete(profile.AnhGiayToTruoc);
        images.Delete(profile.AnhGiayToSau);
        TempData["Success"] = "Đã xóa hồ sơ cá nhân thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Anh(string mat, int? id)
    {
        var viewer = await CurrentViewerAsync();
        if (viewer is null) return Forbid();
        if (mat is not ("truoc" or "sau")) return NotFound();
        var profile = id.HasValue
            ? await db.KhachThues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value)
            : await db.KhachThues.AsNoTracking().SingleOrDefaultAsync(x => x.TaiKhoanId == viewer.Id);
        if (profile is null) return NotFound();
        if (!(viewer.VaiTro == "KHACH_THUE" && profile.TaiKhoanId == viewer.Id) && (!await permissions.AllowsAsync("HOP_DONG") || !await access.CanReadFullAsync(viewer, profile.Id))) return NotFound();
        var path = images.GetPath(mat == "truoc" ? profile?.AnhGiayToTruoc : profile?.AnhGiayToSau);
        if (path is null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, Path.GetExtension(path) == ".jpg" ? "image/jpeg" : "image/png");
    }

    private IActionResult InvalidForm(HoSoViewModel model)
    {
        // Do not echo the submitted full number through Razor's ModelState value.
        model.SoCanCuoc = null;
        ModelState.SetModelValue(nameof(model.SoCanCuoc), new Microsoft.AspNetCore.Mvc.ModelBinding.ValueProviderResult(string.Empty));
        return View("Index", model);
    }

    [HttpGet]
    public async Task<IActionResult> CuaToi()
    {
        var viewer = await CurrentViewerAsync();
        if (viewer is null) return Forbid();
        if (viewer.VaiTro == "KHACH_THUE") return RedirectToAction(nameof(Xem));
        var role = viewer.VaiTro switch { "CHU_NHA" => "Chủ nhà", "QUAN_LY" => "Quản lý tòa nhà", "ADMIN" => "Quản trị hệ thống", _ => "Tài khoản" };
        return View(new HoSoCaNhanViewModel(viewer.Id, viewer.HoTen, role, viewer.SoDienThoai, viewer.Email, viewer.EmailConfirmed, viewer.NgayTao, viewer.LastLogin));
    }

    private async Task<List<(int ProfileId, HoSoHopDongRow Row)>> QuanHeHoSoAsync(TaiKhoan viewer)
    {
        var contracts = await (from h in db.HopDongs.AsNoTracking()
            join p in db.PhongTros on h.PhongId equals p.Id join b in db.ToaNhas on p.ToaNhaId equals b.Id
            where (viewer.VaiTro == "ADMIN" || b.ChuNhaId == viewer.Id)
                && (h.TrangThai == "CHO_HIEU_LUC" || h.TrangThai == "DANG_HIEU_LUC" || h.TrangThai == "DA_KET_THUC")
            select new { h.Id, h.MaHopDong, h.KhachDungTenId, h.TrangThai, BuildingId = b.Id, b.TenToaNha, p.MaPhong, p.SoNguoiToiDa }).ToListAsync();
        var ids = contracts.Select(h => h.Id).ToList();
        var periods = await db.KyHopDongs.AsNoTracking().Where(k => ids.Contains(k.HopDongId)).ToListAsync();
        var stays = await db.NguoiOGheps.AsNoTracking().Where(g => ids.Contains(g.HopDongId)).ToListAsync();
        var today = DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7));
        var result = new List<(int, HoSoHopDongRow)>();
        foreach (var h in contracts)
        {
            var terms = periods.Where(k => k.HopDongId == h.Id).ToList();
            DateOnly? start = terms.Count == 0 ? null : terms.Min(k => k.NgayBatDau);
            DateOnly? end = terms.Count == 0 ? null : terms.Max(k => k.NgayKetThuc);
            var active = h.TrangThai == "DANG_HIEU_LUC" && terms.Any(k => k.NgayBatDau <= today && k.NgayKetThuc >= today);
            var members = stays.Where(g => g.HopDongId == h.Id).ToList();
            var count = members.Count(g => g.NgayVao <= today && (g.NgayRa == null || g.NgayRa >= today)) + (h.KhachDungTenId is null ? 0 : 1);
            HoSoHopDongRow Row(string role, DateOnly? from, DateOnly? to, bool here) => new(h.Id, h.MaHopDong, h.BuildingId, h.TenToaNha, h.MaPhong, role, h.TrangThai, from, to, here, h.SoNguoiToiDa, count);
            if (h.KhachDungTenId is {} signer) result.Add((signer, Row("Người đứng tên", start, end, active)));
            foreach (var member in members)
                result.Add((member.KhachThueId, Row("Người ở ghép", member.NgayVao, member.NgayRa ?? end,
                    active && member.NgayVao <= today && (member.NgayRa == null || member.NgayRa >= today))));
        }
        return result;
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("HOP_DONG")]
    public async Task<IActionResult> LocDanhSach(string? timKiem, int? toaNhaId, string? trangThai)
    {
        var viewer = await CurrentViewerAsync();
        if (viewer?.VaiTro is not ("CHU_NHA" or "ADMIN")) return Forbid();
        if (timKiem?.Length > 100) return BadRequest("Từ khóa tìm kiếm tối đa 100 ký tự.");
        if (toaNhaId.HasValue && !await db.ToaNhas.AnyAsync(b => b.Id == toaNhaId && (viewer.VaiTro == "ADMIN" || b.ChuNhaId == viewer.Id))) return Forbid();
        // CookieTempData is encrypted by Data Protection. Do not put phone/identity search terms in URLs.
        TempData["OwnerProfileSearch"] = viewer.Id + ":" + (timKiem?.Trim() ?? "");
        return RedirectToAction(nameof(DanhSach), new { toaNhaId, trangThai });
    }

    [HttpGet, ModuleAccess("HOP_DONG")]
    public async Task<IActionResult> DanhSach(int? toaNhaId, string? trangThai, int trang = 1)
    {
        var viewer = await CurrentViewerAsync();
        if (viewer?.VaiTro is not ("CHU_NHA" or "ADMIN")) return Forbid();
        if (toaNhaId.HasValue && !await db.ToaNhas.AnyAsync(b => b.Id == toaNhaId && (viewer.VaiTro == "ADMIN" || b.ChuNhaId == viewer.Id))) return Forbid();
        var savedSearch = TempData.Peek("OwnerProfileSearch") as string;
        var prefix = viewer.Id + ":";
        var timKiem = savedSearch?.StartsWith(prefix, StringComparison.Ordinal) == true ? savedSearch[prefix.Length..] : null;
        var relations = await QuanHeHoSoAsync(viewer);
        var ids = viewer.VaiTro == "ADMIN" ? await db.KhachThues.Select(k => k.Id).ToListAsync() : relations.Select(r => r.ProfileId).Distinct().ToList();
        var profiles = await db.KhachThues.AsNoTracking().Where(k => ids.Contains(k.Id)).ToListAsync();
        var grouped = relations.GroupBy(r => r.ProfileId).ToDictionary(g => g.Key, g => g.Select(r => r.Row).ToList());
        List<HoSoHopDongRow> Rows(int id) => grouped.GetValueOrDefault(id) ?? [];
        string State(List<HoSoHopDongRow> rows) => rows.Count == 0 ? "CHUA_CO_HOP_DONG" : rows.Any(r => r.DangO) ? "DANG_THUE" : rows.Any(r => r.TrangThai == "CHO_HIEU_LUC") ? "CHO_HIEU_LUC" : "DA_KET_THUC";
        bool Complete(KhachThue k) => !string.IsNullOrWhiteSpace(k.HoTen) && !string.IsNullOrWhiteSpace(k.SoDienThoai) && !string.IsNullOrWhiteSpace(k.SoGiayTo);
        var model = new HoSoChuNhaDanhSachViewModel { TimKiem = timKiem?.Trim(), ToaNhaId = toaNhaId, TrangThai = trangThai,
            TongKhach = profiles.Count, DangThue = profiles.Count(k => State(Rows(k.Id)) == "DANG_THUE"),
            DaKetThuc = profiles.Count(k => State(Rows(k.Id)) == "DA_KET_THUC"), ThieuHoSo = profiles.Count(k => !Complete(k)) };
        model.ToaNhas = (await db.ToaNhas.AsNoTracking().Where(b => viewer.VaiTro == "ADMIN" || b.ChuNhaId == viewer.Id).OrderBy(b => b.TenToaNha).Select(b => new { b.Id, b.TenToaNha }).ToListAsync()).Select(b => (b.Id, b.TenToaNha)).ToList();
        var filtered = profiles.Where(k => (!toaNhaId.HasValue || Rows(k.Id).Any(r => r.ToaNhaId == toaNhaId))
            && (string.IsNullOrEmpty(trangThai) || State(Rows(k.Id).Where(r => !toaNhaId.HasValue || r.ToaNhaId == toaNhaId).ToList()) == trangThai)
            && (string.IsNullOrEmpty(model.TimKiem) || k.HoTen.Contains(model.TimKiem, StringComparison.OrdinalIgnoreCase)
                || (k.SoDienThoai?.Contains(model.TimKiem) ?? false) || (k.SoGiayTo?.Contains(model.TimKiem) ?? false))).OrderBy(k => k.HoTen).ThenBy(k => k.Id).ToList();
        model.TongKetQua = filtered.Count; model.TongTrang = Math.Max(1, (filtered.Count + 9) / 10); model.Trang = Math.Clamp(trang, 1, model.TongTrang);
        model.Khachs = filtered.Skip((model.Trang - 1) * 10).Take(10).Select(k => {
            var rows = Rows(k.Id).Where(r => !toaNhaId.HasValue || r.ToaNhaId == toaNhaId).ToList(); var row = rows.OrderByDescending(r => r.DangO).ThenByDescending(r => r.BatDau).FirstOrDefault();
            return new HoSoChuNhaRow(k.Id, k.HoTen, HoSoAccess.Mask(k.SoDienThoai), HoSoAccess.Mask(k.SoGiayTo), k.NgaySinh,
                row?.Phong ?? "—", row?.ToaNha ?? "—", row?.VaiTro ?? "—", State(rows), Complete(k));
        }).ToList();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Xem(int? id)
    {
        var viewer = await CurrentViewerAsync();
        if (viewer is null) return Forbid();
        if (viewer.VaiTro == "CHU_NHA" && !id.HasValue) return RedirectToAction(nameof(DanhSach));
        if (viewer.VaiTro is not ("KHACH_THUE" or "CHU_NHA" or "ADMIN")) return Forbid();
        var profile = id.HasValue
            ? await db.KhachThues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value)
            : await db.KhachThues.AsNoTracking().SingleOrDefaultAsync(x => x.TaiKhoanId == viewer.Id);
        if (profile is null)
        {
            if (id.HasValue) return NotFound();
            if (viewer.VaiTro != "KHACH_THUE") return View(new XemHoSoViewModel());
            return View(new XemHoSoViewModel { LaChuHoSo = true, HoTen = viewer.HoTen, TaiKhoanId = viewer.Id,
                SoDienThoai = viewer.SoDienThoai, Email = viewer.Email, EmailDaXacThuc = viewer.EmailConfirmed,
                NgayTaoTaiKhoan = viewer.NgayTao, DangNhapGanNhat = viewer.LastLogin });
        }
        var own = viewer.VaiTro == "KHACH_THUE" && profile.TaiKhoanId == viewer.Id;
        var full = await access.CanReadFullAsync(viewer, profile.Id);
        if (!own && (!full || !await permissions.AllowsAsync("HOP_DONG"))) return Forbid();
        var model = new XemHoSoViewModel
        {
            Id = profile.Id, HoTen = profile.HoTen,
            CanCuocHienThi = full ? profile.SoGiayTo ?? string.Empty : HoSoAccess.Mask(profile.SoGiayTo),
            XemDayDu = full, LaChuHoSo = own,
            NgaySinh = full || own ? profile.NgaySinh : null,
            QueQuan = full || own ? profile.QueQuan : null,
            NgheNghiep = full || own ? profile.NgheNghiep : null,
            CoAnhMatTruoc = (full || own) && profile.AnhGiayToTruoc is not null,
            CoAnhMatSau = (full || own) && profile.AnhGiayToSau is not null
        };
        if (own)
        {
            model.TaiKhoanId = viewer.Id;
            model.SoDienThoai = viewer.SoDienThoai;
            model.Email = viewer.Email;
            model.EmailDaXacThuc = viewer.EmailConfirmed;
            model.NgayTaoTaiKhoan = viewer.NgayTao;
            model.DangNhapGanNhat = viewer.LastLogin;
            var today = DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7));
            var eligible = db.HopDongs.AsNoTracking().Where(h => (h.TrangThai == "DANG_HIEU_LUC" || h.TrangThai == "CHO_HIEU_LUC")
                && db.KyHopDongs.Any(k => k.HopDongId == h.Id && k.NgayKetThuc >= today));
            var signer = await eligible.Where(h => h.KhachDungTenId == profile.Id).Select(h => h.Id).ToListAsync();
            var roommate = await (from h in eligible join g in db.NguoiOGheps on h.Id equals g.HopDongId
                where g.KhachThueId == profile.Id && g.NgayVao <= today && (g.NgayRa == null || g.NgayRa >= today)
                select h.Id).Distinct().ToListAsync();
            model.SoHopDong = signer.Concat(roommate).Distinct().Count();
            model.VaiTroThue = signer.Count > 0 && roommate.Count > 0 ? "Người đứng tên và người ở ghép"
                : signer.Count > 0 ? "Người đứng tên" : roommate.Count > 0 ? "Người ở ghép" : "Chưa tham gia hợp đồng";
        }
        if (!own)
        {
            model.SoDienThoai = profile.SoDienThoai;
            model.HoSoDayDu = !string.IsNullOrWhiteSpace(profile.HoTen) && !string.IsNullOrWhiteSpace(profile.SoDienThoai) && !string.IsNullOrWhiteSpace(profile.SoGiayTo);
            model.HopDongsLienQuan = (await QuanHeHoSoAsync(viewer)).Where(r => r.ProfileId == profile.Id).Select(r => r.Row).OrderByDescending(r => r.DangO).ThenByDescending(r => r.BatDau).ToList();
        }
        return View(model);
    }
}
