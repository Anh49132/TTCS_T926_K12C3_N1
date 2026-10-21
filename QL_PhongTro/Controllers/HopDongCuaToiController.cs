using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "KHACH_THUE")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class HopDongCuaToiController(AppDbContext db, HopDongPdfService pdfService, ITimeProvider? clock = null) : Controller
{
    private static readonly string[] ContractTables =
        ["hop_dong", "ky_hop_dong", "nguoi_o_ghep"];

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await ContractSchemaReadyAsync())
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new HopDongCuaToiViewModel
            {
                SchemaReady = false,
                Message = "Dữ liệu hợp đồng chưa sẵn sàng trên CSDL này. Hãy liên hệ quản trị viên để kiểm tra schema hợp đồng."
            });
        }

        var profile = await CurrentProfileAsync();
        if (profile is null)
            return View(new HopDongCuaToiViewModel
            {
                Message = "Bạn chưa có hồ sơ khách thuê liên kết với tài khoản. Vui lòng hoàn thiện hồ sơ để tra cứu hợp đồng."
            });

        var contracts = await ContractRows()
            .Where(x => x.KhachDungTenId == profile.Id ||
                db.NguoiOGheps.Any(g => g.HopDongId == x.Id && g.KhachThueId == profile.Id))
            .ToListAsync();
        var contractIds = contracts.Select(x => x.Id).ToArray();
        var periods = await db.KyHopDongs.AsNoTracking()
            .Where(x => contractIds.Contains(x.HopDongId))
            .OrderBy(x => x.NgayBatDau)
            .ToListAsync();
        var today = TodayInVietnam();
        foreach (var contract in contracts)
        {
            var contractPeriods = periods.Where(x => x.HopDongId == contract.Id).ToList();
            var latest = contractPeriods.LastOrDefault();
            contract.GiaThue = ApplicablePeriod(contractPeriods, today,
                x => x.NgayBatDau, x => x.NgayKetThuc)?.GiaThue ?? 0;
            contract.NgayBatDau = contractPeriods.FirstOrDefault()?.NgayBatDau;
            contract.NgayKetThuc = latest?.NgayKetThuc;
            contract.SoNgayConLai = SoNgayConLai(contract.NgayKetThuc, today);
            contract.VaiTro = contract.KhachDungTenId == profile.Id ? "Người đứng tên" : "Người ở cùng";
            contract.Category = CategoryFor(contract, today);
        }
        contracts = contracts.OrderByDescending(x => x.NgayBatDau).ToList();

        return View(new HopDongCuaToiViewModel { Contracts = contracts });
    }

    [HttpGet]
    public async Task<IActionResult> Details(string? maHopDong)
    {
        var result = await LoadContractDetailsAsync(maHopDong);
        return result.Error ?? View(result.Model);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadPdf(string? maHopDong)
    {
        var result = await LoadContractDetailsAsync(maHopDong);
        if (result.Error is not null)
            return result.Error;

        var safeCode = System.Text.RegularExpressions.Regex.Replace(
            result.Model!.MaHopDong, @"[^\p{L}\p{N}._-]", "_");
        return File(pdfService.Generate(result.Model), "application/pdf", $"HopDong-{safeCode}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> PreviewPdf(string? maHopDong)
    {
        var result = await LoadContractDetailsAsync(maHopDong);
        return result.Error ?? View(result.Model);
    }

    [HttpGet]
    public async Task<IActionResult> PdfPreviewFile(string? maHopDong)
    {
        var result = await LoadContractDetailsAsync(maHopDong);
        if (result.Error is not null)
            return result.Error;

        return File(pdfService.Generate(result.Model!), "application/pdf");
    }

    private async Task<(HopDongChiTietViewModel? Model, IActionResult? Error)> LoadContractDetailsAsync(string? maHopDong)
    {
        if (string.IsNullOrWhiteSpace(maHopDong))
            return (null, MissingContract());

        if (!await ContractSchemaReadyAsync())
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return (null, View("Unavailable"));
        }

        var contract = await ContractRows().SingleOrDefaultAsync(x => x.MaHopDong == maHopDong.Trim());
        if (contract is null)
            return (null, MissingContract());

        var profile = await CurrentProfileAsync();
        if (profile is null || (contract.KhachDungTenId != profile.Id &&
            !await db.NguoiOGheps.AnyAsync(g => g.HopDongId == contract.Id && g.KhachThueId == profile.Id)))
            return (null, StatusCode(StatusCodes.Status403Forbidden));

        var periods = await db.KyHopDongs.AsNoTracking()
            .Where(x => x.HopDongId == contract.Id)
            .OrderBy(x => x.NgayBatDau)
            .Select(x => new KyHopDongViewModel
            {
                NgayBatDau = x.NgayBatDau,
                NgayKetThuc = x.NgayKetThuc,
                GiaThue = x.GiaThue
            }).ToListAsync();

        var guests = await db.NguoiOGheps.AsNoTracking()
            .Where(x => x.HopDongId == contract.Id)
            .OrderBy(x => x.NgayVao)
            .ToListAsync();
        var guestIds = guests.Select(x => x.KhachThueId).Distinct().ToArray();
        var guestNames = await db.KhachThues.AsNoTracking()
            .Where(x => guestIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.HoTen);

        var today = TodayInVietnam();
        var applicablePeriod = ApplicablePeriod(periods, today,
            x => x.NgayBatDau, x => x.NgayKetThuc);
        var people = new List<NguoiOViewModel>();
        var ownerProfile = await db.KhachThues.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == contract.KhachDungTenId);
        if (ownerProfile is not null)
        {
            people.Add(new NguoiOViewModel
            {
                HoTen = ownerProfile.HoTen,
                VaiTro = "Người đứng tên",
                NgayVao = periods.FirstOrDefault()?.NgayBatDau ?? DateOnly.MinValue,
                NgayChuyenDi = contract.NgayTraPhong,
                DangO = IsCurrentlyStaying(contract.TrangThai, contract.NgayTraPhong, periods, today)
            });
        }

        people.AddRange(guests.Select(g => new NguoiOViewModel
        {
            HoTen = guestNames.TryGetValue(g.KhachThueId, out var name) ? name : $"Khách thuê #{g.KhachThueId}",
            VaiTro = "Người ở cùng",
            NgayVao = g.NgayVao,
            NgayChuyenDi = g.NgayRa,
            DangO = IsCurrentlyStaying(contract.TrangThai, contract.NgayTraPhong, periods, today)
                && g.NgayVao <= today && (g.NgayRa is null || g.NgayRa >= today)
        }));

        var appliedServices = await AppliedServicesAsync(contract.Id);
        var roomInfo = await (from h in db.HopDongs.AsNoTracking()
            join room in db.PhongTros.AsNoTracking() on h.PhongId equals room.Id
            where h.Id == contract.Id
            select new { h.NgayChot, room.SoNguoiToiDa, room.TrangThai,
                Listing = db.TinDangs.Where(t => t.PhongId == room.Id && (t.TrangThai == "DA_CHO_THUE" || t.TrangThai == "DANG_HIEN_THI"))
                    .OrderByDescending(t => t.Id).Select(t => (int?)t.Id).FirstOrDefault(),
                Months = db.KyHopDongs.Where(k => k.HopDongId == h.Id).Sum(k => k.SoThang),
                Image = db.AnhPhongs.Where(a => a.PhongId == room.Id && !a.DangChoXoa)
                    .OrderBy(a => a.ThuTu).Select(a => a.DuongDanAnhNho ?? a.DuongDan).FirstOrDefault() }).SingleAsync();
        DateOnly? nextCutoff = null;
        if (roomInfo.NgayChot is >= 1 and <= 31 && contract.TrangThai == "DANG_HIEU_LUC")
        {
            var month = new DateOnly(today.Year, today.Month, 1);
            var date = month.AddDays(Math.Min(roomInfo.NgayChot, DateTime.DaysInMonth(month.Year, month.Month)) - 1);
            if (date <= today) { month = month.AddMonths(1); date = month.AddDays(Math.Min(roomInfo.NgayChot, DateTime.DaysInMonth(month.Year, month.Month)) - 1); }
            if (periods.Any(p => p.NgayBatDau <= date && p.NgayKetThuc >= date)) nextCutoff = date;
        }
        return (new HopDongChiTietViewModel
        {
            NgayChot = roomInfo.NgayChot,
            SoNguoiToiDa = roomInfo.SoNguoiToiDa,
            ThoiHanThang = roomInfo.Months,
            AnhPhong = roomInfo.Image,
            TinDangId = roomInfo.Listing,
            TrangThaiPhong = roomInfo.TrangThai,
            NgayChotTiepTheo = nextCutoff,
            MaHopDong = contract.MaHopDong,
            MaPhong = contract.MaPhong,
            TenToaNha = contract.TenToaNha,
            DiaChiToaNha = contract.DiaChiToaNha,
            TienCoc = contract.TienCoc,
            TrangThai = contract.TrangThai,
            NgayTraPhong = contract.NgayTraPhong,
            GiaThueHienTai = applicablePeriod?.GiaThue ?? 0,
            CacKy = periods,
            NguoiO = people,
            DichVus = appliedServices,
            SoNgayConLai = SoNgayConLai(periods.LastOrDefault()?.NgayKetThuc, today)
        }, null);
    }

    private async Task<List<DichVuHopDongViewModel>> AppliedServicesAsync(int contractId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        try
        {
            if (openedHere) await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'hop_dong_dich_vu'";
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
                return [];
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }

        return await db.HopDongDichVus.AsNoTracking()
            .Where(x => x.HopDongId == contractId)
            .OrderBy(x => x.TenDichVu)
            .Select(x => new DichVuHopDongViewModel
            {
                TenDichVu = x.TenDichVu,
                CachTinh = x.CachTinh,
                DonViTinh = x.DonViTinh,
                DonGia = x.DonGia
            }).ToListAsync();
    }

    private async Task<KhachThue?> CurrentProfileAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId))
            return null;
        return await db.KhachThues.AsNoTracking().SingleOrDefaultAsync(x => x.TaiKhoanId == accountId);
    }

    private IQueryable<HopDongCardViewModel> ContractRows() =>
        from contract in db.HopDongs.AsNoTracking()
        join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
        join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
        select new HopDongCardViewModel
        {
            Id = contract.Id,
            MaHopDong = contract.MaHopDong,
            KhachDungTenId = contract.KhachDungTenId,
            MaPhong = room.MaPhong,
            TenToaNha = building.TenToaNha,
            DiaChiToaNha = building.DiaChi,
            TienCoc = contract.TienCoc,
            TrangThai = contract.TrangThai,
            NgayTraPhong = contract.NgayTraPhong
        };

    private async Task<bool> ContractSchemaReadyAsync()
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        try
        {
            if (openedHere) await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('hop_dong', 'ky_hop_dong', 'nguoi_o_ghep')";
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == ContractTables.Length;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private ViewResult MissingContract()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    private DateOnly TodayInVietnam() =>
        DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7));

    public static int? SoNgayConLai(DateOnly? ngayKetThuc, DateOnly today) =>
        ngayKetThuc is { } end ? end.DayNumber - today.DayNumber : null;

    private static T? ApplicablePeriod<T>(IEnumerable<T> periods, DateOnly today,
        Func<T, DateOnly> startDate, Func<T, DateOnly> endDate) where T : class =>
        periods.FirstOrDefault(x => startDate(x) <= today && endDate(x) >= today) ??
        periods.Where(x => endDate(x) < today).LastOrDefault() ??
        periods.FirstOrDefault();

    private static string CategoryFor(HopDongCardViewModel contract, DateOnly today)
    {
        if (contract.TrangThai == "DA_KET_THUC" ||
            (contract.NgayKetThuc.HasValue && contract.NgayKetThuc.Value < today) ||
            (contract.NgayTraPhong.HasValue && contract.NgayTraPhong.Value < today))
            return "ended";

        return contract.TrangThai == "DANG_HIEU_LUC" ? "active" : "other";
    }

    private static bool IsCurrentlyStaying(string status, DateOnly? returnDate,
        IReadOnlyCollection<KyHopDongViewModel> periods, DateOnly today) =>
        status == "DANG_HIEU_LUC" &&
        (returnDate is null || returnDate >= today) &&
        periods.Any(x => x.NgayBatDau <= today && x.NgayKetThuc >= today);
}
