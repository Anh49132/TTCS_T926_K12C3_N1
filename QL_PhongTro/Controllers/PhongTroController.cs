using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Authorization;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA,QUAN_LY,ADMIN")]
[ModuleAccess("PHONG_TRO")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PhongTroController(AppDbContext db, DichVuPhongService roomServices, RoomImageStore imageStore, RoomImageDeletionService imageDeletion, ITimeProvider? clock = null) : Controller
{
    [HttpGet, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> DichVuThemPhong(int toaNhaId)
    {
        if (CurrentAccountId() is not { } actor) return Forbid();
        if (!await VisibleBuildings(actor, activeOnly: true).AnyAsync(x => x.Id == toaNhaId)) return Forbid();
        var today = DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7));
        var catalog = await db.DichVuToaNhas.Include(x => x.DichVu).Where(x => x.ToaNhaId == toaNhaId && x.DichVu.DangHoatDong).ToListAsync();
        var prices = await db.CauHinhDichVus.Where(x => x.ToaNhaId == toaNhaId && x.PhongId == null && x.TuNgay <= today && (x.DenNgay == null || x.DenNgay >= today) && x.DangApDung && x.DaChotGia).ToListAsync();
        return Json(catalog.Where(c => prices.Count(p => p.DichVuId == c.DichVuId) == 1).Select(c => {
            var p = prices.Single(p => p.DichVuId == c.DichVuId);
            return new { id = c.Id, name = c.DichVu.TenDichVu, code = c.DichVu.MaDichVu, selected = c.ApDungMacDinh, price = p.DonGia, method = p.CachTinh, unit = p.DonViTinh };
        }));
    }
    [HttpGet]
    public async Task<IActionResult> ChiTietToaNha(int id, string? tuKhoa, int? tang, string? trangThai, int trang = 1, CancellationToken ct = default, string? loaiPhong = null)
    {
        if (CurrentAccountId() is not { } actor) return Forbid();
        if (!await db.ToaNhas.AnyAsync(x => x.Id == id, ct)) return NotFound();
        var building = await VisibleBuildings(actor, activeOnly: false).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (building == null) return Forbid();
        var all = await db.PhongTros.AsNoTracking().Where(x => x.ToaNhaId == id).OrderBy(x => x.MaPhong).ToListAsync(ct);
        var model = new ChiTietToaNhaViewModel { ToaNha = building, TongPhong = all.Count,
            DangThue = all.Count(x => x.TrangThai == "DANG_THUE"), Trong = all.Count(x => x.TrangThai == "TRONG"), DatCoc = all.Count(x => x.TrangThai == "DA_DAT_COC"),
            Tangs = all.Select(x => x.Tang).Distinct().ToList(), TuKhoa = tuKhoa?.Trim(), Tang = tang, TrangThai = trangThai,
            DienTichMin = all.Count == 0 ? null : all.Min(x => x.DienTich), DienTichMax = all.Count == 0 ? null : all.Max(x => x.DienTich),
            QuanLy = await db.TaiKhoans.Where(x => x.Id == building.QuanLyId).Select(x => x.HoTen).SingleOrDefaultAsync(ct),
            Anh = await (from a in db.AnhPhongs join p in db.PhongTros on a.PhongId equals p.Id where p.ToaNhaId == id && !a.DangChoXoa orderby p.Id, a.ThuTu select a.DuongDanAnhNho).FirstOrDefaultAsync(ct) };
        model.LoaiPhong = loaiPhong;
        model.LoaiPhongs = all.Select(x => x.LoaiPhong).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().Order().ToList();
        var matchingRooms = string.IsNullOrWhiteSpace(model.TuKhoa) ? new List<int>() : await (from h in db.HopDongs join k in db.KhachThues on h.KhachDungTenId equals k.Id
            where h.TrangThai == "DANG_HIEU_LUC" && k.HoTen.Contains(model.TuKhoa) && db.KyHopDongs.Any(p => p.HopDongId == h.Id && p.NgayBatDau <= DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7)) && p.NgayKetThuc >= DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7)))
            select h.PhongId).ToListAsync(ct);
        var filtered = all.Where(x => (tang == null || x.Tang == tang) && (string.IsNullOrEmpty(trangThai) || x.TrangThai == trangThai)
            && (string.IsNullOrEmpty(loaiPhong) || x.LoaiPhong == loaiPhong) && (string.IsNullOrEmpty(model.TuKhoa) || x.MaPhong.Contains(model.TuKhoa, StringComparison.OrdinalIgnoreCase) || matchingRooms.Contains(x.Id))).ToList();
        model.TongKetQua = filtered.Count; model.Trang = Math.Clamp(trang, 1, model.TongTrang);
        var rooms = filtered.Skip((model.Trang - 1) * 8).Take(8).ToList(); var ids = rooms.Select(x => x.Id).ToList();
        var today = DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7));
        var contracts = await (from h in db.HopDongs.AsNoTracking() join k in db.KhachThues on h.KhachDungTenId equals k.Id into clients
            from k in clients.DefaultIfEmpty() where ids.Contains(h.PhongId) && h.TrangThai == "DANG_HIEU_LUC"
            && db.KyHopDongs.Any(p => p.HopDongId == h.Id && p.NgayBatDau <= today && p.NgayKetThuc >= today)
            select new { h.Id, h.PhongId, h.MaHopDong, Name = k == null ? null : k.HoTen,
                End = db.KyHopDongs.Where(p => p.HopDongId == h.Id).Max(p => (DateOnly?)p.NgayKetThuc) }).ToListAsync(ct);
        foreach (var room in rooms)
        {
            var contract = contracts.Where(x => x.PhongId == room.Id).OrderByDescending(x => x.Id).FirstOrDefault();
            model.Phongs.Add(new(room, contract?.Name, contract?.Id, contract?.MaHopDong, contract?.End));
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ToaNha(string? tuKhoa)
    {
        var ownerId = CurrentAccountId();
        if (ownerId is null)
            return Forbid();

        var query = VisibleBuildings(ownerId.Value, activeOnly: false);
        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var keyword = tuKhoa.Trim();
            query = query.Where(building => building.TenToaNha.Contains(keyword) || building.DiaChi.Contains(keyword));
        }

        var buildings = await query.OrderBy(building => building.TenToaNha)
            .Select(building => new ToaNhaTongHopViewModel
            {
                Id = building.Id,
                TenToaNha = building.TenToaNha,
                KhuVuc = building.QuanHuyen,
                DiaChi = building.DiaChi,
                QuanLy = db.TaiKhoans.Where(account => account.Id == building.QuanLyId).Select(account => account.HoTen).FirstOrDefault(),
                SoPhong = db.PhongTros.Count(room => room.ToaNhaId == building.Id),
                SoPhongTrong = db.PhongTros.Count(room => room.ToaNhaId == building.Id && room.TrangThai == TrangThaiPhong.TRONG.ToString()),
                SoPhongDangThue = db.PhongTros.Count(room => room.ToaNhaId == building.Id && room.TrangThai == "DANG_THUE"),
                DangHoatDong = building.DangHoatDong
            }).ToListAsync();

        var buildingIds = buildings.Select(x => x.Id).ToList();
        var today = DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7));
        var contractEnds = await (from h in db.HopDongs.AsNoTracking()
            join p in db.PhongTros on h.PhongId equals p.Id
            where buildingIds.Contains(p.ToaNhaId) && h.TrangThai == "DANG_HIEU_LUC"
            select db.KyHopDongs.Where(k => k.HopDongId == h.Id).Max(k => (DateOnly?)k.NgayKetThuc)).ToListAsync();
        return View(new DanhSachToaNhaViewModel { TuKhoa = tuKhoa, ToaNhas = buildings,
            HopDongSapHetHan = contractEnds.Count(end => end >= today && end <= today.AddDays(30)) });
    }

    public async Task<IActionResult> Index(int? toaNhaId, int? quanLyId, TrangThaiPhong? trangThaiFilter, string? tuKhoa)
    {
        var ownerId = CurrentAccountId();
        if (ownerId is null)
            return Forbid();

        var buildingQuery = VisibleBuildings(ownerId.Value, activeOnly: true);
        var managerOptions = await GetManagerOptions();
        if (quanLyId.HasValue && managerOptions.Any(x => x.Value == quanLyId.Value.ToString()))
            buildingQuery = buildingQuery.Where(building => building.QuanLyId == quanLyId);
        else
            quanLyId = null;

        var buildings = await buildingQuery
            .OrderBy(building => building.TenToaNha)
            .Select(building => new SelectListItem(building.TenToaNha, building.Id.ToString()))
            .ToListAsync();

        var selectedId = buildings.Any(building => building.Value == toaNhaId?.ToString())
            ? toaNhaId
            : null;

        var visibleBuildingIds = buildings.Select(building => int.Parse(building.Value)).ToArray();
        var roomQuery = GetRoomQuery(visibleBuildingIds, selectedId, trangThaiFilter);
        var todayForRooms = DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7));
        var currentTenants = await (from h in db.HopDongs.AsNoTracking() join p in db.PhongTros on h.PhongId equals p.Id
            join k in db.KhachThues on h.KhachDungTenId equals k.Id
            where visibleBuildingIds.Contains(p.ToaNhaId) && h.TrangThai == "DANG_HIEU_LUC"
            && db.KyHopDongs.Any(period => period.HopDongId == h.Id && period.NgayBatDau <= todayForRooms && period.NgayKetThuc >= todayForRooms)
            select new { h.PhongId, k.HoTen }).ToListAsync();
        var tenantNames = currentTenants.GroupBy(x => x.PhongId).ToDictionary(x => x.Key, x => x.First().HoTen);
        tuKhoa = tuKhoa?.Trim();
        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var matchingTenantRooms = currentTenants.Where(x => x.HoTen.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase)).Select(x => x.PhongId).ToList();
            roomQuery = roomQuery.Where(room => room.MaPhong.Contains(tuKhoa) || matchingTenantRooms.Contains(room.Id));
        }
        var rooms = await roomQuery.OrderBy(room => room.ToaNhaId).ThenBy(room => room.Tang).ThenBy(room => room.MaPhong).ToListAsync();

        var statusCounts = Enum.GetValues<TrangThaiPhong>()
            .ToDictionary(status => status.ToString(), _ => 0);
        if (visibleBuildingIds.Length > 0)
        {
            var counts = await db.PhongTros.AsNoTracking()
                .Where(room => visibleBuildingIds.Contains(room.ToaNhaId) && (!selectedId.HasValue || room.ToaNhaId == selectedId))
                .GroupBy(room => room.TrangThai)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync();
            foreach (var count in counts)
                statusCounts[count.Status] = count.Count;
        }

        var rentedPrices = await db.PhongTros.AsNoTracking()
            .Where(room => visibleBuildingIds.Contains(room.ToaNhaId) && (!selectedId.HasValue || room.ToaNhaId == selectedId) && room.TrangThai == "DANG_THUE")
            .Select(room => room.GiaThue).ToListAsync();
        return View(new DanhSachPhongViewModel
        {
            ToaNhaId = selectedId,
            QuanLyId = quanLyId,
            TuKhoa = tuKhoa,
            TrangThaiFilter = trangThaiFilter,
            ToaNhaOptions = buildings,
            QuanLyOptions = managerOptions,
            TrangThaiOptions = GetStatusOptions(),
            SoLuongTheoTrangThai = statusCounts,
            PhongTros = rooms,
            DoanhThuPhongDangThue = rentedPrices.Sum(price => (decimal)price),
            KhachTheoPhong = tenantNames
        });
    }

    [HttpGet]
    [ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> Create(int? toaNhaId)
    {
        var ownerId = CurrentAccountId();
        if (ownerId is null)
            return Forbid();

        var options = await GetBuildingOptions(ownerId.Value);
        if (options.Count == 0)
        {
            TempData["RoomWarning"] = "Hãy khai báo tòa nhà trước khi thêm phòng.";
            return RedirectToAction(nameof(TaoToaNha));
        }

        var selectedBuildingId = options.Any(building => building.Value == toaNhaId?.ToString())
            ? toaNhaId
            : null;

        return View(new TaoPhongViewModel
        {
            ToaNhaId = selectedBuildingId,
            GiaThueDisplay = "0",
            ToaNhaOptions = options
        });
    }

    [HttpGet]
    [ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> CreateBulk(int? toaNhaId, int? soTang)
    {
        var ownerId = CurrentAccountId();
        if (ownerId is null)
            return Forbid();

        var options = await GetBuildingOptions(ownerId.Value);
        if (options.Count == 0)
        {
            TempData["RoomWarning"] = "Hãy khai báo tòa nhà trước khi tạo phòng hàng loạt.";
            return RedirectToAction(nameof(TaoToaNha));
        }

        var selectedBuildingId = options.Any(building => building.Value == toaNhaId?.ToString())
            ? toaNhaId
            : null;

        return View(new TaoPhongHangLoatViewModel
        {
            ToaNhaId = selectedBuildingId,
            SoTang = soTang is >= 1 and <= 99 ? soTang : null,
            ToaNhaOptions = options
        });
    }

    [HttpPost]
    [ModuleAccess("PHONG_TRO", write: true)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(45 * 1024 * 1024)]
    public async Task<IActionResult> Create(TaoPhongViewModel model)
    {
        var ownerId = CurrentAccountId();
        if (ownerId is null)
            return Forbid();

        if (model.TrangThai is not null && !Enum.IsDefined(model.TrangThai.Value))
            ModelState.AddModelError(nameof(model.TrangThai), "Trạng thái phòng không hợp lệ.");
        if (model.TienNghi.Any(x => !TienNghiPhong.Options.ContainsKey(x)))
            ModelState.AddModelError(nameof(model.TienNghi), "Tiện nghi đã chọn không hợp lệ.");
        BindRentPrice(nameof(model.GiaThue), model.GiaThueDisplay, price => model.GiaThue = price);

        var building = model.ToaNhaId is null
            ? null
            : await db.ToaNhas.SingleOrDefaultAsync(item =>
                item.Id == model.ToaNhaId && (item.ChuNhaId == ownerId || IsAdmin) && item.DangHoatDong);

        if (building is null)
            ModelState.AddModelError(nameof(model.ToaNhaId), "Tòa nhà không hợp lệ.");

        var roomCode = model.MaPhong?.Trim() ?? string.Empty;
        if (building is not null && roomCode.Length > 0 && await db.PhongTros.AsNoTracking()
                .AnyAsync(room => room.ToaNhaId == building.Id && room.MaPhong == roomCode))
            ModelState.AddModelError(nameof(model.MaPhong), "Mã phòng đã được sử dụng trong tòa nhà này.");

        var preparedImages = new List<PreparedRoomImage>();
        var selectedServices = new List<(DichVuToaNha Catalog, long Price, string Method)>();
        if (model.DichVuDaChon && building is not null)
        {
            var ids = model.DichVuMoi.Select(x => x.CatalogId).ToList();
            var catalog = await db.DichVuToaNhas.Include(x => x.DichVu).Where(x => x.ToaNhaId == building.Id && x.DichVu.DangHoatDong).ToListAsync();
            if (ids.Distinct().Count() != ids.Count || ids.Any(id => catalog.All(c => c.Id != id)))
                ModelState.AddModelError(nameof(model.DichVuMoi), "Danh sách dịch vụ không hợp lệ cho cơ sở này.");
            foreach (var item in model.DichVuMoi.Where(x => x.ApDung))
            {
                var c = catalog.SingleOrDefault(x => x.Id == item.CatalogId);
                if (c is null) continue;
                if (!TryParseRentPrice(item.Gia, out var price) || price < 0 || !CachTinhDichVu.HopLe(item.CachTinh)
                    || ((c.DichVu.MaDichVu is "DIEN" or "NUOC") && price <= 0)
                    || ((c.DichVu.MaDichVu is "DIEN" or "NUOC") && item.CachTinh == CachTinhDichVu.CoDinh)
                    || ((c.DichVu.MaDichVu is not ("DIEN" or "NUOC")) && item.CachTinh == CachTinhDichVu.TheoChiSo))
                    ModelState.AddModelError(nameof(model.DichVuMoi), $"Đơn giá hoặc cách tính {c.DichVu.TenDichVu} không hợp lệ.");
                else selectedServices.Add((c, price, item.CachTinh));
            }
        }
        if (model.AnhPhongMoi.Count > RoomImageStore.MaxImagesPerRoom)
            ModelState.AddModelError(nameof(model.AnhPhongMoi), "Mỗi phòng chỉ được tối đa 8 ảnh.");
        if (ModelState.IsValid)
            foreach (var file in model.AnhPhongMoi)
            {
                try { preparedImages.Add(await imageStore.PrepareAsync(file, HttpContext.RequestAborted)); }
                catch (InvalidDataException ex) { ModelState.AddModelError(nameof(model.AnhPhongMoi), ex.Message); }
            }
        if (!ModelState.IsValid)
        {
            model.ToaNhaOptions = await GetBuildingOptions(ownerId.Value);
            return View(model);
        }

        var room = new PhongTro
        {
            ToaNhaId = building!.Id,
            MaPhong = roomCode,
            Tang = model.Tang!.Value,
            DienTich = model.DienTich!.Value,
            GiaThue = model.GiaThue!.Value,
            SoNguoiToiDa = model.SoNguoiToiDa!.Value,
            TrangThai = model.TrangThai!.Value.ToString(),
            TienNghi = System.Text.Json.JsonSerializer.Serialize(model.TienNghi.Distinct().Order()),
            GhiChuNoiBo = string.IsNullOrWhiteSpace(model.GhiChuNoiBo) ? null : model.GhiChuNoiBo.Trim(),
            NgayTao = DateTime.UtcNow
        };

        await using var createTransaction = await db.Database.BeginTransactionAsync();
        var storedImages = new List<StoredRoomImage>();
        db.PhongTros.Add(room);
        if (!model.DichVuDaChon) await roomServices.GanMacDinhChoPhongMoiAsync([room]);
        try
        {
            await db.SaveChangesAsync();
            foreach (var service in selectedServices)
            {
                db.DichVuPhongs.Add(new DichVuPhong { PhongId = room.Id, DichVuToaNhaId = service.Catalog.Id, DonGiaRieng = service.Price });
                db.CauHinhDichVus.Add(new CauHinhDichVu { ToaNhaId = building.Id, PhongId = room.Id, DichVuId = service.Catalog.DichVuId,
                    DonGia = service.Price, CachTinh = service.Method,
                    DonViTinh = service.Method == CachTinhDichVu.TheoNguoi ? "người/tháng" : service.Method == CachTinhDichVu.CoDinh ? "phòng/tháng" : service.Catalog.DichVu.MaDichVu == "DIEN" ? "kWh" : "m³",
                    TuNgay = DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7)), NguoiTaoId = ownerId.Value, NgayTao = (clock ?? new SystemTimeProvider()).UtcNow });
            }
            foreach (var prepared in preparedImages)
            {
                var stored = await imageStore.SaveAsync(room.Id, prepared, HttpContext.RequestAborted);
                storedImages.Add(stored);
                db.AnhPhongs.Add(new AnhPhong { PhongId = room.Id, DuongDan = stored.OriginalPath,
                    DuongDanAnhNho = stored.ThumbnailPath, ThuTu = storedImages.Count, NgayTao = DateTime.UtcNow });
            }
            if (storedImages.Count > 0 || selectedServices.Count > 0) await db.SaveChangesAsync();
            await createTransaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (IsRoomCodeConflict(exception))
        {
            await createTransaction.RollbackAsync();
            foreach (var stored in storedImages) imageStore.Delete(stored);
            db.Entry(room).State = EntityState.Detached;
            ModelState.AddModelError(nameof(model.MaPhong), "Mã phòng đã được sử dụng trong tòa nhà này.");
            model.ToaNhaOptions = await GetBuildingOptions(ownerId.Value);
            return View(model);
        }
        catch
        {
            await createTransaction.RollbackAsync();
            foreach (var stored in storedImages) imageStore.Delete(stored);
            throw;
        }

        TempData["Success"] = "Đã thêm phòng thành công.";
        return RedirectToAction(nameof(Index), new { toaNhaId = building.Id });
    }

    [HttpPost]
    [ModuleAccess("PHONG_TRO", write: true)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBulk(TaoPhongHangLoatViewModel model)
    {
        var ownerId = CurrentAccountId();
        if (ownerId is null)
            return Forbid();

        if (model.TrangThai is not null && !Enum.IsDefined(model.TrangThai.Value))
            ModelState.AddModelError(nameof(model.TrangThai), "Trạng thái phòng không hợp lệ.");
        BindRentPrice(nameof(model.GiaThue), model.GiaThueDisplay, price => model.GiaThue = price);
        long deposit = 0;
        if (!string.IsNullOrWhiteSpace(model.TienCocDisplay) && !TryParseRentPrice(model.TienCocDisplay, out deposit))
            ModelState.AddModelError(nameof(model.TienCocDisplay), "Tiền cọc phải là số nguyên đồng không âm.");
        var description = string.Join("\n", new[]
        {
            string.IsNullOrWhiteSpace(model.TienIch) ? null : $"Tiện ích: {model.TienIch.Trim()}",
            string.IsNullOrWhiteSpace(model.NoiThat) ? null : $"Nội thất: {model.NoiThat.Trim()}",
            string.IsNullOrWhiteSpace(model.GhiChuPhong) ? null : $"Ghi chú: {model.GhiChuPhong.Trim()}"
        }.Where(value => value is not null));

        var building = model.ToaNhaId is null
            ? null
            : await db.ToaNhas.SingleOrDefaultAsync(item =>
                item.Id == model.ToaNhaId && (item.ChuNhaId == ownerId || IsAdmin) && item.DangHoatDong);

        if (building is null)
            ModelState.AddModelError(nameof(model.ToaNhaId), "Tòa nhà không hợp lệ.");

        if (!ModelState.IsValid)
        {
            model.ToaNhaOptions = await GetBuildingOptions(ownerId.Value);
            return View(model);
        }

        var roomCodes = Enumerable.Range(1, model.SoTang!.Value)
            .SelectMany(floor => Enumerable.Range(1, model.SoPhongMoiTang!.Value)
                .Select(roomNumber => $"{floor}{roomNumber:00}"))
            .ToList();

        var generatedCodeSet = roomCodes.ToHashSet(StringComparer.Ordinal);
        var existingCodes = await db.PhongTros.AsNoTracking()
            .Where(room => room.ToaNhaId == building!.Id)
            .Select(room => room.MaPhong)
            .ToListAsync();
        var existingCode = existingCodes.FirstOrDefault(generatedCodeSet.Contains);

        if (existingCode is not null)
        {
            ModelState.AddModelError(string.Empty,
                $"Không tạo phòng: mã {existingCode} đã tồn tại trong tòa nhà. Không có phòng nào trong lô được lưu.");
            model.ToaNhaOptions = await GetBuildingOptions(ownerId.Value);
            return View(model);
        }

        var createdAt = DateTime.UtcNow;
        var rooms = roomCodes.Select(code => new PhongTro
        {
            ToaNhaId = building!.Id,
            MaPhong = code,
            Tang = int.Parse(code[..^2]),
            DienTich = model.DienTich!.Value,
            GiaThue = model.GiaThue!.Value,
            TienCocDuKien = deposit,
            MoTa = string.IsNullOrEmpty(description) ? null : description,
            SoNguoiToiDa = model.SoNguoiToiDa ?? 1,
            TrangThai = model.TrangThai!.Value.ToString(),
            NgayTao = createdAt
        }).ToList();

        db.PhongTros.AddRange(rooms);
        await roomServices.GanMacDinhChoPhongMoiAsync(rooms);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsRoomCodeConflict(exception))
        {
            foreach (var room in rooms)
                db.Entry(room).State = EntityState.Detached;

            ModelState.AddModelError(string.Empty,
                "Không tạo phòng vì một mã vừa được sử dụng trong tòa nhà. Không có phòng nào trong lô được lưu.");
            model.ToaNhaOptions = await GetBuildingOptions(ownerId.Value);
            return View(model);
        }

        TempData["Success"] = $"Đã tạo {rooms.Count} phòng thành công.";
        if (model.TaoTiep)
            return RedirectToAction(nameof(CreateBulk), new { toaNhaId = building!.Id, soTang = model.SoTang });
        return RedirectToAction(nameof(Index), new { toaNhaId = building!.Id });
    }

    [HttpGet]
    [ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> TaoToaNha()
    {
        if (CurrentAccountId() is null)
            return Forbid();

        return View(new TaoToaNhaViewModel { QuanLyOptions = await GetManagerOptions() });
    }

    [HttpPost]
    [ModuleAccess("PHONG_TRO", write: true)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoToaNha(TaoToaNhaViewModel model)
    {
        var ownerId = CurrentAccountId();
        if (ownerId is null)
            return Forbid();

        model.QuanLyOptions = await GetManagerOptions();
        ValidateManager(model);
        if (!ModelState.IsValid)
            return View(model);

        var building = new ToaNha
        {
            ChuNhaId = ownerId.Value,
            TenToaNha = model.TenToaNha.Trim(),
            DiaChi = model.DiaChi.Trim(),
            PhuongXa = model.PhuongXa?.Trim(),
            QuanHuyen = model.QuanHuyen?.Trim(),
            TinhThanh = model.TinhThanh?.Trim(),
            SoTang = model.SoTang,
            QuanLyId = model.QuanLyId,
            GhiChu = model.GhiChu?.Trim(),
            NgayChotHangThang = 1,
            DangHoatDong = true
        };

        db.ToaNhas.Add(building);
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã thêm tòa nhà thành công.";
        return RedirectToAction(nameof(Create), new { toaNhaId = building.Id });
    }

    [HttpGet]
    [ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> SuaToaNha(int id)
    {
        var building = await GetOwnedBuilding(id);
        if (building is null)
            return NotFound();

        var model = new TaoToaNhaViewModel
        {
            TenToaNha = building.TenToaNha,
            DiaChi = building.DiaChi,
            PhuongXa = building.PhuongXa,
            QuanHuyen = building.QuanHuyen,
            TinhThanh = building.TinhThanh,
            SoTang = building.SoTang,
            DienTichDat = building.DienTichDat,
            ThangMay = building.ThangMay,
            BaiDoXe = building.BaiDoXe,
            CameraAnNinh = building.CameraAnNinh,
            BaoVe24h = building.BaoVe24h,
            KhuGiatSay = building.KhuGiatSay,
            SanThuong = building.SanThuong,

            QuanLyId = building.QuanLyId,
            GhiChu = building.GhiChu,
            QuanLyOptions = await GetManagerOptions()
        };
        await PopulateBuildingEdit(model, building);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> SuaToaNha(int id, TaoToaNhaViewModel model)
    {
        var building = await GetOwnedBuilding(id);
        if (building is null)
            return NotFound();

        model.QuanLyOptions = await GetManagerOptions();
        ValidateManager(model);
        if (!ModelState.IsValid)
        {
            await PopulateBuildingEdit(model, building);
            return View(model);
        }

        building.TenToaNha = model.TenToaNha.Trim();
        building.DiaChi = model.DiaChi.Trim();
        building.PhuongXa = model.PhuongXa?.Trim();
        building.QuanHuyen = model.QuanHuyen?.Trim();
        building.TinhThanh = model.TinhThanh?.Trim();
        building.SoTang = model.SoTang;
        building.DienTichDat = model.DienTichDat;
        building.ThangMay = model.ThangMay;
        building.BaiDoXe = model.BaiDoXe;
        building.CameraAnNinh = model.CameraAnNinh;
        building.BaoVe24h = model.BaoVe24h;
        building.KhuGiatSay = model.KhuGiatSay;
        building.SanThuong = model.SanThuong;

        building.QuanLyId = model.QuanLyId;
        building.GhiChu = model.GhiChu?.Trim();
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật tòa nhà thành công.";
        return RedirectToAction(nameof(ToaNha));
    }

    private async Task PopulateBuildingEdit(TaoToaNhaViewModel model, ToaNha building)
    {
        var rooms = await db.PhongTros.AsNoTracking().Where(x => x.ToaNhaId == building.Id).ToListAsync();
        model.DangHoatDongHienTai = building.DangHoatDong;
        model.TongPhongHienTai = rooms.Count;
        model.DangThueHienTai = rooms.Count(x => x.TrangThai == "DANG_THUE");
        model.TongDienTichHienTai = rooms.Sum(x => x.DienTich);
        model.AnhHienTai = await (from a in db.AnhPhongs join p in db.PhongTros on a.PhongId equals p.Id
            where p.ToaNhaId == building.Id && !a.DangChoXoa orderby p.Id, a.ThuTu select a.DuongDanAnhNho).FirstOrDefaultAsync();
        model.PhongAnhHienTaiId = await (from a in db.AnhPhongs join p in db.PhongTros on a.PhongId equals p.Id
            where p.ToaNhaId == building.Id && !a.DangChoXoa orderby p.Id, a.ThuTu select (int?)p.Id).FirstOrDefaultAsync()
            ?? rooms.OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefault();
        model.DienThoaiQuanLyHienTai = await db.TaiKhoans.Where(x => x.Id == building.QuanLyId).Select(x => x.SoDienThoai).FirstOrDefaultAsync();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> XoaToaNha(int id)
    {
        var building = await GetOwnedBuilding(id);
        if (building is null)
            return NotFound();

        if (await db.PhongTros.AnyAsync(room => room.ToaNhaId == id))
        {
            TempData["Error"] = "Không thể xóa tòa nhà đang có phòng. Hãy chuyển sang ngừng hoạt động nếu không còn sử dụng.";
            return RedirectToAction(nameof(ToaNha));
        }

        db.ToaNhas.Remove(building);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Không thể xóa tòa nhà đang được sử dụng hoặc vừa thay đổi. Vui lòng tải lại danh sách.";
            return RedirectToAction(nameof(ToaNha));
        }
        TempData["Success"] = "Đã xóa tòa nhà thành công.";
        return RedirectToAction(nameof(ToaNha));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> NgungHoatDong(int id)
    {
        var building = await GetOwnedBuilding(id);
        if (building is null)
            return NotFound();

        building.DangHoatDong = false;
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã chuyển tòa nhà sang ngừng hoạt động.";
        return RedirectToAction(nameof(ToaNha));
    }

    private int? CurrentAccountId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ? accountId : null;

    private void ValidateManager(TaoToaNhaViewModel model)
    {
        if (model.QuanLyId is not null && !model.QuanLyOptions.Any(x => x.Value == model.QuanLyId.ToString()))
            ModelState.AddModelError(nameof(model.QuanLyId), "Vui lòng chọn người quản lý đang hoạt động trong danh sách.");
    }

    [HttpGet, ModuleAccess("PHONG_TRO")]
    public async Task<IActionResult> Details(int id)
    {
        var room = await db.PhongTros.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (room is null) return NotFound();
        var accountId = CurrentAccountId();
        if (accountId is null) return Forbid();
        var building = await VisibleBuildings(accountId.Value, activeOnly: false)
            .SingleOrDefaultAsync(x => x.Id == room.ToaNhaId);
        if (building is null) return Forbid();
        return View(new ChiTietPhongViewModel { Phong = room, ToaNha = building, Anh = await GetRoomImagesAsync(id) });
    }

    [HttpGet, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> Edit(int id)
    {
        var room = await GetOwnedRoom(id);
        if (room is null) return NotFound();
        ViewData["RoomId"] = id;
        ViewData["RoomServices"] = await roomServices.XemAsync(CurrentAccountId()!.Value, id);
        return View("Create", new TaoPhongViewModel
        {
            ToaNhaId = room.ToaNhaId, MaPhong = room.MaPhong, Tang = room.Tang,
            DienTich = room.DienTich, GiaThue = room.GiaThue,
            GhiChuNoiBo = room.GhiChuNoiBo,
            TienNghi = string.IsNullOrWhiteSpace(room.TienNghi) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<string>>(room.TienNghi) ?? [],
            GiaThueDisplay = room.GiaThue.ToString(CultureInfo.InvariantCulture),
            SoNguoiToiDa = room.SoNguoiToiDa, TrangThai = Enum.Parse<TrangThaiPhong>(room.TrangThai),
            ToaNhaOptions = await GetBuildingOptions(CurrentAccountId()!.Value),
            Anh = await GetRoomImagesAsync(id)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> Edit(int id, TaoPhongViewModel model)
    {
        var room = await GetOwnedRoom(id);
        if (room is null) return NotFound();
        ViewData["RoomId"] = id;
        ViewData["RoomServices"] = await roomServices.XemAsync(CurrentAccountId()!.Value, id);
        model.ToaNhaOptions = await GetBuildingOptions(CurrentAccountId()!.Value);
        if (model.TienNghi.Any(x => !TienNghiPhong.Options.ContainsKey(x)))
            ModelState.AddModelError(nameof(model.TienNghi), "Tiện nghi đã chọn không hợp lệ.");
        if (model.ToaNhaId != room.ToaNhaId)
            ModelState.AddModelError(nameof(model.ToaNhaId), "Không thể chuyển phòng sang tòa nhà khác.");
        BindRentPrice(nameof(model.GiaThue), model.GiaThueDisplay, price => model.GiaThue = price);
        var code = model.MaPhong?.Trim() ?? string.Empty;
        if (await db.PhongTros.AnyAsync(x => x.ToaNhaId == room.ToaNhaId && x.MaPhong == code && x.Id != id))
            ModelState.AddModelError(nameof(model.MaPhong), "Mã phòng đã được sử dụng trong tòa nhà này.");
        if (!ModelState.IsValid)
        {
            model.Anh = await GetRoomImagesAsync(id);
            return View("Create", model);
        }
        room.MaPhong = code;
        room.Tang = model.Tang!.Value;
        room.DienTich = model.DienTich!.Value;
        room.GiaThue = model.GiaThue!.Value;
        room.SoNguoiToiDa = model.SoNguoiToiDa!.Value;
        room.TrangThai = model.TrangThai!.Value.ToString();
        room.GhiChuNoiBo = string.IsNullOrWhiteSpace(model.GhiChuNoiBo) ? null : model.GhiChuNoiBo.Trim();
        room.TienNghi = System.Text.Json.JsonSerializer.Serialize(model.TienNghi.Distinct().Order());
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            ModelState.AddModelError(string.Empty, IsRoomCodeConflict(ex)
                ? "Mã phòng đã được sử dụng trong tòa nhà này."
                : "Không thể cập nhật phòng lúc này. Vui lòng thử lại.");
            model.Anh = await GetRoomImagesAsync(id);
            return View("Create", model);
        }
        TempData["Success"] = "Đã cập nhật phòng thành công.";
        return RedirectToAction(nameof(Index), new { toaNhaId = room.ToaNhaId });
    }

    [HttpGet]
    public async Task<IActionResult> NhatKyPhong(int phongId, DateOnly? tuNgay, DateOnly? denNgay, int page = 1)
    {
        var ownerId = CurrentAccountId();
        if (ownerId is null) return Forbid();
        var room = await db.PhongTros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == phongId);
        if (room is null) return NotFound();
        var building = await db.ToaNhas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == room.ToaNhaId);
        if (building is null || (building.ChuNhaId != ownerId.Value && !IsAdmin)) return Forbid();
        if (tuNgay > denNgay ||
            tuNgay?.Year < 1900 || tuNgay?.Year > 9998 ||
            denNgay?.Year < 1900 || denNgay?.Year > 9998)
            return BadRequest("Bộ lọc không hợp lệ. Ngày từ phải không sau ngày đến (1900–9998).");

        var hopDongIds = await db.HopDongs.AsNoTracking()
            .Where(hopDong => hopDong.PhongId == phongId)
            .Select(hopDong => hopDong.Id)
            .ToListAsync();
        var query = db.NhatKyHoatDongs.AsNoTracking()
            .Where(row => (row.LoaiDoiTuong == "phong_tro" && row.DoiTuongId == phongId)
                || (row.LoaiDoiTuong == "hop_dong" && hopDongIds.Contains(row.DoiTuongId)));
        if (tuNgay is { } from)
            query = query.Where(row => row.ThoiDiem >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7));
        if (denNgay is { } to)
            query = query.Where(row => row.ThoiDiem < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7));

        var model = new NhatKyPhongViewModel
        {
            PhongId = phongId,
            MaPhong = room.MaPhong,
            TenToaNha = building.TenToaNha,
            TrangThaiPhong = Enum.TryParse<TrangThaiPhong>(room.TrangThai, out var trangThai)
                ? StatusLabel(trangThai) : room.TrangThai,
            TuNgay = tuNgay,
            DenNgay = denNgay
        };
        model.Total = await query.CountAsync();
        model.Page = Math.Clamp(page, 1, model.Pages);
        model.Rows = await query.OrderByDescending(row => row.ThoiDiem).ThenByDescending(row => row.Id)
            .Skip((model.Page - 1) * 20).Take(20).ToListAsync();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    [RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (CurrentAccountId() is not { } ownerId)
            return Forbid();
        if (await GetOwnedRoom(id) is null)
            return NotFound();

        PreparedRoomImage prepared;
        try
        {
            prepared = await imageStore.PrepareAsync(file, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        await db.Database.OpenConnectionAsync(cancellationToken);
        using var sqliteTransaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction);
        StoredRoomImage? stored = null;
        try
        {
            var roomIsOwned = await db.PhongTros.AsNoTracking().AnyAsync(room => room.Id == id
                && db.ToaNhas.Any(building => building.Id == room.ToaNhaId
                    && (building.ChuNhaId == ownerId || IsAdmin) && building.DangHoatDong), cancellationToken);
            if (!roomIsOwned)
                return NotFound();

            var count = await db.AnhPhongs.CountAsync(image => image.PhongId == id, cancellationToken);
            if (count >= RoomImageStore.MaxImagesPerRoom)
                return Conflict(new { message = "Phòng đã đủ 8 ảnh. Không thể tải thêm." });

            var nextOrder = (await db.AnhPhongs.Where(image => image.PhongId == id)
                .Select(image => (int?)image.ThuTu).MaxAsync(cancellationToken) ?? 0) + 1;
            stored = await imageStore.SaveAsync(id, prepared, cancellationToken);
            var image = new AnhPhong
            {
                PhongId = id,
                DuongDan = stored.OriginalPath,
                DuongDanAnhNho = stored.ThumbnailPath,
                ThuTu = nextOrder,
                NgayTao = DateTime.UtcNow
            };
            db.AnhPhongs.Add(image);
            await db.SaveChangesAsync(cancellationToken);
            await transaction!.CommitAsync(cancellationToken);
            return Ok(new
            {
                id = image.Id,
                order = image.ThuTu,
                originalPath = image.DuongDan,
                thumbnailPath = image.DuongDanAnhNho,
                count = count + 1
            });
        }
        catch (DbUpdateException)
        {
            try { await transaction!.RollbackAsync(CancellationToken.None); } catch { }
            imageStore.Delete(stored);
            return Conflict(new { message = "Không thể lưu ảnh vào phòng lúc này. Vui lòng thử lại." });
        }
        catch
        {
            try { await transaction!.RollbackAsync(CancellationToken.None); } catch { }
            imageStore.Delete(stored);
            throw;
        }
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> ReorderImages(int id, [FromBody] List<int>? imageIds, CancellationToken cancellationToken)
    {
        if (CurrentAccountId() is not { } ownerId)
            return Forbid();
        if (imageIds is null || imageIds.Count > RoomImageStore.MaxImagesPerRoom)
            return BadRequest(new { message = "Danh sách thứ tự ảnh không hợp lệ. Vui lòng tải lại trang." });

        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        using var sqliteTransaction = connection.BeginTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction);

        var roomIsOwned = await db.PhongTros.AsNoTracking().AnyAsync(room => room.Id == id
            && db.ToaNhas.Any(building => building.Id == room.ToaNhaId
                && (building.ChuNhaId == ownerId || IsAdmin) && building.DangHoatDong), cancellationToken);
        if (!roomIsOwned)
            return NotFound();

        var currentImages = await db.AnhPhongs.AsNoTracking()
            .Where(image => image.PhongId == id)
            .OrderBy(image => image.ThuTu)
            .ToListAsync(cancellationToken);
        var submittedIds = imageIds.ToHashSet();
        if (imageIds.Count != currentImages.Count || submittedIds.Count != imageIds.Count
            || !submittedIds.SetEquals(currentImages.Select(image => image.Id)))
            return Conflict(new { message = "Danh sách ảnh đã thay đổi ở một tab khác. Tải lại trang rồi thử lại." });

        await using var command = connection.CreateCommand();
        command.Transaction = sqliteTransaction;
        try
        {
            command.CommandText = "PRAGMA ignore_check_constraints=ON";
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "UPDATE anh_phong SET thu_tu=thu_tu+8 WHERE phong_id=$roomId";
            command.Parameters.AddWithValue("$roomId", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.Parameters.Clear();
            command.CommandText = "UPDATE anh_phong SET thu_tu=$order WHERE id=$imageId AND phong_id=$roomId";
            for (var index = 0; index < imageIds.Count; index++)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$order", index + 1);
                command.Parameters.AddWithValue("$imageId", imageIds[index]);
                command.Parameters.AddWithValue("$roomId", id);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            command.Parameters.Clear();
            command.CommandText = "PRAGMA ignore_check_constraints=OFF";
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        await transaction!.CommitAsync(cancellationToken);
        return Ok(new { imageIds });
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> DeleteImage(int id, int imageId, CancellationToken cancellationToken)
    {
        if (CurrentAccountId() is not { } ownerId)
            return Forbid();

        var result = await imageDeletion.DeleteOwnedImageAsync(id, imageId, ownerId, cancellationToken);
        return result.Status switch
        {
            RoomImageDeleteStatus.NotFound => NotFound(),
            RoomImageDeleteStatus.StorageFailed => StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = result.Message,
                images = result.Images,
                count = result.Images.Count
            }),
            _ => Ok(new
            {
                images = result.Images,
                count = result.Images.Count
            })
        };
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("PHONG_TRO", write: true)]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await GetOwnedRoom(id);
        if (room is null) return NotFound();
        if (room.TrangThai is "DANG_THUE" or "DA_DAT_COC")
        {
            TempData["Error"] = "Không thể xóa phòng đang thuê hoặc đã đặt cọc.";
            return RedirectToAction(nameof(Index), new { toaNhaId = room.ToaNhaId });
        }
        db.PhongTros.Remove(room);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Không thể xóa phòng đang được hợp đồng hoặc dữ liệu khác sử dụng. Vui lòng tải lại danh sách.";
            return RedirectToAction(nameof(Index), new { toaNhaId = room.ToaNhaId });
        }
        TempData["Success"] = "Đã xóa phòng thành công.";
        return RedirectToAction(nameof(Index), new { toaNhaId = room.ToaNhaId });
    }

    private Task<PhongTro?> GetOwnedRoom(int id) => db.PhongTros.SingleOrDefaultAsync(room =>
        room.Id == id && db.ToaNhas.Any(building => building.Id == room.ToaNhaId &&
            (building.ChuNhaId == CurrentAccountId() || IsAdmin) && building.DangHoatDong));

    private Task<List<AnhPhongQuanLyViewModel>> GetRoomImagesAsync(int roomId) => db.AnhPhongs.AsNoTracking()
        .Where(image => image.PhongId == roomId)
        .OrderBy(image => image.ThuTu)
        .Select(image => new AnhPhongQuanLyViewModel
        {
            Id = image.Id,
            DuongDan = image.DuongDan,
            DuongDanAnhNho = image.DuongDanAnhNho,
            ThuTu = image.ThuTu
        }).ToListAsync();

    private IQueryable<PhongTro> GetRoomQuery(int[] buildingIds, int? buildingId, TrangThaiPhong? status)
    {
        var query = db.PhongTros.AsNoTracking().Where(room => buildingIds.Contains(room.ToaNhaId)
            && (!buildingId.HasValue || room.ToaNhaId == buildingId));
        if (status is not null && Enum.IsDefined(status.Value))
            query = query.Where(room => room.TrangThai == status.Value.ToString());

        return query;
    }

    private void BindRentPrice(string propertyName, string? displayValue, Action<long?> setPrice)
    {
        ModelState.Remove(propertyName);
        if (!TryParseRentPrice(displayValue, out var price))
        {
            setPrice(null);
            ModelState.AddModelError(propertyName, "Giá thuê phải là số nguyên từ 500.000 VND trở lên.");
            return;
        }

        setPrice(price);
        if (price < 500000)
            ModelState.AddModelError(propertyName, "Giá thuê tối thiểu là 500.000 VND.");
    }

    private static bool TryParseRentPrice(string? input, out long price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var value = input.Trim();
        if (!Regex.IsMatch(value, @"^\d+$") && !Regex.IsMatch(value, @"^\d{1,3}(?:\.\d{3})+$"))
            return false;

        return long.TryParse(value.Replace(".", string.Empty), NumberStyles.None, CultureInfo.InvariantCulture, out price);
    }

    private static bool IsRoomCodeConflict(DbUpdateException exception) =>
        exception.GetBaseException() is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        };

    private static IReadOnlyList<SelectListItem> GetStatusOptions() =>
        Enum.GetValues<TrangThaiPhong>()
            .Select(status => new SelectListItem(StatusLabel(status), status.ToString()))
            .ToList();

    public static string StatusLabel(TrangThaiPhong status) => status switch
    {
        TrangThaiPhong.TRONG => "Trống",
        TrangThaiPhong.DA_DAT_COC => "Đã đặt cọc",
        TrangThaiPhong.DANG_THUE => "Đang thuê",
        TrangThaiPhong.NGUNG_CHO_THUE => "Ngừng cho thuê",
        _ => status.ToString()
    };

    private async Task<List<SelectListItem>> GetBuildingOptions(int ownerId)
    {
        var buildings = await VisibleBuildings(ownerId, activeOnly: true)
            .OrderBy(building => building.TenToaNha)
            .Select(building => new { building.Id, building.TenToaNha, building.QuanHuyen, building.TinhThanh, building.SoTang })
            .ToListAsync();
        ViewData["RoomBuildingLocations"] = buildings.ToDictionary(building => building.Id.ToString(),
            building => string.Join(", ", new[] { building.QuanHuyen, building.TinhThanh }.Where(value => !string.IsNullOrWhiteSpace(value))));
        ViewData["RoomBuildingFloors"] = buildings.ToDictionary(building => building.Id.ToString(), building => building.SoTang);
        return buildings.Select(building => new SelectListItem(building.TenToaNha, building.Id.ToString())).ToList();
    }

    private Task<ToaNha?> GetOwnedBuilding(int id) => db.ToaNhas
        .SingleOrDefaultAsync(building => building.Id == id && (building.ChuNhaId == CurrentAccountId() || IsAdmin));

    private bool IsAdmin => User.IsInRole("ADMIN");

    private IQueryable<ToaNha> VisibleBuildings(int accountId, bool activeOnly)
    {
        var query = db.ToaNhas.AsNoTracking();
        if (activeOnly) query = query.Where(building => building.DangHoatDong);
        if (User.IsInRole("ADMIN")) return query;
        if (User.IsInRole("QUAN_LY")) return query.Where(building => building.QuanLyId == accountId);
        return query.Where(building => building.ChuNhaId == accountId);
    }

    private Task<List<SelectListItem>> GetManagerOptions() => db.TaiKhoans.AsNoTracking()
        .Where(account => account.VaiTro == "QUAN_LY" && account.DangHoatDong)
        .OrderBy(account => account.HoTen)
        .Select(account => new SelectListItem(account.HoTen, account.Id.ToString()))
        .ToListAsync();
}
