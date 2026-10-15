using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed class BaoCaoService(AppDbContext db, ITimeProvider clock)
{
    private static readonly TimeZoneInfo VnZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public DateTime GioHienTaiVn =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), VnZone);

    public async Task<BaoCaoDoanhThuViewModel> LayBaoCaoThangAsync(
        int actorId,
        string role,
        int? toaNhaId,
        int thang,
        int nam,
        CancellationToken ct = default)
    {
        var model = new BaoCaoDoanhThuViewModel
        {
            ToaNhaId = toaNhaId,
            TuThang = thang,
            DenThang = thang,
            TuNam = nam,
            DenNam = nam
        };

        if (thang is < 1 or > 12 || nam is < 1900 or > 9999)
        {
            model.ErrorMessage = "Tháng hoặc năm không hợp lệ.";
            return model;
        }
        if (role == "KHACH_THUE")
            throw new UnauthorizedAccessException("Khách thuê không có quyền xem báo cáo tài chính.");

        var buildingsQuery = db.ToaNhas.AsNoTracking().Where(x => x.DangHoatDong);
        if (role == "CHU_NHA")
            buildingsQuery = buildingsQuery.Where(x => x.ChuNhaId == actorId);
        else if (role == "QUAN_LY")
            buildingsQuery = buildingsQuery.Where(x => x.QuanLyId == actorId);

        var buildings = await buildingsQuery
            .OrderBy(x => x.TenToaNha)
            .Select(x => new { x.Id, x.TenToaNha })
            .ToListAsync(ct);
        if (toaNhaId.HasValue && buildings.All(x => x.Id != toaNhaId.Value))
            throw new UnauthorizedAccessException("Không có quyền truy cập dữ liệu của tòa nhà này.");

        model.TenToaNhaDangChon = toaNhaId.HasValue
            ? buildings.First(x => x.Id == toaNhaId.Value).TenToaNha
            : "Tất cả tòa nhà";
        model.DanhSachToaNha =
        [
            new SelectListItem("Tất cả tòa nhà", "", !toaNhaId.HasValue),
            .. buildings.Select(x => new SelectListItem(x.TenToaNha, x.Id.ToString(), x.Id == toaNhaId))
        ];

        var buildingIds = toaNhaId.HasValue
            ? new[] { toaNhaId.Value }
            : buildings.Select(x => x.Id).ToArray();
        if (buildingIds.Length == 0)
            return model;

        var buildingRows = buildings.Select(x => (x.Id, x.TenToaNha)).ToArray();
        model.LapDayTheoToaNha = await TinhLapDayTheoToaNhaAsync(buildingRows, buildingIds, thang, nam, ct);
        model.TiLeLapDayHienTai = toaNhaId.HasValue
            ? model.LapDayTheoToaNha.FirstOrDefault()?.TiLeLapDay
            : null;

        var invoices = await (
            from invoice in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs.AsNoTracking() on invoice.HopDongId equals contract.Id
            join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
            where buildingIds.Contains(room.ToaNhaId)
                  && invoice.Nam == nam && invoice.Thang == thang
                  && invoice.TrangThai == "DA_PHAT_HANH"
            select new { invoice.Id, invoice.TongTien }
        ).ToListAsync(ct);

        model.TongPhatHanh = invoices.Sum(x => x.TongTien);
        var invoiceIds = invoices.Select(x => x.Id).ToArray();
        if (invoiceIds.Length == 0)
            return model;

        model.TongDaThu = await db.ThanhToans.AsNoTracking()
            .Where(x => invoiceIds.Contains(x.HoaDonId) && x.TrangThai == "DA_XAC_NHAN")
            .SumAsync(x => (long?)x.SoTien, ct) ?? 0L;

        var hasDepositLedger = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='giao_dich_coc'")
            .SingleAsync(ct) == 1;
        long depositedOffsets = 0;
        if (hasDepositLedger)
        {
            depositedOffsets = await db.GiaoDichCocs.AsNoTracking()
                .Where(x => x.HoaDonId.HasValue && invoiceIds.Contains(x.HoaDonId.Value)
                            && x.Loai == "CAN_TRU" && x.TrangThai == "DA_XAC_NHAN")
                .SumAsync(x => (long?)x.SoTien, ct) ?? 0L;
        }

        model.TongConPhaiThu = Math.Max(0L, checked(model.TongPhatHanh - model.TongDaThu - depositedOffsets));
        model.TiLeThuTongThe = model.TongPhatHanh == 0
            ? 0m
            : Math.Round((decimal)model.TongDaThu / model.TongPhatHanh * 100m, 1, MidpointRounding.AwayFromZero);
        return model;
    }

    private async Task<List<BaoCaoLapDayToaNhaRow>> TinhLapDayTheoToaNhaAsync(
        IReadOnlyCollection<(int Id, string TenToaNha)> buildings,
        int[] buildingIds,
        int month,
        int year,
        CancellationToken ct)
    {
        var lastDay = DateTime.DaysInMonth(year, month);
        var lastDate = new DateOnly(year, month, lastDay);
        var cutoffLocal = new DateTime(year, month, lastDay, 23, 59, 59, DateTimeKind.Unspecified)
            .AddTicks(TimeSpan.TicksPerSecond - 1);
        var cutoffUtc = TimeZoneInfo.ConvertTimeToUtc(cutoffLocal, VnZone);
        var rooms = await db.PhongTros.AsNoTracking()
            .Where(x => buildingIds.Contains(x.ToaNhaId))
            .Select(x => new { x.Id, x.ToaNhaId, x.TrangThai, x.NgayTao })
            .ToListAsync(ct);
        var roomIds = rooms.Select(x => x.Id).ToArray();

        var hasHistory = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='lich_su_trang_thai_phong'")
            .SingleAsync(ct) == 1;
        var historiesByRoom = new Dictionary<int, LichSuTrangThaiPhong[]>();
        if (hasHistory && roomIds.Length > 0)
        {
            var histories = await db.LichSuTrangThaiPhongs.AsNoTracking()
                .Where(x => roomIds.Contains(x.PhongId) && x.TuThoiDiem <= cutoffUtc)
                .ToListAsync(ct);
            historiesByRoom = histories.GroupBy(x => x.PhongId)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(h => h.TuThoiDiem).ToArray());
        }

        var activePeriodsByRoom = new Dictionary<int, bool>();
        if (roomIds.Length > 0)
        {
            var periods = await (
                from contract in db.HopDongs.AsNoTracking()
                join period in db.KyHopDongs.AsNoTracking() on contract.Id equals period.HopDongId
                where roomIds.Contains(contract.PhongId)
                      && (contract.TrangThai == "DANG_HIEU_LUC" || contract.TrangThai == "DA_KET_THUC")
                      && period.NgayBatDau <= lastDate && period.NgayKetThuc >= lastDate
                      && (!contract.NgayTraPhong.HasValue || contract.NgayTraPhong.Value >= lastDate)
                select new { contract.PhongId }
            ).ToListAsync(ct);
            activePeriodsByRoom = periods.Select(x => x.PhongId)
                .Distinct().ToDictionary(x => x, _ => true);
        }

        var rows = buildings.Select(x => new BaoCaoLapDayToaNhaRow
        {
            ToaNhaId = x.Id,
            TenToaNha = x.TenToaNha
        }).ToDictionary(x => x.ToaNhaId);

        foreach (var room in rooms)
        {
            if (room.NgayTao > cutoffUtc)
                continue;

            var state = historiesByRoom.TryGetValue(room.Id, out var roomHistory)
                ? roomHistory.FirstOrDefault(x => x.DenThoiDiem == null || x.DenThoiDiem > cutoffUtc)?.TrangThai
                : null;
            state ??= activePeriodsByRoom.ContainsKey(room.Id)
                ? "DANG_THUE"
                : room.TrangThai == "DANG_THUE" ? "TRONG" : room.TrangThai;

            var row = rows[room.ToaNhaId];
            if (state == "DANG_THUE")
            {
                row.SoPhongDangThue++;
                row.TongPhongChoThue++;
            }
            else if (state is "TRONG" or "DA_DAT_COC")
            {
                row.TongPhongChoThue++;
                if (state == "TRONG")
                    row.SoPhongTrong++;
            }
        }

        foreach (var row in rows.Values)
        {
            row.TiLeLapDay = row.TongPhongChoThue == 0
                ? null
                : Math.Round((decimal)row.SoPhongDangThue / row.TongPhongChoThue * 100m, 1, MidpointRounding.AwayFromZero);
        }
        return rows.Values.OrderBy(x => x.TenToaNha).ToList();
    }

    public async Task<BaoCaoDoanhThuViewModel> LayBaoCaoAsync(
        int actorId,
        string role,
        int? toaNhaId,
        int? tuThang,
        int? tuNam,
        int? denThang,
        int? denNam,
        CancellationToken ct = default)
    {
        // 1. Phân quyền và danh sách toà nhà được xem
        if (role == "KHACH_THUE")
            throw new UnauthorizedAccessException("Khách thuê không có quyền xem báo cáo tài chính.");

        var visibleBuildingsQuery = db.ToaNhas.AsNoTracking().Where(x => x.DangHoatDong);
        if (role == "CHU_NHA")
            visibleBuildingsQuery = visibleBuildingsQuery.Where(x => x.ChuNhaId == actorId);
        else if (role == "QUAN_LY")
            visibleBuildingsQuery = visibleBuildingsQuery.Where(x => x.QuanLyId == actorId);

        var visibleBuildings = await visibleBuildingsQuery
            .OrderBy(x => x.TenToaNha)
            .Select(x => new { x.Id, x.TenToaNha })
            .ToListAsync(ct);

        // Kiểm tra toà nhà được chọn
        if (toaNhaId.HasValue && !visibleBuildings.Any(x => x.Id == toaNhaId.Value))
            throw new UnauthorizedAccessException("Không có quyền truy cập dữ liệu của toà nhà này.");

        var model = new BaoCaoDoanhThuViewModel
        {
            ToaNhaId = toaNhaId,
            TenToaNhaDangChon = toaNhaId.HasValue
                ? visibleBuildings.FirstOrDefault(x => x.Id == toaNhaId.Value)?.TenToaNha ?? "Toà nhà"
                : "Tất cả toà nhà",
            DanhSachToaNha =
            [
                new SelectListItem("Tất cả toà nhà", "", !toaNhaId.HasValue),
                .. visibleBuildings.Select(b => new SelectListItem(b.TenToaNha, b.Id.ToString(), b.Id == toaNhaId))
            ]
        };

        // 2. Chuẩn hoá và kiểm tra khoảng tháng
        var now = GioHienTaiVn;
        denThang ??= now.Month;
        denNam ??= now.Year;

        if (!tuThang.HasValue || !tuNam.HasValue)
        {
            var defaultEnd = new DateOnly(denNam.Value, denThang.Value, 1);
            var defaultStart = defaultEnd.AddMonths(-5);
            tuThang = defaultStart.Month;
            tuNam = defaultStart.Year;
        }

        model.TuThang = tuThang.Value;
        model.TuNam = tuNam.Value;
        model.DenThang = denThang.Value;
        model.DenNam = denNam.Value;

        if (tuThang < 1 || tuThang > 12 || denThang < 1 || denThang > 12 ||
            tuNam < 1900 || tuNam > 9999 || denNam < 1900 || denNam > 9999)
        {
            model.ErrorMessage = "Tháng hoặc năm không hợp lệ.";
            return model;
        }

        var startDate = new DateOnly(tuNam.Value, tuThang.Value, 1);
        var endDate = new DateOnly(denNam.Value, denThang.Value, 1);

        if (startDate > endDate)
        {
            model.ErrorMessage = "Tháng bắt đầu không được lớn hơn tháng kết thúc.";
            return model;
        }

        int monthsCount = (denNam.Value - tuNam.Value) * 12 + (denThang.Value - tuThang.Value) + 1;
        if (monthsCount > 12)
        {
            model.ErrorMessage = "Khoảng thời gian xem báo cáo tối đa là 12 tháng một lần xem.";
            return model;
        }

        var targetBuildingIds = toaNhaId.HasValue
            ? new List<int> { toaNhaId.Value }
            : visibleBuildings.Select(b => b.Id).ToList();

        if (targetBuildingIds.Count == 0)
            return model;

        // 3. Chuẩn bị khoảng thời gian cho cả bảng (12 tháng tối đa) và biểu đồ (6 tháng gần nhất tính đến denNam/denThang)
        var chartStart = endDate.AddMonths(-5); // 6 tháng gần nhất
        var earliestDate = startDate < chartStart ? startDate : chartStart;
        var latestDate = endDate;

        // 4. Tải dữ liệu phòng
        var rooms = await db.PhongTros.AsNoTracking()
            .Where(p => targetBuildingIds.Contains(p.ToaNhaId))
            .Select(p => new
            {
                p.Id,
                p.ToaNhaId,
                p.MaPhong,
                p.TrangThai,
                p.NgayTao
            })
            .ToListAsync(ct);

        var roomIds = rooms.Select(r => r.Id).ToList();
        if (roomIds.Count == 0)
            return model;

        // 5. Tải hoá đơn đã phát hành trong phạm vi thời gian mở rộng
        var invoices = await (
            from h in db.HoaDons.AsNoTracking()
            join c in db.HopDongs.AsNoTracking() on h.HopDongId equals c.Id
            where roomIds.Contains(c.PhongId)
                  && h.TrangThai == "DA_PHAT_HANH"
                  && (h.Nam > earliestDate.Year || (h.Nam == earliestDate.Year && h.Thang >= earliestDate.Month))
                  && (h.Nam < latestDate.Year || (h.Nam == latestDate.Year && h.Thang <= latestDate.Month))
            select new
            {
                h.Id,
                h.HopDongId,
                c.PhongId,
                h.Thang,
                h.Nam,
                h.TongTien
            }
        ).ToListAsync(ct);

        var invoiceIds = invoices.Select(i => i.Id).ToList();

        // 6. Tải các khoản thanh toán đã xác nhận
        var payments = invoiceIds.Count == 0 ? [] : await db.ThanhToans.AsNoTracking()
            .Where(t => invoiceIds.Contains(t.HoaDonId) && t.TrangThai == "DA_XAC_NHAN")
            .Select(t => new { t.HoaDonId, t.SoTien })
            .ToListAsync(ct);

        var paymentsByInvoice = payments
            .GroupBy(t => t.HoaDonId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.SoTien));

        // 7. Tải cấn cọc nếu có sổ cọc
        var hasGiaoDichCoc = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='giao_dich_coc'"
        ).SingleAsync(ct) == 1;

        Dictionary<int, long> offsetsByInvoice = [];
        if (hasGiaoDichCoc && invoiceIds.Count > 0)
        {
            var offsets = await db.GiaoDichCocs.AsNoTracking()
                .Where(g => g.HoaDonId.HasValue
                            && invoiceIds.Contains(g.HoaDonId.Value)
                            && g.Loai == "CAN_TRU"
                            && g.TrangThai == "DA_XAC_NHAN")
                .Select(g => new { HoaDonId = g.HoaDonId!.Value, g.SoTien })
                .ToListAsync(ct);

            offsetsByInvoice = offsets
                .GroupBy(g => g.HoaDonId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SoTien));
        }

        // 8. Tải lịch sử trạng thái phòng nếu có bảng lich_su_trang_thai_phong
        var hasLichSuPhong = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='lich_su_trang_thai_phong'"
        ).SingleAsync(ct) == 1;

        List<LichSuTrangThaiPhong> roomHistories = [];
        if (hasLichSuPhong && roomIds.Count > 0)
        {
            roomHistories = await db.LichSuTrangThaiPhongs.AsNoTracking()
                .Where(h => roomIds.Contains(h.PhongId))
                .ToListAsync(ct);
        }
        var roomHistoriesByRoom = roomHistories
            .GroupBy(h => h.PhongId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.TuThoiDiem).ToArray());

        // 9. Tải hợp đồng và các kỳ hợp đồng liên quan để đối chiếu khi thiếu lịch sử
        var contractPeriods = await (
            from c in db.HopDongs.AsNoTracking()
            join k in db.KyHopDongs.AsNoTracking() on c.Id equals k.HopDongId
            where roomIds.Contains(c.PhongId)
                  && c.TrangThai != "NHAP" && c.TrangThai != "DA_HUY"
                  && k.NgayBatDau <= new DateOnly(latestDate.Year, latestDate.Month, DateTime.DaysInMonth(latestDate.Year, latestDate.Month))
                  && (!c.NgayTraPhong.HasValue || c.NgayTraPhong.Value >= earliestDate)
            select new
            {
                c.Id,
                c.PhongId,
                c.TrangThai,
                c.NgayTraPhong,
                k.NgayBatDau,
                k.NgayKetThuc
            }
        ).ToListAsync(ct);
        var contractPeriodsByRoom = contractPeriods
            .GroupBy(c => c.PhongId)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var invoicesByMonth = invoices
            .GroupBy(i => (i.Nam, i.Thang))
            .ToDictionary(g => g.Key, g => g.ToArray());

        // 10. Hàm tính toán chỉ số cho một tháng cụ thể
        BaoCaoThangRow TinhThang(int nam, int thang)
        {
            var monthInvoices = invoicesByMonth.GetValueOrDefault((nam, thang)) ?? [];
            long tongPhatHanh = monthInvoices.Sum(i => i.TongTien);
            long tongDaThu = 0;
            long tongCanTru = 0;

            foreach (var inv in monthInvoices)
            {
                if (paymentsByInvoice.TryGetValue(inv.Id, out var paid))
                    tongDaThu = checked(tongDaThu + paid);
                if (offsetsByInvoice.TryGetValue(inv.Id, out var offset))
                    tongCanTru = checked(tongCanTru + offset);
            }

            long soConPhaiThu = checked(tongPhatHanh - tongDaThu - tongCanTru);
            if (soConPhaiThu < 0) soConPhaiThu = 0;

            decimal tiLeThu = tongPhatHanh == 0
                ? 0m
                : Math.Round((decimal)tongDaThu / tongPhatHanh * 100m, 1, MidpointRounding.AwayFromZero);

            // Tỉ lệ lấp đầy tại ngày cuối tháng
            int lastDayOfMonth = DateTime.DaysInMonth(nam, thang);
            var lastDayDate = new DateOnly(nam, thang, lastDayOfMonth);
            // Thời điểm chốt tính bằng UTC tương ứng 23:59:59.999 giờ Việt Nam (UTC+7)
            var cutoffUtc = new DateTime(nam, thang, lastDayOfMonth, 23, 59, 59, 999, DateTimeKind.Utc).AddHours(-7);

            int soPhongDangThue = 0;
            int tongPhongChoThue = 0;

            foreach (var r in rooms)
            {
                // Phòng tạo sau thời điểm chốt không tính vào tháng đó
                if (r.NgayTao > cutoffUtc)
                    continue;

                string trangThai = "TRONG";

                // Ưu tiên 1: Đọc từ lich_su_trang_thai_phong
                if (hasLichSuPhong)
                {
                    var h = roomHistoriesByRoom.GetValueOrDefault(r.Id, [])
                        .FirstOrDefault(x => x.TuThoiDiem <= cutoffUtc && (x.DenThoiDiem == null || x.DenThoiDiem > cutoffUtc));

                    if (h != null)
                    {
                        trangThai = h.TrangThai;
                        goto EvaluateState;
                    }
                }

                // Ưu tiên 2: Đối chiếu hợp đồng hiệu lực tại ngày cuối tháng
                var activeContract = contractPeriodsByRoom.GetValueOrDefault(r.Id, []).Any(c =>
                    c.NgayBatDau <= lastDayDate
                    && c.NgayKetThuc >= lastDayDate
                    && (!c.NgayTraPhong.HasValue || c.NgayTraPhong.Value >= lastDayDate));

                if (activeContract)
                {
                    trangThai = "DANG_THUE";
                }
                else if (r.TrangThai == "NGUNG_CHO_THUE")
                {
                    trangThai = "NGUNG_CHO_THUE";
                }
                else
                {
                    trangThai = "TRONG";
                }

            EvaluateState:
                if (trangThai == "DANG_THUE")
                {
                    soPhongDangThue++;
                    tongPhongChoThue++;
                }
                else if (trangThai is "TRONG" or "DA_DAT_COC")
                {
                    tongPhongChoThue++;
                }
                // NGUNG_CHO_THUE bị loại khỏi tổng số phòng đang cho thuê của toà
            }

            decimal? tiLeLapDay = tongPhongChoThue == 0
                ? null
                : Math.Round((decimal)soPhongDangThue / tongPhongChoThue * 100m, 1, MidpointRounding.AwayFromZero);

            return new BaoCaoThangRow
            {
                Nam = nam,
                Thang = thang,
                TongPhatHanh = tongPhatHanh,
                TongDaThu = tongDaThu,
                TongCanTru = tongCanTru,
                SoConPhaiThu = soConPhaiThu,
                TiLeThu = tiLeThu,
                SoPhongDangThue = soPhongDangThue,
                TongPhongChoThue = tongPhongChoThue,
                TiLeLapDay = tiLeLapDay
            };
        }

        // 11. Tạo danh sách các tháng trong phạm vi người dùng lọc (tối đa 12 tháng)
        var currentMonth = startDate;
        while (currentMonth <= endDate)
        {
            var row = TinhThang(currentMonth.Year, currentMonth.Month);
            model.ChiTietTheoThang.Add(row);
            currentMonth = currentMonth.AddMonths(1);
        }

        // 12. Tạo biểu đồ 6 tháng gần nhất (tính đến denNam/denThang)
        var chartMonth = chartStart;
        while (chartMonth <= endDate)
        {
            var row = model.ChiTietTheoThang.FirstOrDefault(r => r.Nam == chartMonth.Year && r.Thang == chartMonth.Month)
                      ?? TinhThang(chartMonth.Year, chartMonth.Month);

            model.BieuDo6Thang.Add(new BaoCaoChartItem
            {
                Nam = row.Nam,
                Thang = row.Thang,
                TongPhatHanh = row.TongPhatHanh,
                TongDaThu = row.TongDaThu,
                SoConPhaiThu = row.SoConPhaiThu,
                TiLeThu = row.TiLeThu,
                TiLeLapDay = row.TiLeLapDay
            });

            chartMonth = chartMonth.AddMonths(1);
        }

        // 13. Tính tổng hợp KPI cho khoảng được lọc
        model.TongPhatHanh = model.ChiTietTheoThang.Sum(x => x.TongPhatHanh);
        model.TongDaThu = model.ChiTietTheoThang.Sum(x => x.TongDaThu);
        model.TongConPhaiThu = checked(model.TongPhatHanh - model.TongDaThu - model.ChiTietTheoThang.Sum(x => x.TongCanTru));
        if (model.TongConPhaiThu < 0) model.TongConPhaiThu = 0;

        model.TiLeThuTongThe = model.TongPhatHanh == 0
            ? 0m
            : Math.Round((decimal)model.TongDaThu / model.TongPhatHanh * 100m, 1, MidpointRounding.AwayFromZero);

        // Tỉ lệ lấp đầy hiện tại = tháng mới nhất trong khoảng lọc
        model.TiLeLapDayHienTai = model.ChiTietTheoThang.LastOrDefault()?.TiLeLapDay;

        return model;
    }
}

