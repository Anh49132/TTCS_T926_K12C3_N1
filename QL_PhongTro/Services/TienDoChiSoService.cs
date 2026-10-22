using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed class TienDoChiSoService(AppDbContext db)
{
    public async Task<List<HomeMeterAlert>> LayCanhBaoAsync(
        int ownerId, DateOnly today, CancellationToken ct = default)
    {
        await EnsureLandlordAsync(ownerId, ct);
        if (!await CoBangChiSoAsync(ct)) return [];

        var buildings = await (from building in db.ToaNhas.AsNoTracking()
            join room in db.PhongTros.AsNoTracking() on building.Id equals room.ToaNhaId
            join contract in db.HopDongs.AsNoTracking() on room.Id equals contract.PhongId
            join period in db.KyHopDongs.AsNoTracking() on contract.Id equals period.HopDongId
            where building.ChuNhaId == ownerId && building.DangHoatDong
                && (contract.TrangThai == "DANG_HIEU_LUC" || contract.TrangThai == "DA_KET_THUC")
                && period.NgayBatDau <= today
            group period by new { building.Id, building.TenToaNha, building.NgayChotHangThang } into periods
            select new
            {
                periods.Key.Id,
                periods.Key.TenToaNha,
                periods.Key.NgayChotHangThang,
                NgayBatDau = periods.Min(x => x.NgayBatDau)
            }).ToListAsync(ct);

        var dueBuildingsByMonth = new Dictionary<(int Year, int Month), List<(int Id, string Name)>>();
        foreach (var building in buildings)
        {
            var month = new DateOnly(building.NgayBatDau.Year, building.NgayBatDau.Month, 1);
            var current = new DateOnly(today.Year, today.Month, 1);
            while (month <= current)
            {
                var cutoffDay = Math.Min(building.NgayChotHangThang, DateTime.DaysInMonth(month.Year, month.Month));
                if (new DateOnly(month.Year, month.Month, cutoffDay) <= today)
                {
                    var key = (month.Year, month.Month);
                    if (!dueBuildingsByMonth.TryGetValue(key, out var dueBuildings))
                        dueBuildingsByMonth.Add(key, dueBuildings = []);
                    dueBuildings.Add((building.Id, building.TenToaNha));
                }
                month = month.AddMonths(1);
            }
        }

        var alerts = new List<HomeMeterAlert>();
        foreach (var (period, dueBuildings) in dueBuildingsByMonth.OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month))
        {
            var progress = await XemAsync(ownerId, period.Year, period.Month, ct);
            var summaries = progress.ToaNhas.ToDictionary(x => x.ToaNhaId);
            foreach (var building in dueBuildings)
            {
                if (summaries.TryGetValue(building.Id, out var summary)
                    && !summary.KyDaKhoa && summary.SoPhongConThieu > 0)
                    alerts.Add(new HomeMeterAlert(building.Id, building.Name, period.Year, period.Month,
                        summary.SoPhongConThieu));
            }
        }

        return alerts.OrderBy(x => x.Nam).ThenBy(x => x.Thang).ThenBy(x => x.TenToaNha).ToList();
    }

    public async Task<TienDoChiSoViewModel> XemAsync(int ownerId, int year, int month, CancellationToken ct)
        => await XemAsync(ownerId, year, month, ct, null);

    public async Task<TienDoChiSoViewModel> XemAsync(
        int ownerId, int year, int month, CancellationToken ct, int? buildingId)
    {
        ValidatePeriod(year, month);
        await EnsureLandlordAsync(ownerId, ct);

        var ownerBuildings = db.ToaNhas.AsNoTracking().Where(x => x.ChuNhaId == ownerId);
        if (buildingId.HasValue)
        {
            if (!await ownerBuildings.AnyAsync(x => x.Id == buildingId.Value, ct))
                throw new UnauthorizedAccessException();
            ownerBuildings = ownerBuildings.Where(x => x.Id == buildingId.Value);
        }
        var buildings = await ownerBuildings
            .OrderBy(x => x.TenToaNha)
            .Select(x => new { x.Id, x.TenToaNha, ManagerName = db.TaiKhoans.Where(a => a.Id == x.QuanLyId).Select(a => a.HoTen).FirstOrDefault() })
            .ToListAsync(ct);
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var lockedBuildings = await KyChiSoLockService.LayToaNhaDaKhoaAsync(
            db, buildings.Select(x => x.Id).ToArray(), year, month, ct);
        var leasedRooms = await GetLeasedRoomsAsync(ownerId, first, last, ct);
        var confirmedContracts = await GetConfirmedContractsAsync(leasedRooms, first, last, ct);

        var summaries = leasedRooms.GroupBy(x => x.Id).ToDictionary(
            group => group.Key,
            group =>
            {
                var roomGroups = group.GroupBy(x => x.RoomId);
                var total = roomGroups.Count();
                var confirmed = roomGroups.Count(room => room.All(x => confirmedContracts.Contains(x.ContractId)));
                return (Total: total, Confirmed: confirmed);
            });

        return new TienDoChiSoViewModel
        {
            Nam = year,
            Thang = month,
            ToaNhas = buildings.Select(building =>
            {
                var counts = summaries.GetValueOrDefault(building.Id);
                var total = counts.Total;
                return new TienDoChiSoToaNha(building.Id, building.TenToaNha, total, counts.Confirmed,
                    total - counts.Confirmed, lockedBuildings.Contains(building.Id)) { TenQuanLy = building.ManagerName };
            }).ToList()
        };
    }

    public async Task<PhongConThieuViewModel> PhongConThieuAsync(
        int ownerId, int buildingId, int year, int month, CancellationToken ct)
    {
        ValidatePeriod(year, month);
        await EnsureLandlordAsync(ownerId, ct);

        var building = await db.ToaNhas.AsNoTracking()
            .Where(x => x.Id == buildingId && x.ChuNhaId == ownerId)
            .Select(x => new { x.Id, x.TenToaNha })
            .SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();

        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var leasedRooms = await GetLeasedRoomsAsync(ownerId, first, last, ct);
        var confirmedContracts = await GetConfirmedContractsAsync(leasedRooms, first, last, ct);
        var missingRooms = leasedRooms
            .Where(x => x.Id == building.Id)
            .GroupBy(x => x.RoomId)
            .Where(room => room.Any(x => !confirmedContracts.Contains(x.ContractId)))
            .Select(room => room.First())
            .OrderBy(x => x.Floor)
            .ThenBy(x => x.RoomCode)
            .Select(x => new PhongConThieuItem(x.RoomId, x.RoomCode, x.Floor, x.ManagerName))
            .ToList();

        return new PhongConThieuViewModel(building.Id, building.TenToaNha, year, month, missingRooms);
    }

    public async Task<List<HomeMeterRoom>> PhongChuaChotDashboardAsync(int ownerId, int year, int month, int? buildingId, CancellationToken ct)
    {
        ValidatePeriod(year, month);
        await EnsureLandlordAsync(ownerId, ct);
        if (!await CoBangChiSoAsync(ct)) return [];
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var activeBuildings = await db.ToaNhas.AsNoTracking().Where(x => x.ChuNhaId == ownerId && x.DangHoatDong &&
            (!buildingId.HasValue || x.Id == buildingId.Value)).Select(x => x.Id).ToListAsync(ct);
        var rooms = (await GetLeasedRoomsAsync(ownerId, first, last, ct)).Where(x => activeBuildings.Contains(x.Id)).ToList();
        var confirmed = await GetConfirmedContractsAsync(rooms, first, last, ct);
        rooms = rooms.Where(x => !confirmed.Contains(x.ContractId)).ToList();
        var ids = rooms.Select(x => x.ContractId).Distinct().ToList();
        var contracts = await (from contract in db.HopDongs.AsNoTracking()
            join tenant in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals tenant.Id into tenants
            from tenant in tenants.DefaultIfEmpty()
            where ids.Contains(contract.Id)
            select new { contract.Id, contract.NgayChot, Name = tenant == null ? "Khách thuê" : tenant.HoTen }).ToDictionaryAsync(x => x.Id, ct);
        var services = await db.DichVus.AsNoTracking().Where(x => x.MaDichVu == "DIEN" || x.MaDichVu == "NUOC")
            .Select(x => new { x.Id, x.MaDichVu }).ToListAsync(ct);
        var readings = await db.ChiSoDienNuocs.AsNoTracking().Where(x => ids.Contains(x.HopDongId) &&
            x.TuNgay <= first && x.DenNgay >= last && x.ChiSoDau >= 0 && x.ChiSoCuoi >= x.ChiSoDau)
            .Select(x => new { x.HopDongId, x.DichVuId }).ToListAsync(ct);
        var pricing = new DichVuPhongService(db, new DichVuService(db));
        var result = new List<HomeMeterRoom>();
        foreach (var room in rooms.DistinctBy(x => x.ContractId).OrderBy(x => x.Floor).ThenBy(x => x.RoomCode))
        {
            var contract = contracts[room.ContractId];
            var configMissing = services.Count != 2 || contract.NgayChot is < 1 or > 31;
            var missingElectricity = false;
            var missingWater = false;
            var cutoff = new DateOnly(year, month, Math.Clamp(contract.NgayChot, 1, last.Day));
            foreach (var service in services)
            {
                var price = await pricing.LayGiaHoaDonAsync(ownerId, room.RoomId, service.Id, cutoff);
                if (price is null || price.DonGia <= 0 || (price.CachTinh != QL_PhongTro.Models.CachTinhDichVu.TheoNguoi &&
                    price.CachTinh != QL_PhongTro.Models.CachTinhDichVu.TheoChiSo)) { configMissing = true; continue; }
                if (price.CachTinh == QL_PhongTro.Models.CachTinhDichVu.TheoNguoi) continue;
                var missing = !readings.Any(x => x.HopDongId == room.ContractId && x.DichVuId == service.Id);
                if (service.MaDichVu == "DIEN") missingElectricity = missing;
                else missingWater = missing;
            }
            result.Add(new HomeMeterRoom(room.Id, room.RoomCode, contract.Name, missingElectricity, missingWater, configMissing));
        }
        return result.GroupBy(x => new { x.BuildingId, x.Code }).Select(g => new HomeMeterRoom(g.Key.BuildingId, g.Key.Code,
            g.First().TenantName, g.Any(x => x.MissingElectricity), g.Any(x => x.MissingWater), g.Any(x => x.NeedsConfiguration))).ToList();
    }

    private async Task EnsureLandlordAsync(int ownerId, CancellationToken ct)
    {
        if (!await db.TaiKhoans.AsNoTracking().AnyAsync(x => x.Id == ownerId
            && x.VaiTro == "CHU_NHA" && x.DangHoatDong && !x.IsDeleted, ct))
            throw new UnauthorizedAccessException();
    }

    private async Task<bool> CoBangChiSoAsync(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var close = connection.State != System.Data.ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        try
        {
            using var command = connection.CreatePortableCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='chi_so_dien_nuoc'";
            return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
        }
        finally
        {
            if (close) await connection.CloseAsync();
        }
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 1900 or > 9998 || month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Kỳ theo dõi không hợp lệ.");
    }

    private Task<List<LeasedRoom>> GetLeasedRoomsAsync(
        int ownerId, DateOnly first, DateOnly last, CancellationToken ct) =>
        (from building in db.ToaNhas.AsNoTracking()
         join room in db.PhongTros.AsNoTracking() on building.Id equals room.ToaNhaId
         join contract in db.HopDongs.AsNoTracking() on room.Id equals contract.PhongId
         join period in db.KyHopDongs.AsNoTracking() on contract.Id equals period.HopDongId
         where building.ChuNhaId == ownerId
             && (contract.TrangThai == "DANG_HIEU_LUC" || contract.TrangThai == "DA_KET_THUC")
             && period.NgayBatDau <= last && period.NgayKetThuc >= first
             && (!contract.NgayTraPhong.HasValue || contract.NgayTraPhong.Value >= first)
         select new LeasedRoom(
             building.Id, room.Id, room.MaPhong, room.Tang, contract.Id, building.ChuNhaId,
             db.TaiKhoans.Where(account => account.Id == building.QuanLyId)
                 .Select(account => account.HoTen).FirstOrDefault()))
        .Distinct()
        .ToListAsync(ct);

    private async Task<HashSet<int>> GetConfirmedContractsAsync(
        List<LeasedRoom> leasedRooms, DateOnly first, DateOnly last, CancellationToken ct)
    {
        var serviceIds = await db.DichVus.AsNoTracking()
            .Where(x => x.MaDichVu == "DIEN" || x.MaDichVu == "NUOC").Select(x => x.Id).ToListAsync(ct);
        var contractIds = leasedRooms.Select(x => x.ContractId).Distinct().ToList();
        var readings = await db.ChiSoDienNuocs.AsNoTracking()
            .Where(x => contractIds.Contains(x.HopDongId) && x.TuNgay <= first && x.DenNgay >= last
                && x.ChiSoDau >= 0 && x.ChiSoCuoi >= x.ChiSoDau)
            .Select(x => new { x.HopDongId, x.DichVuId }).ToListAsync(ct);
        var cutoffs = await db.HopDongs.AsNoTracking().Where(x => contractIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.NgayChot, ct);
        var pricing = new DichVuPhongService(db, new DichVuService(db));
        var confirmedContracts = new HashSet<int>();
        foreach (var room in leasedRooms.DistinctBy(x => x.ContractId))
        {
            if (serviceIds.Count != 2 || !cutoffs.TryGetValue(room.ContractId, out var day) || day is < 1 or > 31) continue;
            var cutoff = new DateOnly(first.Year, first.Month, Math.Min(day, last.Day));
            var ready = true;
            foreach (var serviceId in serviceIds)
            {
                ct.ThrowIfCancellationRequested();
                var price = await pricing.LayGiaHoaDonAsync(room.OwnerId, room.RoomId, serviceId, cutoff);
                if (price is null || price.DonGia <= 0) { ready = false; break; }
                if (price.CachTinh == QL_PhongTro.Models.CachTinhDichVu.TheoNguoi) continue;
                if (price.CachTinh != QL_PhongTro.Models.CachTinhDichVu.TheoChiSo ||
                    !readings.Any(x => x.HopDongId == room.ContractId && x.DichVuId == serviceId))
                { ready = false; break; }
            }
            if (ready) confirmedContracts.Add(room.ContractId);
        }

        return confirmedContracts;
    }

    private sealed record LeasedRoom(
        int Id, int RoomId, string RoomCode, int Floor, int ContractId, int OwnerId, string? ManagerName);
}
