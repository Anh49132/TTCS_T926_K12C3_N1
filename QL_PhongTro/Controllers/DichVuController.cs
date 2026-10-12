using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA,ADMIN")]
[ModuleAccess("PHONG_TRO")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DichVuController(AppDbContext db, DichVuService services, DichVuPhongService roomServices) : Controller
{
    private int AccountId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> DienNuoc(int toaNhaId)
    {
        if (!await services.SoHuuToaNhaAsync(AccountId, toaNhaId)) return Forbid();
        if (!await services.SanSangAsync()) return RedirectToAction(nameof(Index), new { toaNhaId });
        ViewData["UtilityRoomCount"] = await db.PhongTros.CountAsync(room => room.ToaNhaId == toaNhaId);
        return View(await services.LayCauHinhDienNuocAsync(AccountId, toaNhaId));
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> DienNuoc(CauHinhDienNuocViewModel model)
    {
        if (!await services.SoHuuToaNhaAsync(AccountId, model.ToaNhaId)) return Forbid();
        if (!await services.SanSangAsync()) return RedirectToAction(nameof(Index), new { toaNhaId = model.ToaNhaId });
        var saved = await services.LayCauHinhDienNuocAsync(AccountId, model.ToaNhaId);
        ViewData["UtilityRoomCount"] = await db.PhongTros.CountAsync(room => room.ToaNhaId == model.ToaNhaId);
        model.TenToaNha = saved.TenToaNha;
        model.DienDaLuu = saved.DienDaLuu;
        model.NuocDaLuu = saved.NuocDaLuu;
        model.DienHienTai = saved.DienHienTai;
        model.NuocHienTai = saved.NuocHienTai;
        model.DienCho = saved.DienCho;
        model.NuocCho = saved.NuocCho;
        model.KyHienTai = saved.KyHienTai;
        model.KyKeTiep = saved.KyKeTiep;
        if (model.KyDaXem != saved.KyKeTiep || model.TrangThaiDaXem != saved.TrangThaiDaXem)
        {
            ModelState.AddModelError("", "Kỳ áp dụng hoặc cấu hình đã thay đổi. Hãy kiểm tra lại trước khi lưu.");
            model.KyDaXem = saved.KyKeTiep;
            model.TrangThaiDaXem = saved.TrangThaiDaXem;
            ModelState.Remove(nameof(model.KyDaXem));
            ModelState.Remove(nameof(model.TrangThaiDaXem));
        }
        ValidateUtility(nameof(model.Dien), model.Dien);
        ValidateUtility(nameof(model.Nuoc), model.Nuoc);
        if (!ModelState.IsValid) return View(model);
        try
        {
            await services.LuuCauHinhDienNuocAsync(AccountId, model);
            TempData["Success"] = "Đã lưu cấu hình điện nước cho kỳ kế tiếp.";
            return RedirectToAction(nameof(DienNuoc), new { toaNhaId = model.ToaNhaId });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        catch (DbUpdateException) { ModelState.AddModelError("", "Không lưu được cấu hình. Hãy tải lại trang và thử lại."); }
        return View(model);
    }

    private void ValidateUtility(string prefix, CauHinhTienDichVuViewModel input)
    {
        if (input.CachTinh is CachTinhDichVu.TheoChiSo or CachTinhDichVu.TheoNguoi)
        {
            var unused = input.CachTinh == CachTinhDichVu.TheoChiSo
                ? nameof(input.TienMotNguoi) : nameof(input.DonGiaChiSo);
            ModelState.Remove($"{prefix}.{unused}");
        }
        // MVC may skip IValidatableObject when an unused numeric field fails binding.
        // Recheck the selected field without clearing its binding errors or attempted value.
        foreach (var error in input.Validate(new ValidationContext(input)))
        {
            foreach (var member in error.MemberNames)
            {
                var key = $"{prefix}.{member}";
                if (!ModelState.TryGetValue(key, out var state) || state.Errors.Count == 0)
                    ModelState.AddModelError(key, error.ErrorMessage!);
            }
        }
    }
    private Task<List<SelectListItem>> ToaNhasAsync() => db.ToaNhas.AsNoTracking()
        .Where(x => (x.ChuNhaId == AccountId || User.IsInRole("ADMIN")) && x.DangHoatDong).OrderBy(x => x.TenToaNha)
        .Select(x => new SelectListItem(x.TenToaNha, x.Id.ToString())).ToListAsync();

    [HttpGet]
    public async Task<IActionResult> Index(int? toaNhaId)
    {
        var buildings = await ToaNhasAsync();
        if (toaNhaId.HasValue && !await services.SoHuuToaNhaAsync(AccountId, toaNhaId.Value)) return Forbid();
        var selected = toaNhaId;
        var ready = await services.SanSangAsync();
        return View(new DanhSachDichVuViewModel
        {
            ToaNhaId = selected, ToaNhas = buildings, SanSang = ready,
            MacDinhIds = ready && selected.HasValue ? (await db.DichVuToaNhas.Where(x => x.ToaNhaId == selected && x.ApDungMacDinh).Select(x => x.DichVuId).ToListAsync()).ToHashSet() : [],
            CanKhoiTao = ready && selected.HasValue && !await services.DaKhoiTaoAsync(AccountId, selected.Value),
            DichVus = ready && selected.HasValue ? await services.DanhSachAsync(AccountId, selected.Value) : []
        });
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> Initialize(int toaNhaId)
    {
        try { await services.KhoiTaoMacDinhAsync(AccountId, toaNhaId); TempData["Success"] = "Đã tạo dịch vụ mặc định thành công."; }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index), new { toaNhaId });
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> SetDefault(int toaNhaId, int dichVuId, bool enabled, bool returnToManage = false)
    {
        if (!ModelState.IsValid) return BadRequest();
        try { await roomServices.DatMacDinhAsync(AccountId, toaNhaId, dichVuId, enabled); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return returnToManage ? RedirectToAction(nameof(Manage), new { toaNhaId, dichVuId }) : RedirectToAction(nameof(Index), new { toaNhaId });
    }

    [HttpGet]
    public async Task<IActionResult> Manage(int toaNhaId, int dichVuId)
    {
        try
        {
            var history = await services.LichSuAsync(AccountId, toaNhaId, dichVuId);
            if (history.Count == 0) return NotFound();
            var latest = history.Last();
            var nextDate = latest.TuNgay.AddDays(1) > DichVuService.HomNay() ? latest.TuNgay.AddDays(1) : DichVuService.HomNay();
            var model = new QuanLyDichVuViewModel { ToaNhaId = toaNhaId, DichVuId = dichVuId, LichSu = history,
                DonGia = latest.DaChotGia ? latest.DonGia : null, GiaCu = latest.DonGia, PhienBan = latest.Id, TuNgay = nextDate };
            await FillManageAsync(model);
            return View(model);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet]
    public async Task<IActionResult> ChuyenToaSuaGia(int toaNhaId, int dichVuId)
    {
        if (!await services.SoHuuToaNhaAsync(AccountId, toaNhaId)) return Forbid();
        var ids = await db.CauHinhDichVus.AsNoTracking().Where(x => x.ToaNhaId == toaNhaId && x.PhongId == null)
            .Select(x => x.DichVuId).Distinct().OrderBy(x => x).ToListAsync();
        if (ids.Count == 0) return RedirectToAction(nameof(Index), new { toaNhaId });
        return RedirectToAction(nameof(Manage), new { toaNhaId, dichVuId = ids.Contains(dichVuId) ? dichVuId : ids[0] });
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> SavePrice(QuanLyDichVuViewModel model, bool initial = false)
    {
        if (!initial && !model.TuNgay.HasValue) ModelState.AddModelError(nameof(model.TuNgay), "Hãy chọn ngày hiệu lực.");
        try
        {
            model.LichSu = await services.LichSuAsync(AccountId, model.ToaNhaId, model.DichVuId);
            if (model.LichSu.Count == 0) return NotFound();
            await FillManageAsync(model);
            if (!ModelState.IsValid) return View("Manage", model);
            if (initial) await services.SuaGiaBanDauAsync(AccountId, model.ToaNhaId, model.DichVuId, model.DonGia!.Value, model.GiaCu);
            else await services.DoiGiaAsync(AccountId, model.ToaNhaId, model.DichVuId, model.DonGia!.Value, model.TuNgay!.Value, model.PhienBan);
            TempData["Success"] = "Đã cập nhật đơn giá dịch vụ thành công.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); return View("Manage", model); }
        return RedirectToAction(nameof(Manage), new { model.ToaNhaId, model.DichVuId });
    }

    private async Task FillManageAsync(QuanLyDichVuViewModel model)
    {
        ViewData["PriceBuildingOptions"] = await ToaNhasAsync();
        var priceRows = await db.CauHinhDichVus.AsNoTracking().Include(x => x.DichVu)
            .Where(x => x.ToaNhaId == model.ToaNhaId && x.PhongId == null)
            .OrderBy(x => x.TuNgay).ToListAsync();
        ViewData["BuildingPriceRows"] = priceRows.GroupBy(x => x.DichVuId).Select(group => group.LastOrDefault(x => x.TuNgay <= DichVuService.HomNay() && (x.DenNgay == null || x.DenNgay >= DichVuService.HomNay())) ?? group.Last()).ToList();
        ViewData["PriceServiceRoomCounts"] = await db.DichVuPhongs.AsNoTracking()
            .Where(x => x.DichVuToaNha.ToaNhaId == model.ToaNhaId)
            .GroupBy(x => x.DichVuToaNha.DichVuId).Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count);
        model.TenToaNha = await db.ToaNhas.Where(x => x.Id == model.ToaNhaId).Select(x => x.TenToaNha).SingleAsync();
        ViewData["PriceBuildingAddress"] = await db.ToaNhas.Where(x => x.Id == model.ToaNhaId).Select(x => x.DiaChi).SingleAsync();
        ViewData["PriceBuildingRooms"] = await db.PhongTros.CountAsync(x => x.ToaNhaId == model.ToaNhaId);
        model.ApDungMacDinh = await db.DichVuToaNhas.AnyAsync(x => x.ToaNhaId == model.ToaNhaId && x.DichVuId == model.DichVuId && x.ApDungMacDinh);
        var today = DichVuService.HomNay();
        var selections = db.DichVuPhongs.AsNoTracking().Where(x => x.DichVuToaNha.ToaNhaId == model.ToaNhaId && x.DichVuToaNha.DichVuId == model.DichVuId
            && !db.NgungDichVuPhongs.Any(n => n.DichVuPhongId == x.Id && n.NgungTuKy <= today && (n.ApDungLaiTuKy == null || n.ApDungLaiTuKy > today)));
        model.SoPhongApDung = await selections.CountAsync();
        model.SoPhongGiaRieng = await selections.CountAsync(x => x.DonGiaRieng != null);
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> SetStatus(int toaNhaId, int dichVuId, bool active, DateOnly? tuNgay, int phienBan)
    {
        try
        {
            if (!tuNgay.HasValue || !ModelState.IsValid) throw new InvalidOperationException("Ngày hiệu lực không hợp lệ.");
            await services.DoiTrangThaiAsync(AccountId, toaNhaId, dichVuId, active, tuNgay.Value, phienBan);
            TempData["Success"] = "Đã cập nhật lịch thay đổi trạng thái dịch vụ thành công.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Manage), new { toaNhaId, dichVuId });
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> Delete(int toaNhaId, int dichVuId)
    {
        try
        {
            await services.XoaAsync(AccountId, toaNhaId, dichVuId);
            TempData["Success"] = "Đã xóa dịch vụ khỏi tòa nhà thành công.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateException) { TempData["Error"] = "Dịch vụ đang được tham chiếu hoặc vừa thay đổi. Hãy tải lại; có thể ngừng áp dụng thay vì xóa."; }
        return RedirectToAction(nameof(Index), new { toaNhaId });
    }

    [HttpGet, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> Create(int? toaNhaId)
    {
        if (!await services.SanSangAsync()) return RedirectToAction(nameof(Index));
        if (toaNhaId.HasValue && !await services.SoHuuToaNhaAsync(AccountId, toaNhaId.Value)) return Forbid();
        var buildings = await ToaNhasAsync();
        var availableBuildingIds = buildings.Select(x => int.Parse(x.Value)).ToList();
        return View(new TaoDichVuViewModel { DonGia = 0, ToaNhaIds = toaNhaId.HasValue ? [toaNhaId.Value] : [], ToaNhaId = toaNhaId ?? (buildings.Count > 0 ? int.Parse(buildings[0].Value) : (int?)null), ToaNhas = buildings, Phongs = await db.PhongTros.AsNoTracking().Where(x => availableBuildingIds.Contains(x.ToaNhaId)).OrderBy(x => x.MaPhong).ToListAsync() });
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> Create(TaoDichVuViewModel model)
    {
        if (!await services.SanSangAsync()) return RedirectToAction(nameof(Index));
        if (model.ToaNhaId.HasValue && !await services.SoHuuToaNhaAsync(AccountId, model.ToaNhaId.Value)) return Forbid();
        if (!ModelState.IsValid)
        {
            model.ToaNhas = await ToaNhasAsync();
            var allowedIds = model.ToaNhas.Select(x => int.Parse(x.Value)).ToList();
            model.Phongs = await db.PhongTros.AsNoTracking().Where(x => allowedIds.Contains(x.ToaNhaId)).OrderBy(x => x.MaPhong).ToListAsync();
            return View(model);
        }
        try { await services.ThemAsync(AccountId, model); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        TempData["Success"] = "Đã tạo dịch vụ và đơn giá thành công.";
        return RedirectToAction(nameof(Index), new { toaNhaId = model.ToaNhaIds.FirstOrDefault() > 0 ? model.ToaNhaIds.First() : model.ToaNhaId });
    }
}
