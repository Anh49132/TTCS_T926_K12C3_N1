using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

public partial class HomeController(
    RegistrationSettings settings,
    AppDbContext? db = null,
    YeuCauThueService? requests = null,
    TinDangExpirationService? expiration = null,
    TienDoChiSoService? meterProgress = null,
    ITimeProvider? clock = null) : Controller
{
    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(int? toaNhaId = null, string? ky = null, int months = 6)
    {
        var localToday = DateOnly.FromDateTime((clock?.UtcNow ?? DateTime.UtcNow).AddHours(7));
        if(months is not (3 or 6 or 12)) return BadRequest("Khoảng biểu đồ không hợp lệ.");
        var selectedPeriod = new DateOnly(localToday.Year, localToday.Month, 1);
        if (User.IsInRole("CHU_NHA") && ky != null &&
            (!DateOnly.TryParseExact(ky + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out selectedPeriod) || selectedPeriod.Year < 1900 || selectedPeriod.Year > 9998))
            return BadRequest("Kỳ theo dõi không hợp lệ.");
        if (User.IsInRole("CHU_NHA") && toaNhaId.HasValue && db != null)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var filterOwnerId) ||
                !await db.ToaNhas.AnyAsync(x => x.Id == toaNhaId && x.ChuNhaId == filterOwnerId && x.DangHoatDong))
                return StatusCode(StatusCodes.Status403Forbidden);
        }
        ViewData["RegistrationEnabled"] = settings.EnableDuplicateCheck;
        var model = new HomeViewModel
        {
            RegistrationEnabled = settings.EnableDuplicateCheck
        };
        model.Dashboard.SelectedBuildingId = User.IsInRole("CHU_NHA") ? toaNhaId : null;
        model.Dashboard.SelectedPeriod = selectedPeriod;
        model.Dashboard.FinanceMonths = months;

        if (db != null && requests != null)
        {
            try
            {
                bool hasTinDang = false;
                bool hasPayments = false;
                await db.Database.OpenConnectionAsync();
                try
                {
                    using var command = db.Database.GetDbConnection().CreatePortableCommand();
                    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'tin_dang'";
                    hasTinDang = Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
                    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('thanh_toan','hoa_don','hop_dong')";
                    hasPayments = Convert.ToInt64(await command.ExecuteScalarAsync()) == 3;
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }

                if (hasTinDang)
                {
                    if (expiration != null)
                    {
                        await expiration.ExpireAsync();
                    }
                    model.QuanHuyens = await db.ToaNhas.AsNoTracking()
                        .Where(t => t.QuanHuyen != null && t.QuanHuyen.Trim() != "")
                        .Select(t => t.QuanHuyen!.Trim())
                        .Distinct()
                        .OrderBy(q => q)
                        .ToListAsync();

                    var query = from post in requests.PublicListings()
                                join room in db.PhongTros.AsNoTracking() on post.PhongId equals room.Id
                                join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
                                orderby post.NgayDang descending, post.Id descending
                                select new
                                {
                                    post.Id,
                                    post.TieuDe,
                                    post.PhongId,
                                    room.GiaThue,
                                    room.DienTich,
                                    room.SoNguoiToiDa,
                                    building.TenToaNha,
                                    building.PhuongXa,
                                    building.QuanHuyen,
                                    building.DiaChi
                                };

                    var items = await query.Take(6).ToListAsync();
                    var phongIds = items.Select(x => x.PhongId).Distinct().ToList();

                    var images = await db.AnhPhongs.AsNoTracking()
                        .Where(a => phongIds.Contains(a.PhongId))
                        .OrderBy(a => a.ThuTu)
                        .ToListAsync();

                    var imageMap = images
                        .GroupBy(a => a.PhongId)
                        .ToDictionary(g => g.Key, g => g.First().DuongDanAnhNho);

                    model.TinMoiNhat = items.Select(x => new HomeTinDangItemViewModel
                    {
                        Id = x.Id,
                        TieuDe = x.TieuDe,
                        GiaThue = x.GiaThue,
                        DienTich = x.DienTich,
                        SoNguoiToiDa = x.SoNguoiToiDa,
                        TenToaNha = x.TenToaNha,
                        PhuongXa = x.PhuongXa,
                        QuanHuyen = x.QuanHuyen,
                        DiaChi = x.DiaChi,
                        AnhDaiDien = imageMap.GetValueOrDefault(x.PhongId)
                    }).ToList();

                    if (User.Identity?.IsAuthenticated == true &&
                        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId))
                    {
                        var role = User.FindFirstValue(ClaimTypes.Role);
                        var visibleRooms = db.PhongTros.AsNoTracking().Where(room => db.ToaNhas.Any(building =>
                            building.Id == room.ToaNhaId && (role == "ADMIN" ||
                            (role == "CHU_NHA" && building.ChuNhaId == accountId) ||
                            (role == "QUAN_LY" && building.QuanLyId == accountId))));
                        model.Dashboard.Rooms = await (from room in visibleRooms
                            join building in db.ToaNhas on room.ToaNhaId equals building.Id
                            orderby room.MaPhong
                            select new HomeRoomItem(room.Id, room.MaPhong, building.TenToaNha, room.TrangThai, room.GiaThue)).Take(6).ToListAsync();
                        model.Dashboard.Requests = await (from request in db.YeuCauThues.AsNoTracking()
                            join tenant in db.KhachThues on request.KhachThueId equals tenant.Id
                            join listing in db.TinDangs on request.TinDangId equals listing.Id
                            join room in db.PhongTros on listing.PhongId equals room.Id
                            where role == "KHACH_THUE" ? tenant.TaiKhoanId == accountId : visibleRooms.Any(x => x.Id == room.Id)
                            orderby request.NgayTao descending
                            select new HomeRequestItem(request.Id, request.MaYeuCau, room.MaPhong, tenant.HoTen, request.TrangThai, request.NgayTao)).Take(5).ToListAsync();
                        if (role == "ADMIN")
                        {
                            var roleCounts = await db.TaiKhoans.Where(x => !x.IsDeleted).GroupBy(x => x.VaiTro)
                                .Select(x => new { Role = x.Key, Count = x.Count() }).ToListAsync();
                            model.Dashboard.Roles = roleCounts.Select(x => new HomeRoleItem(x.Role switch {
                                "KHACH_THUE" => "Khách thuê", "CHU_NHA" => "Chủ nhà", "QUAN_LY" => "Quản lý", _ => "Admin" }, x.Count)).ToList();
                            model.Dashboard.Activities = await db.NhatKyHoatDongs.AsNoTracking().OrderByDescending(x => x.ThoiDiem).Take(6).ToListAsync();
                            model.Dashboard.TongTaiKhoan = await db.TaiKhoans.CountAsync(x => x.DangHoatDong && !x.IsDeleted);
                            model.Dashboard.TongChuNha = await db.TaiKhoans.CountAsync(x => x.DangHoatDong && !x.IsDeleted && x.VaiTro == "CHU_NHA");
                            model.Dashboard.TongToaNha = await db.ToaNhas.CountAsync(x => x.DangHoatDong);
                            model.Dashboard.TongPhong = await db.PhongTros.CountAsync();
                            model.Dashboard.YeuCauChoXuLy = await db.YeuCauThues.CountAsync(x =>
                                x.TrangThai == TrangThaiYeuCau.Moi || x.TrangThai == TrangThaiYeuCau.DaHenLich);
                        }
                        else if (role is "CHU_NHA" or "QUAN_LY")
                        {
                            var buildingIds = await db.ToaNhas.AsNoTracking()
                                .Where(x => x.DangHoatDong && (role == "CHU_NHA" ? x.ChuNhaId == accountId : x.QuanLyId == accountId))
                                .Select(x => x.Id).ToListAsync();
                            if (role == "CHU_NHA")
                            {
                                model.Dashboard.BuildingOptions = await db.ToaNhas.AsNoTracking()
                                    .Where(x => buildingIds.Contains(x.Id)).OrderBy(x => x.TenToaNha)
                                    .Select(x => new HomeBuildingOption(x.Id, x.TenToaNha)).ToListAsync();
                                if (toaNhaId.HasValue) buildingIds = buildingIds.Where(x => x == toaNhaId.Value).ToList();
                            }
                            var rooms = db.PhongTros.AsNoTracking().Where(x => buildingIds.Contains(x.ToaNhaId));
                            model.Dashboard.TongToaNha = buildingIds.Count;
                            model.Dashboard.TongPhong = await rooms.CountAsync();
                            model.Dashboard.PhongTrong = await rooms.CountAsync(x => x.TrangThai == "TRONG");
                            model.Dashboard.PhongDangThue = await rooms.CountAsync(x => x.TrangThai == "DANG_THUE");
                            if (role == "CHU_NHA")
                            {
                                var dashboardNow = clock?.UtcNow ?? DateTime.UtcNow;
                                await LoadUnlistedRoomsAsync(model.Dashboard, accountId, toaNhaId, dashboardNow);
                                var pendingRequests = from request in db.YeuCauThues.AsNoTracking()
                                    join tenant in db.KhachThues on request.KhachThueId equals tenant.Id
                                    join listing in db.TinDangs on request.TinDangId equals listing.Id
                                    join room in rooms on listing.PhongId equals room.Id
                                    where request.TrangThai == TrangThaiYeuCau.Moi || request.TrangThai == TrangThaiYeuCau.DaHenLich
                                    orderby request.NgayTao
                                    select new { request.Id, Code = request.MaYeuCau, Room = room.MaPhong, Name = tenant.HoTen, Status = request.TrangThai, Created = request.NgayTao };
                                var overdueBefore = dashboardNow.AddHours(-24);
                                model.Dashboard.OverdueRequestCount = await pendingRequests.CountAsync(x => x.Created < overdueBefore);
                                var pendingItems = await pendingRequests.Take(3).ToListAsync();
                                model.Dashboard.PendingRequests = pendingItems.Select(x => new HomeRequestItem(x.Id, x.Code, x.Room, x.Name, x.Status, x.Created)).ToList();
                                model.Dashboard.PhongDatCoc = await rooms.CountAsync(x => x.TrangThai == "DA_DAT_COC");
                                model.Dashboard.Buildings = await db.ToaNhas.AsNoTracking()
                                    .Where(x => buildingIds.Contains(x.Id))
                                    .OrderBy(x => x.TenToaNha)
                                    .Select(x => new HomeBuildingItem(x.Id, x.TenToaNha, x.DiaChi,
                                        db.PhongTros.Count(r => r.ToaNhaId == x.Id),
                                        db.PhongTros.Count(r => r.ToaNhaId == x.Id && r.TrangThai == "DANG_THUE")))
                                    .ToListAsync();
                                if (hasPayments && await db.RolePermissions.AnyAsync(p => p.RoleCode == "CHU_NHA" &&
                                    p.ModuleCode == "TAI_CHINH" && (p.AccessLevel == "READ" || p.AccessLevel == "WRITE" || p.AccessLevel == "FULL")))
                                {
                                    var pendingPayments = from payment in db.ThanhToans.AsNoTracking()
                                        join invoice in db.HoaDons.AsNoTracking() on payment.HoaDonId equals invoice.Id
                                        join contract in db.HopDongs.AsNoTracking() on invoice.HopDongId equals contract.Id
                                        join room in rooms on contract.PhongId equals room.Id
                                        join tenant in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals tenant.Id into tenants
                                        from tenant in tenants.DefaultIfEmpty()
                                        where payment.TrangThai == "CHO_XAC_NHAN" && invoice.TrangThai == "DA_PHAT_HANH" &&
                                            invoice.Nam == selectedPeriod.Year && invoice.Thang == selectedPeriod.Month
                                        orderby payment.NgayThanhToan, payment.Id
                                        select new { InvoiceId = invoice.Id, Room = room.MaPhong, Name = tenant == null ? "Khách thuê" : tenant.HoTen,
                                            Amount = payment.SoTien, Date = payment.NgayThanhToan };
                                    model.Dashboard.PendingPaymentCount = await pendingPayments.CountAsync();
                                    model.Dashboard.PendingPaymentTotal = await pendingPayments.SumAsync(x => (long?)x.Amount) ?? 0;
                                    var paymentItems = await pendingPayments.Take(3).ToListAsync();
                                    model.Dashboard.PendingPayments = paymentItems.Select(x => new HomePaymentItem(x.InvoiceId, x.Room, x.Name, x.Amount, x.Date)).ToList();
                                    model.Dashboard.PaymentsAvailable = true;
                                    var paymentPeriodEnd = selectedPeriod.AddMonths(1).AddDays(-1);
                                    var collected = from payment in db.ThanhToans.AsNoTracking()
                                        join invoice in db.HoaDons.AsNoTracking() on payment.HoaDonId equals invoice.Id
                                        join contract in db.HopDongs.AsNoTracking() on invoice.HopDongId equals contract.Id
                                        join room in rooms on contract.PhongId equals room.Id
                                        where payment.TrangThai == "DA_XAC_NHAN" && payment.NgayThanhToan >= selectedPeriod && payment.NgayThanhToan <= paymentPeriodEnd
                                        select payment.SoTien;
                                    model.Dashboard.CollectedTotal = await collected.SumAsync(x => (long?)x) ?? 0;
                                    model.Dashboard.CollectedPaymentCount = await collected.CountAsync();
                                }
                            }
                            model.Dashboard.YeuCauChoXuLy = await (from request in db.YeuCauThues.AsNoTracking()
                                join listing in db.TinDangs.AsNoTracking() on request.TinDangId equals listing.Id
                                join room in rooms on listing.PhongId equals room.Id
                                where request.TrangThai == TrangThaiYeuCau.Moi || request.TrangThai == TrangThaiYeuCau.DaHenLich
                                select request.Id).CountAsync();
                        }
                        else if (role == "KHACH_THUE")
                        {
                            model.Dashboard.YeuCauCuaToi = await (from request in db.YeuCauThues.AsNoTracking()
                                join tenant in db.KhachThues.AsNoTracking() on request.KhachThueId equals tenant.Id
                                where tenant.TaiKhoanId == accountId
                                select request.Id).CountAsync();
                            model.Dashboard.YeuCauChoXuLy = await (from request in db.YeuCauThues.AsNoTracking()
                                join tenant in db.KhachThues.AsNoTracking() on request.KhachThueId equals tenant.Id
                                where tenant.TaiKhoanId == accountId &&
                                    (request.TrangThai == TrangThaiYeuCau.Moi || request.TrangThai == TrangThaiYeuCau.DaHenLich)
                                select request.Id).CountAsync();
                        }
                    }
                }
            }
            catch
            {
                // Fallback nếu schema CSDL chưa sẵn sàng trong các môi trường kiểm thử tối giản
            }
        }

        if (db != null && User.IsInRole("KHACH_THUE") &&
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var tenantAccountId))
        {
            await LoadTenantContractsAsync(model.Dashboard, tenantAccountId, localToday);
            await LoadTenantInvoicesAsync(model.Dashboard, tenantAccountId, localToday);
        }

        if (meterProgress != null && clock != null && User.IsInRole("CHU_NHA")
            && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
        {
            var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
            model.Dashboard.MeterAlerts = await meterProgress.LayCanhBaoAsync(ownerId, today, HttpContext.RequestAborted);
            model.Dashboard.MeterAlerts = model.Dashboard.MeterAlerts.Where(x =>
                x.Nam == selectedPeriod.Year && x.Thang == selectedPeriod.Month &&
                (!toaNhaId.HasValue || x.ToaNhaId == toaNhaId.Value)).ToList();
            if (db != null && await db.RolePermissions.AnyAsync(p => p.RoleCode == "CHU_NHA" && p.ModuleCode == "DIEN_NUOC" &&
                (p.AccessLevel == "READ" || p.AccessLevel == "WRITE" || p.AccessLevel == "FULL")))
                model.Dashboard.MeterRooms = await meterProgress.PhongChuaChotDashboardAsync(ownerId, selectedPeriod.Year, selectedPeriod.Month, toaNhaId, HttpContext.RequestAborted);
        }

        if(db != null && User.IsInRole("CHU_NHA") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var financeOwner))
        {
            var ids=await db.ToaNhas.Where(x=>x.ChuNhaId==financeOwner && x.DangHoatDong && (!toaNhaId.HasValue || x.Id==toaNhaId)).Select(x=>x.Id).ToArrayAsync();
            model.Dashboard.Finance=await new DashboardFinanceService(db).LoadAsync(financeOwner,"CHU_NHA",ids,selectedPeriod,localToday,HttpContext.RequestAborted);
        }
        return View(model);
    }

    private async Task LoadTenantContractsAsync(HomeDashboardViewModel dashboard, int accountId, DateOnly today)
    {
        if (db is null) return;
        var tables = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('hop_dong','ky_hop_dong','nguoi_o_ghep','khach_thue','tai_khoan','phong_tro','toa_nha','role_permission')").SingleAsync();
        if (tables != 8) { dashboard.TenantContractMessage = "Dữ liệu hợp đồng chưa sẵn sàng."; return; }
        if (!await db.RolePermissions.AnyAsync(p => p.RoleCode == "KHACH_THUE" && p.ModuleCode == "HOP_DONG" && (p.AccessLevel == "READ" || p.AccessLevel == "WRITE" || p.AccessLevel == "FULL")))
        { dashboard.TenantContractMessage = "Tài khoản chưa được cấp quyền xem hợp đồng."; return; }
        var profileId = await (from k in db.KhachThues.AsNoTracking() join a in db.TaiKhoans on k.TaiKhoanId equals a.Id
            where a.Id == accountId && a.VaiTro == "KHACH_THUE" && a.DangHoatDong && !a.IsDeleted select (int?)k.Id).SingleOrDefaultAsync();
        if (profileId is null) return;
        var contracts = await (from h in db.HopDongs.AsNoTracking()
            join r in db.PhongTros on h.PhongId equals r.Id join b in db.ToaNhas on r.ToaNhaId equals b.Id
            where (h.TrangThai == "DANG_HIEU_LUC" || h.TrangThai == "CHO_HIEU_LUC" || h.TrangThai == "DA_KET_THUC")
                && (h.KhachDungTenId == profileId || db.NguoiOGheps.Any(g => g.HopDongId == h.Id && g.KhachThueId == profileId))
            select new { h.Id, h.MaHopDong, h.KhachDungTenId, h.TrangThai, h.NgayTraPhong, r.MaPhong, b.TenToaNha }).ToListAsync();
        var ids = contracts.Select(h => h.Id).ToList();
        var periods = await db.KyHopDongs.AsNoTracking().Where(k => ids.Contains(k.HopDongId)).OrderBy(k => k.NgayBatDau).ToListAsync();
        var stays = await db.NguoiOGheps.AsNoTracking().Where(g => ids.Contains(g.HopDongId) && g.KhachThueId == profileId).OrderBy(g => g.NgayVao).ToListAsync();
        foreach (var h in contracts)
        {
            var terms = periods.Where(k => k.HopDongId == h.Id).ToList();
            var period = terms.FirstOrDefault(k => k.NgayBatDau <= today && k.NgayKetThuc >= today)
                ?? terms.FirstOrDefault(k => k.NgayBatDau > today) ?? terms.LastOrDefault();
            var signer = h.KhachDungTenId == profileId;
            var member = stays.FirstOrDefault(g => g.HopDongId == h.Id && g.NgayVao <= today && (g.NgayRa == null || g.NgayRa >= today))
                ?? stays.FirstOrDefault(g => g.HopDongId == h.Id && g.NgayVao > today) ?? stays.LastOrDefault(g => g.HopDongId == h.Id);
            var active = h.TrangThai == "DANG_HIEU_LUC" && (h.NgayTraPhong == null || h.NgayTraPhong >= today)
                && terms.Any(k => k.NgayBatDau <= today && k.NgayKetThuc >= today)
                && (signer || (member is not null && member.NgayVao <= today && (member.NgayRa == null || member.NgayRa >= today)));
            var future = h.TrangThai != "DA_KET_THUC" && (h.NgayTraPhong == null || h.NgayTraPhong >= today)
                && terms.Any(k => k.NgayKetThuc >= today) && (signer || (member is not null && (member.NgayRa == null || member.NgayRa >= today)));
            var label = active ? "Đang thuê" : future ? "Chờ bắt đầu thuê" : "Đã kết thúc thuê";
            if (period is null) label = "Chưa có thông tin kỳ hợp đồng";
            dashboard.TenantContracts.Add(new(h.MaHopDong, h.MaPhong, h.TenToaNha, signer ? "Người đứng tên" : "Người ở ghép", label,
                period?.GiaThue ?? 0, signer ? period?.NgayBatDau : member?.NgayVao, signer ? terms.LastOrDefault()?.NgayKetThuc : member?.NgayRa ?? terms.LastOrDefault()?.NgayKetThuc,
                active ? 0 : future ? 1 : 2));
        }
        dashboard.TenantContracts = dashboard.TenantContracts.OrderBy(c => c.Priority).ThenByDescending(c => c.Start).ToList();
    }

    private async Task LoadTenantInvoicesAsync(HomeDashboardViewModel dashboard, int accountId, DateOnly today)
    {
        if (db is null) return;
        var tables = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','chi_tiet_hoa_don','thanh_toan','hop_dong','khach_thue','tai_khoan','phong_tro','role_permission')").SingleAsync();
        if (tables != 8) { dashboard.TenantInvoiceMessage = "Dữ liệu hóa đơn chưa sẵn sàng."; return; }
        if (!await db.RolePermissions.AnyAsync(p => p.RoleCode == "KHACH_THUE" && p.ModuleCode == "TAI_CHINH" && (p.AccessLevel == "READ" || p.AccessLevel == "WRITE" || p.AccessLevel == "FULL")))
        { dashboard.TenantInvoiceMessage = "Tài khoản chưa được cấp quyền xem hóa đơn."; return; }
        var rows = await (from bill in db.HoaDons.AsNoTracking()
            join h in db.HopDongs on bill.HopDongId equals h.Id
            join k in db.KhachThues on h.KhachDungTenId equals k.Id
            join a in db.TaiKhoans on k.TaiKhoanId equals a.Id
            join room in db.PhongTros on h.PhongId equals room.Id
            where a.Id == accountId && a.VaiTro == "KHACH_THUE" && a.DangHoatDong && !a.IsDeleted && bill.TrangThai == "DA_PHAT_HANH"
            select new { bill.Id, bill.MaHoaDon, room.MaPhong, bill.Thang, bill.Nam, bill.HanThanhToan, Published = bill.NgayPhatHanh ?? bill.NgayLap,
                Total = db.ChiTietHoaDons.Where(l => l.HoaDonId == bill.Id).Sum(l => (long?)(l.LoaiKhoan == "GIAM_TRU" ? -l.ThanhTien : l.ThanhTien)) ?? 0L,
                Paid = db.ThanhToans.Where(p => p.HoaDonId == bill.Id && p.TrangThai == "DA_XAC_NHAN").Sum(p => (long?)p.SoTien) ?? 0L }).ToListAsync();
        var invoices = rows.Select(b => {
            var remaining = Math.Max(0L, checked(b.Total - b.Paid));
            var status = remaining == 0 ? "Đã thanh toán" : today > b.HanThanhToan ? "Quá hạn" : b.Paid > 0 ? "Thanh toán một phần" : "Chưa thanh toán";
            return new HomeTenantInvoice(b.MaHoaDon, b.MaPhong, b.Thang, b.Nam, b.HanThanhToan, b.Total, b.Paid, remaining, status, b.Published, b.Id);
        }).ToList();
        dashboard.TenantInvoicesReady = true;
        dashboard.TenantUnpaidInvoices = invoices.Count(b => b.Remaining > 0);
        dashboard.TenantAmountDue = invoices.Sum(b => (decimal)b.Remaining);
        dashboard.TenantInvoices = invoices.OrderByDescending(b => b.Published).ThenByDescending(b => b.Year).ThenByDescending(b => b.Month).ThenByDescending(b => b.Code).Take(5).ToList();
    }

    [HttpGet]
    public IActionResult GioiThieu() => View();

    public IActionResult Privacy() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [QL_PhongTro.Authorization.ModuleAccess("TAI_KHOAN", write: true)]
    public IActionResult ToggleRegistration()
    {
        settings.EnableDuplicateCheck = !settings.EnableDuplicateCheck;
        TempData["Msg"] = settings.EnableDuplicateCheck ? "Duplicate check enabled" : "Duplicate check disabled";
        return RedirectToAction("Index");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}
