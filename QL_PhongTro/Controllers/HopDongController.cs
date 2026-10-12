using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using QL_PhongTro.Services;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[Authorize, ModuleAccess("HOP_DONG")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class HopDongController(AppDbContext db, ITimeProvider? clock = null, PermissionService? permissions = null) : Controller
{
    private int AccountId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private DateOnly HomNay => DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7));
    private async Task<bool> IsAdmin(CancellationToken ct) => await db.TaiKhoans.AnyAsync(x => x.Id == AccountId && x.VaiTro == "ADMIN" && x.DangHoatDong && !x.IsDeleted, ct);
    private async Task<bool> ForeignRequest(int id, CancellationToken ct) => !await IsAdmin(ct) && await
        (from y in db.YeuCauThues join t in db.TinDangs on y.TinDangId equals t.Id
         join p in db.PhongTros on t.PhongId equals p.Id join b in db.ToaNhas on p.ToaNhaId equals b.Id
         where y.Id == id && b.ChuNhaId != AccountId select y.Id).AnyAsync(ct);

    private async Task<List<YeuCauHopDong>> Approved(CancellationToken ct)
    {
        var admin = await IsAdmin(ct);
        var rows = await (from y in db.YeuCauThues.AsNoTracking()
                      join t in db.TinDangs on y.TinDangId equals t.Id
                      join p in db.PhongTros on t.PhongId equals p.Id
                      join b in db.ToaNhas on p.ToaNhaId equals b.Id
                      join k in db.KhachThues on y.KhachThueId equals k.Id
                      where y.TrangThai == "DA_DUYET" && (admin || b.ChuNhaId == AccountId)
                          && !db.HopDongs.Any(h => h.YeuCauThueId == y.Id && h.TrangThai != "NHAP")
                      orderby y.NgayTao descending
                      select new YeuCauHopDong { Id = y.Id, Ma = y.MaYeuCau, KhachId = k.Id,
                          ToaNhaId = b.Id, DiaChi = b.DiaChi, Tang = p.Tang, DienTich = p.DienTich,
                          LoaiPhong = p.LoaiPhong, TrangThaiPhong = p.TrangThai,
                          SoNguoiDuKien = y.SoNguoiDuKien,
                          Khach = k.HoTen, DienThoai = k.SoDienThoai, PhongId = p.Id,
                          Phong = p.MaPhong, ToaNha = b.TenToaNha, NgayMongMuon = y.NgayMongMuon,
                          GiaThue = p.GiaThue, TienCoc = p.GiaThue, NgayChot = b.NgayChotHangThang, PhienBanPhong = p.PhienBan }).ToListAsync(ct);
        var permittedIds = admin ? rows.Select(x => x.KhachId).ToList()
            : await new HoSoAccess(db).RelatedProfileIdsAsync(AccountId);
        var ids = rows.Select(x => x.KhachId).Where(permittedIds.Contains).Distinct().ToList();
        var profiles = await db.KhachThues.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var accountIds = profiles.Values.Where(x => x.TaiKhoanId.HasValue).Select(x => x.TaiKhoanId!.Value).ToList();
        var emails = await db.TaiKhoans.AsNoTracking().Where(x => accountIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Email, ct);
        foreach (var row in rows)
        {
            if (profiles.TryGetValue(row.KhachId, out var profile))
            {
                row.DuocXemHoSo = true;
                row.SoGiayTo = profile.SoGiayTo;
                row.QueQuan = profile.QueQuan;
                row.Email = profile.TaiKhoanId is { } accountId ? emails.GetValueOrDefault(accountId) : null;
            }
            else row.DienThoai = HoSoAccess.Mask(row.DienThoai);
        }
        return rows;
    }

    [HttpGet, Authorize(Roles = "CHU_NHA"), ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> ThemKhachThue(int? hopDongId, CancellationToken ct)
    {
        if (!await db.TaiKhoans.AnyAsync(x => x.Id == AccountId && x.VaiTro == "CHU_NHA" && x.DangHoatDong && !x.IsDeleted, ct))
            return Forbid();
        if (hopDongId.HasValue)
        {
            if (!await db.HopDongs.AnyAsync(x => x.Id == hopDongId.Value, ct)) return NotFound();
            if (!await OwnContract(hopDongId.Value, ct)) return Forbid();
        }
        return hopDongId.HasValue
            ? RedirectToAction(nameof(Details), new { id = hopDongId.Value, tab = "people" })
            : RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var admin = await IsAdmin(ct);
        var rows = await (from h in db.HopDongs.AsNoTracking()
                          join p in db.PhongTros on h.PhongId equals p.Id
                          join b in db.ToaNhas on p.ToaNhaId equals b.Id
                          join k in db.KhachThues on h.KhachDungTenId equals k.Id into clients
                          from k in clients.DefaultIfEmpty()
                          where admin || b.ChuNhaId == AccountId
                              || (User.IsInRole("QUAN_LY") && b.QuanLyId == AccountId)
                              || (User.IsInRole("KHACH_THUE") && k != null && k.TaiKhoanId == AccountId)
                          orderby h.Id descending
                          select new HopDongDanhSach(h, p.MaPhong, b.TenToaNha, k == null ? null : k.HoTen)).ToListAsync(ct);
        var ids = rows.Select(x => x.HopDong.Id).ToList();
        var periods = await db.KyHopDongs.AsNoTracking().Where(x => ids.Contains(x.HopDongId)).OrderByDescending(x => x.NgayBatDau).ToListAsync(ct);
        var occupants = await db.NguoiOGheps.AsNoTracking()
            .Where(x => ids.Contains(x.HopDongId) && x.NgayVao <= HomNay && (x.NgayRa == null || x.NgayRa >= HomNay))
            .GroupBy(x => x.HopDongId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        ViewData["Occupants"] = occupants;
        ViewData["HomNay"] = HomNay;
        return View(rows.Select(x => x with { Ky = periods.FirstOrDefault(k => k.HopDongId == x.HopDong.Id) }).ToList());
    }

    [HttpGet, ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> Create(int? yeuCauId, CancellationToken ct)
    {
        var vm = new HopDongCreateViewModel { YeuCauId = yeuCauId, SoThang = 12, SoThangCoc = 1, HomNay = HomNay };
        vm.YeuCaus = await Approved(ct);
        vm.DaChon = vm.YeuCaus.SingleOrDefault(x => x.Id == yeuCauId);
        if (yeuCauId.HasValue && vm.DaChon is null)
            return await ForeignRequest(yeuCauId.Value, ct) ? Forbid() : NotFound();
        if (vm.DaChon is { } y)
        {
            vm.GiaThue = y.GiaThue; vm.TienCoc = y.TienCoc;
            vm.PhienBanPhong = y.PhienBanPhong;
            vm.NgayBatDau = y.NgayMongMuon; vm.NgayChot = y.NgayChot;
            var draft = await db.HopDongs.AsNoTracking().SingleOrDefaultAsync(h => h.YeuCauThueId == y.Id && h.TrangThai == "NHAP", ct);
            if (draft is not null)
            {
                var period = await db.KyHopDongs.AsNoTracking().FirstOrDefaultAsync(k => k.HopDongId == draft.Id && k.SoThuTu == 1, ct);
                vm.NgayChot = draft.NgayChot;
                vm.TienCoc = draft.TienCoc;
                if (period is not null) { vm.GiaThue = period.GiaThue; vm.NgayBatDau = period.NgayBatDau; vm.SoThang = period.SoThang; }
            }
            if (vm.NgayBatDau < vm.HomNay) vm.NgayBatDau = vm.HomNay;
        }
        if (vm.DaChon is not null && vm.NgayBatDau is { } start && vm.NgayKetThuc is { } end)
            vm.ChongLans = await new HopDongService(db).ChongLanAsync(vm.DaChon.PhongId, start, end, ct);
        await LoadContractServices(vm, ct);
        return View(vm);
    }

    private async Task LoadContractServices(HopDongCreateViewModel vm, CancellationToken ct)
    {
        vm.PhiDichVus.Clear();
        if (vm.DaChon is not { } chosen) return;
        var ownerId = await db.ToaNhas.Where(x => x.Id == chosen.ToaNhaId).Select(x => x.ChuNhaId).SingleAsync(ct);
        var serviceIds = await db.DichVuPhongs.AsNoTracking().Where(x => x.PhongId == chosen.PhongId)
            .Select(x => x.DichVuToaNha.DichVuId).Distinct().ToListAsync(ct);
        var prices = new DichVuPhongService(db, new DichVuService(db), clock);
        foreach (var serviceId in serviceIds)
            if (await prices.LayGiaHoaDonAsync(ownerId, chosen.PhongId, serviceId, vm.NgayBatDau ?? HomNay) is { } price)
                vm.PhiDichVus.Add(price);
        vm.PhiDichVus = vm.PhiDichVus.OrderBy(x => x.DichVuId).ToList();
    }

    private Task<bool> CoDichVuHopDongAsync(CancellationToken ct) => db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='hop_dong_dich_vu'")
        .AnyAsync(count => count == 1, ct);

    private async Task LuuDichVuHopDongAsync(int contractId, IEnumerable<DonGiaDichVu> prices, DateTime now, CancellationToken ct)
    {
        // Optional invoice tables are never installed by a web request.
        if (!await CoDichVuHopDongAsync(ct)) return;
        var existing = await db.HopDongDichVus.Where(x => x.HopDongId == contractId).ToListAsync(ct);
        var selected = prices.ToList();
        db.HopDongDichVus.RemoveRange(existing.Where(x => selected.All(price => price.DichVuId != x.DichVuId)));
        foreach (var price in selected)
        {
            var snapshot = existing.SingleOrDefault(x => x.DichVuId == price.DichVuId);
            if (snapshot is null)
            {
                snapshot = new HopDongDichVu { HopDongId = contractId, DichVuId = price.DichVuId };
                db.HopDongDichVus.Add(snapshot);
            }
            snapshot.CauHinhDichVuId = price.CauHinhId; snapshot.TenDichVu = price.TenDichVu;
            snapshot.CachTinh = price.CachTinh; snapshot.DonViTinh = price.DonViTinh;
            snapshot.DonGia = price.DonGia; snapshot.NgayGhiNhan = now;
        }
    }

    [HttpGet, ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> KiemTra(int yeuCauId, DateOnly ngayBatDau, int soThang, CancellationToken ct)
    {
        var y = (await Approved(ct)).SingleOrDefault(x => x.Id == yeuCauId);
        if (y is null) return await ForeignRequest(yeuCauId, ct) ? Forbid() : NotFound();
        if (!ModelState.IsValid || soThang is < 1 or > 120 || ngayBatDau.Year > 9988 || ngayBatDau < HomNay) return BadRequest();
        var conflicts = await new HopDongService(db).ChongLanAsync(y.PhongId, ngayBatDau, HopDongCreateViewModel.TinhNgayKetThuc(ngayBatDau, soThang), ct);
        return Json(conflicts.Select(x => new { x.Ma, TuNgay = x.NgayBatDau.ToString("dd/MM/yyyy"), DenNgay = x.NgayKetThuc.ToString("dd/MM/yyyy") }));
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> Create(HopDongCreateViewModel vm, CancellationToken ct)
    {
        bool luuNhap = vm.Intent == "NHAP";
        // BEGIN IMMEDIATE serializes writers before checking overlaps and allocating a code.
        await db.Database.OpenConnectionAsync(ct);
        await using var sqliteTransaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction, ct);
        vm.YeuCaus = await Approved(ct);
        vm.DaChon = vm.YeuCaus.SingleOrDefault(x => x.Id == vm.YeuCauId);
        vm.HomNay = HomNay;
        await LoadContractServices(vm, ct);
        if (vm.DaChon is null && vm.YeuCauId.HasValue && await ForeignRequest(vm.YeuCauId.Value, ct)) return Forbid();
        if (vm.DaChon is null) ModelState.AddModelError(nameof(vm.YeuCauId), "Yêu cầu không được duyệt, không thuộc quyền quản lý hoặc đã lập hợp đồng.");
        // Revalidate independently of the browser and never bind a submitted end date.
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(vm, new(vm), errors, true);
        foreach (var error in errors) ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? "", error.ErrorMessage!);
        if (vm.NgayKetThuc is null) ModelState.AddModelError(nameof(vm.NgayBatDau), "Ngày bắt đầu hoặc kỳ hạn không hợp lệ.");
        if (vm.NgayBatDau < vm.HomNay)
            ModelState.AddModelError(nameof(vm.NgayBatDau), "Ngày bắt đầu không được trước ngày hiện tại.");
        if (vm.GiaThue.HasValue && vm.TienCoc.HasValue && (decimal)vm.TienCoc.Value > 3m * vm.GiaThue.Value)
            ModelState.AddModelError(nameof(vm.TienCoc), "Tiền cọc không được vượt quá 3 tháng giá thuê chốt.");
        if (vm.DaChon is { } chosen && vm.PhienBanPhong != chosen.PhienBanPhong)
            ModelState.AddModelError("", "Thông tin phòng đã thay đổi. Vui lòng tải lại trang trước khi lưu.");
        foreach (var meter in new[] { (nameof(vm.ChiSoDien), vm.ChiSoDien), (nameof(vm.ChiSoNuoc), vm.ChiSoNuoc) })
            if (meter.Item2.HasValue && decimal.Round(meter.Item2.Value, 3) != meter.Item2.Value)
                ModelState.AddModelError(meter.Item1, "Chỉ số đầu kỳ chỉ được có tối đa 3 chữ số thập phân.");
        if (vm.DaChon is not null && vm.NgayBatDau is { } start && vm.NgayKetThuc is { } end)
        {
            vm.ChongLans = await new HopDongService(db).ChongLanAsync(vm.DaChon.PhongId, start, end, ct);
            if (vm.ChongLans.Count > 0) ModelState.AddModelError("", "Không thể lưu: phòng có hợp đồng hiệu lực chồng lấn. Xem thông tin hợp đồng đang vướng bên phải.");
        }
        if (!ModelState.IsValid) return View(vm);
        var y = vm.DaChon!;
        var room = await db.PhongTros.SingleAsync(p => p.Id == y.PhongId, ct);
        if (room.TrangThai == "NGUNG_CHO_THUE")
        {
            ModelState.AddModelError("", "Phòng đang ngừng cho thuê, không thể lập hợp đồng.");
            return View(vm);
        }
        var now = (clock ?? new SystemTimeProvider()).UtcNow;
        string code;
        try { code = await new HopDongService(db).SinhMaAsync(now.AddHours(7).Year, ct); }
        catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); return View(vm); }
        try
        {
        var contract = await db.HopDongs.SingleOrDefaultAsync(h => h.YeuCauThueId == y.Id && h.TrangThai == "NHAP", ct);
        if (contract is null)
        {
            contract = new HopDongThamChieu { YeuCauThueId = y.Id, NguoiLapId = AccountId, NgayTao = now };
            db.HopDongs.Add(contract);
        }
        contract.MaHopDong = code; contract.PhongId = y.PhongId; contract.KhachDungTenId = y.KhachId;
        contract.TienCoc = vm.TienCoc!.Value; contract.NgayChot = vm.NgayChot!.Value; contract.TrangThai = luuNhap ? "NHAP" : "DANG_HIEU_LUC";
        await db.SaveChangesAsync(ct);
        var period = await db.KyHopDongs.SingleOrDefaultAsync(k => k.HopDongId == contract.Id && k.SoThuTu == 1, ct);
        if (period is null) { period = new KyHopDongThamChieu { HopDongId = contract.Id, NguoiLapId = AccountId, NgayTao = now }; db.KyHopDongs.Add(period); }
        period.NgayBatDau = vm.NgayBatDau!.Value; period.NgayKetThuc = vm.NgayKetThuc!.Value;
        period.GiaThue = vm.GiaThue!.Value; period.SoThang = vm.SoThang!.Value;
        // Only new contracts or editable drafts reach this action. Freeze the actual
        // assigned room prices with the contract, in the same transaction.
        await LuuDichVuHopDongAsync(contract.Id, vm.PhiDichVus, now, ct);
        var meter = await db.HopDongChiSoDauKys.SingleOrDefaultAsync(m => m.HopDongId == contract.Id, ct);
        if (meter is null) { meter = new HopDongChiSoDauKy { HopDongId = contract.Id }; db.HopDongChiSoDauKys.Add(meter); }
        meter.NgayBanGiao = vm.NgayBatDau.Value;
        meter.ChiSoDien = vm.ChiSoDien!.Value;
        meter.ChiSoNuoc = vm.ChiSoNuoc!.Value;
        meter.NguoiNhapId = AccountId;
        meter.NgayNhap = now;
        if (!luuNhap)
        {
            room.TrangThai = "DANG_THUE";
            room.PhienBan++;
            // Hợp đồng có hiệu lực: mọi tin đăng của phòng rời khỏi trang công khai cùng lúc với phòng.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tin_dang SET trang_thai='DA_CHO_THUE' WHERE phong_id={room.Id} AND trang_thai<>'DA_CHO_THUE'", ct);
        }
        await db.SaveChangesAsync(ct);
        await sqliteTransaction.CommitAsync(ct);
        if (luuNhap)
        {
            TempData["ContractSuccess"] = $"Đã lưu hợp đồng {code} ở trạng thái nháp. Mở chi tiết hợp đồng và bấm Kích hoạt khi sẵn sàng cho thuê.";
            return RedirectToAction(nameof(Details), new { id = contract.Id });
        }
        TempData["ContractSuccess"] = $"Đã tạo hợp đồng {code}, ghi chỉ số điện nước đầu kỳ. Phòng {y.Phong} — {y.ToaNha}: Đang thuê.";
        return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            await sqliteTransaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "Không thể lưu hợp đồng. Không có thay đổi nào được ghi nhận. Vui lòng tải lại trang và thử lại.");
            return View(vm);
        }
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> KichHoat(int id, string? simulateError, CancellationToken ct)
    {
        if (!await db.HopDongs.AnyAsync(h => h.Id == id, ct)) return NotFound();
        if (!await OwnContract(id, ct)) return Forbid();

        // Error simulation for testing (only in Development)
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var shouldSimulate = isDevelopment && !string.IsNullOrEmpty(simulateError);

        try
        {
            // BEGIN IMMEDIATE serializes writers so two owners cannot activate one room at once.
            await db.Database.OpenConnectionAsync(ct);
            await using var sqliteTransaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction, ct);
            if (!await OwnContract(id, ct)) return Forbid();
            var hopDong = await db.HopDongs.SingleAsync(h => h.Id == id, ct);
            var room = await db.PhongTros.SingleAsync(p => p.Id == hopDong.PhongId, ct);
            if (hopDong.TrangThai == "DANG_HIEU_LUC")
                ModelState.AddModelError("", "Hợp đồng đã hiệu lực. Phòng và tin đăng đã được cập nhật trước đó.");
            else if (hopDong.TrangThai is not ("NHAP" or "CHO_HIEU_LUC"))
                ModelState.AddModelError("", "Chỉ kích hoạt được hợp đồng ở trạng thái nháp hoặc chờ hiệu lực.");
            if (room.TrangThai == "NGUNG_CHO_THUE")
                ModelState.AddModelError("", "Phòng đang ngừng cho thuê, không thể kích hoạt hợp đồng.");
            var period = await db.KyHopDongs.AsNoTracking().Where(k => k.HopDongId == id).OrderByDescending(k => k.NgayBatDau).FirstOrDefaultAsync(ct);
            if (period is not null)
            {
                var conflicts = await new HopDongService(db).ChongLanAsync(room.Id, period.NgayBatDau, period.NgayKetThuc, ct);
                if (conflicts.Any(c => c.Id != id))
                    ModelState.AddModelError("", $"Phòng {room.MaPhong} đã có hợp đồng hiệu lực khác. Không thể kích hoạt.");
            }
            if (!ModelState.IsValid) return View("Details", await DetailModel(id, null, ct));

            // Step 1: Activate contract
            hopDong.TrangThai = "DANG_HIEU_LUC";
            if (shouldSimulate && simulateError == "contract")
                throw new InvalidOperationException("Giả lập lỗi: không thể cập nhật trạng thái hợp đồng.");
            await db.SaveChangesAsync(ct);

            // Step 2: Update room status
            room.TrangThai = "DANG_THUE";
            room.PhienBan++;
            if (shouldSimulate && simulateError == "room")
                throw new InvalidOperationException("Giả lập lỗi: không thể cập nhật trạng thái phòng.");
            await db.SaveChangesAsync(ct);

            // Step 3: Update all listings to DA_CHO_THUE
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tin_dang SET trang_thai='DA_CHO_THUE' WHERE phong_id={room.Id} AND trang_thai<>'DA_CHO_THUE'", ct);
            if (shouldSimulate && simulateError == "listing")
                throw new InvalidOperationException("Giả lập lỗi: không thể cập nhật trạng thái tin đăng.");
            await db.SaveChangesAsync(ct);

            await sqliteTransaction.CommitAsync(ct);
            TempData["ContractSuccess"] = $"Hợp đồng {hopDong.MaHopDong} đã hiệu lực. Phòng {room.MaPhong}: Đang thuê; tin đăng: Đã cho thuê.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException or InvalidOperationException)
        {
            db.ChangeTracker.Clear();
            var message = ex is InvalidOperationException && shouldSimulate
                ? $"Kích hoạt thất bại (giả lập): {ex.Message}. Trạng thái chưa thay đổi, bạn có thể thử lại."
                : "Không thể kích hoạt hợp đồng do lỗi cơ sở dữ liệu. Không có thay đổi nào được lưu. Vui lòng tải lại trang và thử lại.";
            ModelState.AddModelError("", message);
            return View("Details", await DetailModel(id, null, ct));
        }
        catch (Exception)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "Không thể kích hoạt hợp đồng do lỗi không mong đợi. Không có thay đổi nào được lưu. Vui lòng tải lại trang và thử lại.");
            return View("Details", await DetailModel(id, null, ct));
        }
    }


    private async Task<bool> OwnContract(int id, CancellationToken ct) => await
        (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id
         join b in db.ToaNhas on p.ToaNhaId equals b.Id
         join a in db.TaiKhoans on b.ChuNhaId equals a.Id
         where h.Id == id && a.Id == AccountId && a.VaiTro == "CHU_NHA" && a.DangHoatDong && !a.IsDeleted
         select h.Id).AnyAsync(ct);

    private async Task<HopDongDetailsViewModel> DetailModel(int id, NguoiOGhepInput? input, CancellationToken ct)
    {
        var h = await db.HopDongs.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var p = await db.PhongTros.AsNoTracking().SingleAsync(x => x.Id == h.PhongId, ct);
        var signer = await db.KhachThues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == h.KhachDungTenId, ct);
        var model = new HopDongDetailsViewModel { HopDong = h, Phong = p.MaPhong, GioiHan = p.SoNguoiToiDa, DungTen = signer,
            HomNay = HomNay, ChoThem = signer != null && h.TrangThai is "CHO_HIEU_LUC" or "DANG_HIEU_LUC",
            Input = input ?? new() { NgayVao = HomNay, PhienBanPhong = p.PhienBan },
            ChuyenDi = new() { NgayRa = HomNay, PhienBanPhong = p.PhienBan } };
        model.ToaNha = await db.ToaNhas.Where(x => x.Id == p.ToaNhaId).Select(x => x.TenToaNha).SingleAsync(ct);
        var building = await db.ToaNhas.AsNoTracking().SingleAsync(x => x.Id == p.ToaNhaId, ct);
        model.DiaChiPhong = building.DiaChi;
        model.LoaiPhong = p.LoaiPhong; model.Tang = p.Tang; model.DienTich = p.DienTich;
        model.ChuNha = await db.TaiKhoans.Where(x => x.Id == building.ChuNhaId).Select(x => x.HoTen).SingleAsync(ct);
        var ownerContact = await db.TaiKhoans.AsNoTracking().Where(x => x.Id == building.ChuNhaId)
            .Select(x => new { x.SoDienThoai, x.Email }).SingleAsync(ct);
        model.ChuNhaDienThoai = AccountId == building.ChuNhaId || await IsAdmin(ct)
            ? ownerContact.SoDienThoai : HoSoAccess.Mask(ownerContact.SoDienThoai);
        model.ChuNhaEmail = ownerContact.Email;
        if (signer?.TaiKhoanId is { } signerAccountId &&
            (AccountId == building.ChuNhaId || await IsAdmin(ct) || signerAccountId == AccountId))
            model.NguoiThueEmail = await db.TaiKhoans.AsNoTracking().Where(x => x.Id == signerAccountId)
                .Select(x => x.Email).SingleOrDefaultAsync(ct);
        model.DichVus = await CoDichVuHopDongAsync(ct)
            ? await db.HopDongDichVus.AsNoTracking().Where(x => x.HopDongId == id).OrderBy(x => x.DichVuId).ToListAsync(ct)
            : [];
        model.XemTaiChinh = permissions is not null && await permissions.AllowsAsync("TAI_CHINH")
            && await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='hoa_don'")
                .AnyAsync(count => count == 1, ct);
        if (model.XemTaiChinh)
            model.HoaDons = await db.HoaDons.AsNoTracking().Where(x => x.HopDongId == id && x.TrangThai != "DA_HUY")
                .OrderByDescending(x => x.Nam).ThenByDescending(x => x.Thang).Take(3).ToListAsync(ct);
        var periods = await db.KyHopDongs.AsNoTracking().Where(x => x.HopDongId == id)
            .OrderByDescending(x => x.NgayBatDau).ThenByDescending(x => x.Id).ToListAsync(ct);
        model.Ky = periods.FirstOrDefault();
        var renewalPeriods = periods.Where(x => x.SoThuTu > 1).ToList();
        if (renewalPeriods.Count > 0)
        {
            var periodIds = renewalPeriods.Select(x => x.Id).ToList();
            var logs = await db.NhatKyHoatDongs.AsNoTracking()
                .Where(x => x.LoaiDoiTuong == "ky_hop_dong" && periodIds.Contains(x.DoiTuongId) && x.HanhDong == "TAO")
                .OrderByDescending(x => x.Id)
                .ToListAsync(ct);
            var logByPeriod = logs.GroupBy(x => x.DoiTuongId).ToDictionary(x => x.Key, x => x.First());
            var actorIds = renewalPeriods.Where(x => !logByPeriod.ContainsKey(x.Id) && x.NguoiLapId.HasValue)
                .Select(x => x.NguoiLapId!.Value).Distinct().ToList();
            var actorNames = await db.TaiKhoans.AsNoTracking().Where(x => actorIds.Contains(x.Id))
                .Select(x => new { x.Id, x.HoTen }).ToDictionaryAsync(x => x.Id, x => x.HoTen, ct);

            model.LichSuGiaHan = renewalPeriods
                .OrderBy(x => x.SoThuTu)
                .Select(period => logByPeriod.TryGetValue(period.Id, out var log)
                    ? new HopDongGiaHanHistoryRow(period.SoThuTu, period.NgayBatDau, period.NgayKetThuc,
                        period.GiaThue, log.ThoiDiem.AddHours(7), log.TenNguoiThucHien)
                    : new HopDongGiaHanHistoryRow(period.SoThuTu, period.NgayBatDau, period.NgayKetThuc,
                        period.GiaThue, period.NgayTao, period.NguoiLapId is { } actorId
                            ? actorNames.GetValueOrDefault(actorId) : null))
                .ToList();
        }
        model.ChoGiaHan = model.Ky is not null && (h.TrangThai is "DANG_HIEU_LUC" or "CHO_HIEU_LUC")
            && permissions is not null && await permissions.AllowsAsync("HOP_DONG", write: true);
        model.ChiSo = await db.HopDongChiSoDauKys.AsNoTracking().SingleOrDefaultAsync(x => x.HopDongId == id, ct);
        var people = await (from g in db.NguoiOGheps.AsNoTracking() join k in db.KhachThues on g.KhachThueId equals k.Id
            where g.HopDongId == id orderby g.NgayVao, g.Id
            select new NguoiOGhepRow(k.HoTen, k.SoDienThoai, k.SoGiayTo, g.NgayVao) { Id = g.Id, NgayRa = g.NgayRa }).ToListAsync(ct);
        if ((AccountId != building.ChuNhaId && !await IsAdmin(ct)) || h.TrangThai != "DANG_HIEU_LUC" || !await db.KyHopDongs.AnyAsync(k => k.HopDongId == id && k.NgayBatDau <= HomNay && k.NgayKetThuc >= HomNay, ct))
        {
            static string? Mask(string? value) => value == null ? null : new string('*', Math.Max(0, value.Length - 4)) + value[^Math.Min(4, value.Length)..];
            if (signer != null) { signer.SoDienThoai = Mask(signer.SoDienThoai); signer.SoGiayTo = Mask(signer.SoGiayTo); }
            people = people.Select(x => x with { SoDienThoai = Mask(x.SoDienThoai), SoGiayTo = Mask(x.SoGiayTo) }).ToList();
        }
        model.Nguois = people.Where(x => x.NgayVao <= HomNay && (x.NgayRa == null || x.NgayRa >= HomNay)).ToList();
        model.DaChuyenDi = people.Where(x => x.NgayRa < HomNay).OrderByDescending(x => x.NgayRa).ToList();
        model.SapVao = people.Where(x => x.NgayVao > HomNay).ToList();
        model.TrangThaiPhong = p.TrangThai;
        model.TrangThaiTin = await db.TinDangs.AsNoTracking().Where(t => t.PhongId == h.PhongId)
            .OrderByDescending(t => t.Id).Select(t => t.TrangThai).FirstOrDefaultAsync(ct) ?? "Chưa có tin";
        model.ChoKichHoat = h.TrangThai is "NHAP" or "CHO_HIEU_LUC";
        return model;
    }

    private async Task<bool> CanRenewAsync(int id, CancellationToken ct)
    {
        if (!await db.HopDongs.AnyAsync(x => x.Id == id && (x.TrangThai == "DANG_HIEU_LUC" || x.TrangThai == "CHO_HIEU_LUC"), ct))
            return false;
        var end = await db.KyHopDongs.Where(x => x.HopDongId == id)
            .OrderByDescending(x => x.NgayBatDau).ThenByDescending(x => x.Id)
            .Select(x => (DateOnly?)x.NgayKetThuc).FirstOrDefaultAsync(ct);
        return end.HasValue && end.Value != DateOnly.MaxValue;
    }

    private async Task<HopDongGiaHanViewModel> GiaHanModelAsync(int id, CancellationToken ct)
    {
        var contract = await db.HopDongs.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var current = await db.KyHopDongs.AsNoTracking().Where(x => x.HopDongId == id)
            .OrderByDescending(x => x.NgayBatDau).ThenByDescending(x => x.Id).FirstAsync(ct);
        return new HopDongGiaHanViewModel
        {
            HopDongId = id,
            MaHopDong = contract.MaHopDong,
            NgayKetThucHienTai = current.NgayKetThuc,
            NgayBatDau = current.NgayKetThuc == DateOnly.MaxValue ? null : current.NgayKetThuc.AddDays(1),
            GiaThueHienTai = current.GiaThue,
            GiaThueMoi = current.GiaThue
        };
    }

    [HttpGet]
    public async Task<IActionResult> LichSuNguoiO(int phongId, DateOnly? tuNgay, DateOnly? denNgay, CancellationToken ct)
    {
        if (!await db.PhongTros.AnyAsync(p => p.Id == phongId, ct)) return NotFound();
        var service = new LichSuNguoiOService(db);
        var room = await service.PhongAsync(AccountId, phongId, ct);
        if (room == null) return Forbid();
        var model = new LichSuNguoiOViewModel { PhongId = phongId, Phong = room.Value.Phong, ToaNha = room.Value.ToaNha,
            TuNgay = tuNgay ?? new DateOnly(HomNay.Year, HomNay.Month, 1), DenNgay = denNgay ?? HomNay };
        if (model.DenNgay < model.TuNgay) ModelState.AddModelError(nameof(model.DenNgay), "Đến ngày không được trước từ ngày.");
        if (ModelState.IsValid)
        {
            var history = await service.LayAsync(AccountId, phongId, model.TuNgay.Value, model.DenNgay.Value, HomNay, ct);
            model.Nguois = history.Rows; model.HopDongThieuDuLieu = history.Missing; model.DaLoc = true;
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        if (!await db.HopDongs.AnyAsync(x => x.Id == id, ct)) return NotFound();
        if (!await OwnContract(id, ct)) return Forbid();
        return View(await DetailModel(id, null, ct));
    }

    [HttpGet, ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> GiaHan(int id, CancellationToken ct)
    {
        if (!await db.HopDongs.AnyAsync(x => x.Id == id, ct)) return NotFound();
        if (!await OwnContract(id, ct)) return Forbid();
        if (!await CanRenewAsync(id, ct))
        {
            TempData["ContractWarning"] = "Hợp đồng không đủ điều kiện gia hạn hoặc ngày kết thúc đã ở giới hạn tối đa.";
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(await GiaHanModelAsync(id, ct));
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> GiaHan(int id, HopDongGiaHanViewModel vm, CancellationToken ct)
    {
        if (!await db.HopDongs.AnyAsync(x => x.Id == id, ct)) return NotFound();
        if (!await OwnContract(id, ct)) return Forbid();

        async Task<IActionResult> InvalidAsync()
        {
            var model = await GiaHanModelAsync(id, ct);
            model.SoThang = vm.SoThang;
            model.GiaThueMoi = vm.GiaThueMoi ?? model.GiaThueHienTai;
            return View(model);
        }

        if (!ModelState.IsValid) return await InvalidAsync();

        try
        {
            await db.Database.OpenConnectionAsync(ct);
            await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await using var transaction = await db.Database.UseTransactionAsync(sqlite, ct);
            if (!await OwnContract(id, ct)) return Forbid();

            var contract = await db.HopDongs.SingleAsync(x => x.Id == id, ct);
            var current = await db.KyHopDongs.Where(x => x.HopDongId == id)
                .OrderByDescending(x => x.NgayBatDau).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            if (current is null)
            {
                TempData["ContractWarning"] = "Hợp đồng không có kỳ hạn hiện tại để gia hạn.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (contract.TrangThai is not ("DANG_HIEU_LUC" or "CHO_HIEU_LUC"))
                ModelState.AddModelError("", "Chỉ gia hạn được hợp đồng đang hoặc chờ hiệu lực có kỳ hạn hiện tại.");

            DateOnly? start = null;
            DateOnly? end = null;
            if (current.NgayKetThuc == DateOnly.MaxValue)
                ModelState.AddModelError("", "Không thể gia hạn vì ngày kết thúc hiện tại đã là ngày tối đa được hỗ trợ.");
            else
            {
                start = current.NgayKetThuc.AddDays(1);
                try
                {
                    if (vm.SoThang is >= 1 and <= 120)
                        end = HopDongGiaHanViewModel.TinhNgayKetThuc(start.Value, vm.SoThang.Value);
                }
                catch (ArgumentOutOfRangeException)
                {
                    ModelState.AddModelError(nameof(vm.SoThang), "Ngày kết thúc mới vượt ngoài phạm vi ngày được hỗ trợ.");
                }
            }

            var rent = vm.GiaThueMoi ?? current.GiaThue;
            if (rent <= 0)
                ModelState.AddModelError(nameof(vm.GiaThueMoi), "Giá thuê mới phải lớn hơn 0.");

            var roomId = contract.PhongId;
            if (start.HasValue && end.HasValue)
            {
                var conflicts = await new HopDongService(db).ChongLanAsync(roomId, start.Value, end.Value, ct);
                if (conflicts.Any(x => x.Id != id))
                    ModelState.AddModelError("", "Kỳ gia hạn bị chồng lấn với một hợp đồng khác trong cùng phòng.");
            }
            if (!ModelState.IsValid) return await InvalidAsync();

            var nextOrder = await db.KyHopDongs.Where(x => x.HopDongId == id)
                .Select(x => (int?)x.SoThuTu).MaxAsync(ct) ?? 0;
            db.KyHopDongs.Add(new KyHopDongThamChieu
            {
                HopDongId = id,
                SoThuTu = checked(nextOrder + 1),
                NgayBatDau = start!.Value,
                NgayKetThuc = end!.Value,
                SoThang = vm.SoThang!.Value,
                GiaThue = rent,
                NguoiLapId = AccountId,
                NgayTao = (clock ?? new SystemTimeProvider()).UtcNow.AddHours(7)
            });
            await db.SaveChangesAsync(ct);
            await sqlite.CommitAsync(ct);

            TempData["ContractSuccess"] = $"Đã gia hạn hợp đồng {contract.MaHopDong} đến ngày {end:dd/MM/yyyy}.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "Không thể lưu kỳ gia hạn do lỗi cơ sở dữ liệu. Không có thay đổi nào được lưu; vui lòng tải lại và thử lại.");
            return await InvalidAsync();
        }
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> ChuyenDi(int id, int nguoiId, [Bind(Prefix = "ChuyenDi")] ChuyenDiInput input, CancellationToken ct)
    {
        if (!await db.HopDongs.AnyAsync(h => h.Id == id, ct)) return NotFound();
        if (!await OwnContract(id, ct)) return Forbid();
        if (!await db.NguoiOGheps.AnyAsync(g => g.Id == nguoiId && g.HopDongId == id, ct)) return Forbid();
        async Task<IActionResult> Invalid()
        {
            var model = await DetailModel(id, null, ct);
            model.ChuyenDi = input; model.ChuyenDiNguoiId = nguoiId;
            return View("Details", model);
        }
        if (input.NgayRa == null) ModelState.AddModelError("ChuyenDi.NgayRa", "Chọn ngày chuyển đi.");
        if (input.NgayRa?.Year > 9998) ModelState.AddModelError("ChuyenDi.NgayRa", "Ngày chuyển đi ngoài phạm vi hỗ trợ.");
        if (!ModelState.IsValid) return await Invalid();
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await using var transaction = await db.Database.UseTransactionAsync(sqlite, ct);
            if (!await OwnContract(id, ct)) return Forbid();
            var h = await db.HopDongs.SingleAsync(x => x.Id == id, ct);
            var room = await db.PhongTros.SingleAsync(x => x.Id == h.PhongId, ct);
            var stay = await db.NguoiOGheps.SingleOrDefaultAsync(g => g.Id == nguoiId && g.HopDongId == id, ct);
            if (stay == null) return Forbid();
            if (h.TrangThai is not ("DANG_HIEU_LUC" or "CHO_HIEU_LUC"))
                ModelState.AddModelError("", "Chỉ ghi nhận chuyển đi trong hợp đồng đang hoặc chờ hiệu lực.");
            if (room.PhienBan != input.PhienBanPhong)
                ModelState.AddModelError("", "Dữ liệu phòng đã thay đổi. Vui lòng tải lại trang trước khi lưu.");
            if (stay.NgayRa != null) ModelState.AddModelError("", "Người ở ghép đã được ghi ngày chuyển đi. Không thể ghi lại.");
            if (input.NgayRa < stay.NgayVao) ModelState.AddModelError("ChuyenDi.NgayRa", "Ngày chuyển đi không được trước ngày bắt đầu ở cùng.");
            if (!ModelState.IsValid) return await Invalid();
            var day = input.NgayRa!.Value;
            var next = new DateOnly(day.Year, day.Month, 1).AddMonths(1);
            using var probe = db.Database.GetDbConnection().CreateCommand();
            probe.Transaction = sqlite;
            probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='hoa_don'";
            var invoicesReady = Convert.ToInt32(await probe.ExecuteScalarAsync(ct)) == 1;
            // Contracts also work on databases where the optional invoice module is absent.
            var issued = invoicesReady ? await db.HoaDons.AsNoTracking().Where(x => x.HopDongId == id && x.TrangThai != "DA_HUY"
                && (x.Nam > next.Year || (x.Nam == next.Year && x.Thang >= next.Month)))
                .OrderBy(x => x.Nam).ThenBy(x => x.Thang).Select(x => new { x.Nam, x.Thang }).ToListAsync(ct) : [];
            stay.NgayRa = day; room.PhienBan++;
            await db.SaveChangesAsync(ct);
            await sqlite.CommitAsync(ct);
            TempData["ContractSuccess"] = $"Đã ghi ngày chuyển đi {day:dd/MM/yyyy}. Ngày chuyển đi vẫn tính là ngày còn ở; giảm khoản khoán từ kỳ {next:MM/yyyy}.";
            if (issued.Count > 0)
                TempData["ContractWarning"] = "Hóa đơn các kỳ " + string.Join(", ", issued.Select(x => $"{x.Thang:00}/{x.Nam}"))
                    + " đã lập được giữ nguyên. Số người giảm chỉ áp dụng cho hóa đơn chưa lập; hãy kiểm tra các hóa đơn này.";
            return RedirectToAction(nameof(Details), new { id, tab = "people" });
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "Không thể ghi nhận chuyển đi. Không có thay đổi nào được lưu. Vui lòng tải lại và thử lại.");
            return await Invalid();
        }
    }

    [HttpPost, ValidateAntiForgeryToken, ModuleAccess("HOP_DONG", write: true)]
    public async Task<IActionResult> ThemNguoi(int id, [Bind(Prefix = "Input")] NguoiOGhepInput input, CancellationToken ct)
    {
        if (!await db.HopDongs.AnyAsync(x => x.Id == id, ct)) return NotFound();
        if (!await OwnContract(id, ct)) return Forbid();
        input.HoTen = input.HoTen?.Trim() ?? "";
        input.SoDienThoai = input.SoDienThoai?.Trim() ?? "";
        input.SoGiayTo = input.SoGiayTo?.Trim() ?? "";
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(input, new(input), errors, true);
        foreach (var error in errors) ModelState.AddModelError("Input." + error.MemberNames.FirstOrDefault(), error.ErrorMessage!);
        async Task<IActionResult> Invalid() => View("Details", await DetailModel(id, input, ct));
        if (!ModelState.IsValid) return await Invalid();
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await using var transaction = await db.Database.UseTransactionAsync(sqlite, ct);
            if (!await OwnContract(id, ct)) return Forbid();
            var h = await db.HopDongs.SingleAsync(x => x.Id == id, ct);
            var room = await db.PhongTros.SingleAsync(x => x.Id == h.PhongId, ct);
            var signer = await db.KhachThues.SingleOrDefaultAsync(x => x.Id == h.KhachDungTenId, ct);
            if (signer is null || h.TrangThai is not ("DANG_HIEU_LUC" or "CHO_HIEU_LUC"))
                ModelState.AddModelError("", "Chỉ thêm người vào hợp đồng đã chốt có một người đứng tên.");
            if (room.PhienBan != input.PhienBanPhong)
                ModelState.AddModelError("", "Dữ liệu phòng đã thay đổi. Vui lòng tải lại trang trước khi lưu.");
            var day = input.NgayVao!.Value;
            if (!await db.KyHopDongs.AnyAsync(k => k.HopDongId == id && k.NgayBatDau <= day && k.NgayKetThuc >= day, ct))
                ModelState.AddModelError("Input.NgayVao", "Ngày bắt đầu ở cùng phải nằm trong kỳ hợp đồng.");
            // New stay has no end date: check every future arrival as well as the requested start.
            var stays = await db.NguoiOGheps.Where(g => g.HopDongId == id && (g.NgayRa == null || g.NgayRa >= day)).ToListAsync(ct);
            var peak = stays.Select(g => g.NgayVao).Where(d => d >= day).Append(day)
                .Max(d => 1 + stays.Count(g => g.NgayVao <= d && (g.NgayRa == null || g.NgayRa >= d)));
            if (peak + 1 > room.SoNguoiToiDa)
                ModelState.AddModelError("", $"Phòng {room.MaPhong} tối đa {room.SoNguoiToiDa} người (bao gồm người đứng tên). Không thể thêm người ở ghép.");
            if (signer?.SoGiayTo == input.SoGiayTo || await
                (from g in db.NguoiOGheps join k in db.KhachThues on g.KhachThueId equals k.Id
                 where g.HopDongId == id && k.SoGiayTo == input.SoGiayTo && (g.NgayRa == null || g.NgayRa >= day)
                 select g.Id).AnyAsync(ct))
                ModelState.AddModelError("Input.SoGiayTo", "Căn cước trùng người đứng tên hoặc người ở ghép có khoảng ở chồng lấn.");
            if (!ModelState.IsValid) return await Invalid();
            // Do not link or modify a sensitive profile owned by an unrelated account.
            var profile = new KhachThue { HoTen = input.HoTen, SoDienThoai = input.SoDienThoai,
                SoGiayTo = input.SoGiayTo, NgayTao = (clock ?? new SystemTimeProvider()).UtcNow };
            db.KhachThues.Add(profile);
            await db.SaveChangesAsync(ct);
            db.NguoiOGheps.Add(new() { HopDongId = id, KhachThueId = profile.Id, NgayVao = day });
            room.PhienBan++;
            await db.SaveChangesAsync(ct);
            await sqlite.CommitAsync(ct);
            TempData["ContractSuccess"] = "Đã thêm người ở ghép. Người đứng tên tiếp tục chịu trách nhiệm thanh toán và nhận lại tiền cọc.";
            return RedirectToAction(nameof(Details), new { id, tab = "people" });
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "Không thể lưu do dữ liệu đang thay đổi. Vui lòng tải lại trang và thử lại.");
            return await Invalid();
        }
    }
}
