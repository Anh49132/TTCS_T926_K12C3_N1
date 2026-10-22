using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace QL_PhongTro.Services;

public sealed partial class HoaDonDichVuService
{
    private async Task KiemTraChuNhaAsync(int actor, int building)
    {
        if (!await db.TaiKhoans.AnyAsync(x => x.Id == actor && x.VaiTro == "CHU_NHA" && x.DangHoatDong && !x.IsDeleted)
            || !await services.SoHuuToaNhaAsync(actor, building)) throw new UnauthorizedAccessException();
    }

    public static string ReviewFingerprint(HoaDonThangDuKien row) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        row.HoaDon.HopDongId, row.PhienBanPhong, row.HoaDon.SoNguoiTinhPhi, row.HoaDon.NgayChot, row.HoaDon.TongTien,
        Lines = row.HoaDon.ChiTiet.Select(x => new { x.BaoHongId, x.DichVuId, x.CauHinhDichVuId, x.KyHopDongId, x.TenKhoan, x.CachTinhApDung, x.DonViTinh, x.DonGia, x.SoLuong, x.ChiSoDau, x.ChiSoCuoi, x.ThanhTien }),
        Meters = row.ChiSo.Select(x => new { x.Id, x.PhienBan })
    }))));

    public (DateOnly Issue, DateOnly Due) NgayHoaDon(DateOnly? issue = null, DateOnly? due = null)
    {
        var date = issue ?? DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7));
        if (date.Year is < 1900 or > 9998) throw new InvalidOperationException("Ngày phát hành ngoài phạm vi hỗ trợ.");
        var deadline = due ?? date.AddDays(7);
        if (deadline.Year is < 1900 or > 9998 || deadline < date)
            throw new InvalidOperationException("Hạn thanh toán phải từ ngày phát hành trở đi.");
        return (date, deadline);
    }

    public async Task<PhatHanhThangViewModel> XemThangAsync(int actor, int building, int year, int month, DateOnly? issueDate = null, DateOnly? dueDate = null)
    {
        await KiemTraChuNhaAsync(actor, building);
        if (year is < 1900 or > 9998 || month is < 1 or > 12) throw new InvalidOperationException("Kỳ hóa đơn không hợp lệ.");
        var dates = NgayHoaDon(issueDate, dueDate);
        var model = new PhatHanhThangViewModel { ToaNhaId = building, Nam = year, Thang = month, NgayPhatHanh = dates.Issue, HanThanhToan = dates.Due, SanSang = await SanSangAsync() };
        if (!model.SanSang) return model;
        // A GET never installs optional database modules.
        if (!await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='chi_so_dien_nuoc'").AnyAsync(x => x == 1))
        { model.SanSang = false; return model; }
        if (!await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('hoa_don') WHERE name='ngay_phat_hanh_nghiep_vu'").AnyAsync(x => x == 1))
        { model.SanSang = false; return model; }
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var contracts = await (from h in db.HopDongs.AsNoTracking() join p in db.PhongTros on h.PhongId equals p.Id
            where p.ToaNhaId == building && p.TrangThai == "DANG_THUE" && h.TrangThai == "DANG_HIEU_LUC"
            orderby p.MaPhong select new { Contract = h, Room = p }).ToListAsync();
        var meterServices = await db.DichVus.AsNoTracking().Where(x => x.MaDichVu == "DIEN" || x.MaDichVu == "NUOC").OrderBy(x => x.MaDichVu).ToListAsync();
        var pricing = new DichVuPhongService(db, services);
        var rentalRooms = await db.PhongTros.AsNoTracking().Where(x => x.ToaNhaId == building && x.TrangThai == "DANG_THUE").ToListAsync();
        model.TongPhong = rentalRooms.Count;
        foreach (var room in rentalRooms.Where(x => !contracts.Any(h => h.Room.Id == x.Id)))
            model.BoQua.Add(new(0, room.MaPhong, "Chưa có hợp đồng hiệu lực."));
        foreach (var context in contracts)
        {
            var h = context.Contract;
            var terms = await db.KyHopDongs.AsNoTracking().Where(x => x.HopDongId == h.Id
                && x.NgayBatDau <= last && x.NgayKetThuc >= first).OrderBy(x => x.NgayBatDau).ToListAsync();
            void Skip(string reason) => model.BoQua.Add(new(h.Id, context.Room.MaPhong, reason));
            var existing = await db.HoaDons.AsNoTracking().Where(x => x.HopDongId == h.Id && x.Nam == year && x.Thang == month && x.TrangThai != "DA_HUY")
                .Select(x => (int?)x.Id).SingleOrDefaultAsync();
            if (existing.HasValue)
            {
                model.BoQua.Add(new(h.Id, context.Room.MaPhong, $"Đã có hóa đơn tháng {month:00}/{year}.")
                    { TrangThai = "DA_CO_HOA_DON", HoaDonId = existing });
                continue;
            }
            if (h.NgayTraPhong < last) { Skip("Chưa có kỳ hợp đồng thuê trọn tháng hợp lệ."); continue; }
            List<ChiTietHoaDon> rentLines;
            try { rentLines = TinhChiTietTienPhong(terms, first, last); }
            catch (InvalidOperationException ex) { Skip(ex.Message); continue; }
            var cancelled = await db.HoaDons.AsNoTracking().Where(x => x.HopDongId == h.Id && x.Nam == year && x.Thang == month && x.TrangThai == "DA_HUY")
                .OrderByDescending(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync();
            if (cancelled.HasValue) {
                model.BoQua.Add(new(h.Id, context.Room.MaPhong, "Kỳ này có bản đã hủy. Mở bản cũ để tạo Nháp thay thế.")
                    { TrangThai="DA_CO_HOA_DON", HoaDonId=cancelled });
                continue;
            }
            if (h.NgayChot is < 1 or > 31) { Skip("Ngày chốt hợp đồng không hợp lệ."); continue; }
            var cutoff = new DateOnly(year, month, Math.Min(h.NgayChot, last.Day));
            SoNguoiHoaDon occupancy;
            try { occupancy = await LaySoNguoiAsync(actor, h.Id, building, first); }
            catch (InvalidOperationException ex) { Skip(ex.Message); continue; }
            var agreed = await db.HopDongDichVus.Where(x => x.HopDongId == h.Id).Select(x => x.DichVuId).ToListAsync();
            var now = (clock ?? new SystemTimeProvider()).UtcNow;
            var invoice = new HoaDon { MaHoaDon = "HD" + Guid.NewGuid().ToString("N")[..24], HopDongId = h.Id,
                Nam = year, Thang = month, TuNgay = first, DenNgay = last, NgayChot = cutoff,
                SoNguoiTinhPhi = occupancy.SoNguoi, NgayLap = now, NgayPhatHanhNghiepVu = dates.Issue, HanThanhToan = dates.Due, NguoiLapId = actor };
            invoice.ChiTiet.AddRange(rentLines);
            var readings = new List<ChiSoDienNuoc>();
            var problems = new List<string>();
            var missing = new List<string>();
            var requiredReadings = 0;
            if (meterServices.Count != 2) problems.Add("Chưa cấu hình đủ dịch vụ điện và nước.");
            foreach (var service in meterServices)
            {
                var name = service.MaDichVu == "DIEN" ? "điện" : "nước";
                if (agreed.Count > 0 && !agreed.Contains(service.Id)) { problems.Add($"Dịch vụ {name} không thuộc hợp đồng."); continue; }
                var price = await pricing.LayGiaHoaDonAsync(actor, context.Room.Id, service.Id, cutoff);
                if (price is null || price.DonGia <= 0 || price.CachTinh is not (CachTinhDichVu.TheoChiSo or CachTinhDichVu.TheoNguoi))
                { problems.Add($"Thiếu đơn giá {name} đang áp dụng."); continue; }
                // Flat utilities use the contract's billable residents, not meter readings.
                if (price.CachTinh == CachTinhDichVu.TheoNguoi)
                {
                    invoice.ChiTiet.Add(new() { SoThuTu = invoice.ChiTiet.Count + 1, DichVuId = service.Id,
                        CauHinhDichVuId = price.CauHinhId, TenKhoan = price.TenDichVu,
                        CachTinhApDung = price.CachTinh, DonViTinh = price.DonViTinh, DonGia = price.DonGia,
                        SoLuong = occupancy.SoNguoi, ThanhTien = ThanhTien(occupancy.SoNguoi, price.DonGia) });
                    continue;
                }
                requiredReadings++;
                var reading = await db.ChiSoDienNuocs.AsNoTracking().SingleOrDefaultAsync(x => x.HopDongId == h.Id && x.DichVuId == service.Id && x.TuNgay == first && x.DenNgay == last);
                if (reading is null) { missing.Add(name); continue; }
                if (reading.DaKhoa || reading.ChiSoDau < 0 || reading.ChiSoCuoi < reading.ChiSoDau)
                { problems.Add($"Chỉ số {name} bị khóa hoặc không hợp lệ."); continue; }
                readings.Add(reading);
                invoice.ChiTiet.Add(new() { SoThuTu = invoice.ChiTiet.Count + 1, DichVuId = service.Id, CauHinhDichVuId = price.CauHinhId,
                    TenKhoan = price.TenDichVu, CachTinhApDung = price.CachTinh, DonViTinh = price.DonViTinh, DonGia = price.DonGia,
                    ChiSoDau = reading.ChiSoDau, ChiSoCuoi = reading.ChiSoCuoi, SoLuong = reading.ChiSoCuoi - reading.ChiSoDau,
                    ThanhTien = ThanhTien(reading.ChiSoCuoi - reading.ChiSoDau, price.DonGia) });
            }
            // Only metered utilities require readings; invalid/missing prices still block billing.
            if (missing.Count == 2) problems.Insert(0, "Chưa chốt chỉ số điện và nước.");
            else if (missing.Count == 1) problems.Insert(0, $"Thiếu chỉ số {missing[0]}.");
            if (problems.Count > 0 || readings.Count != requiredReadings)
            {
                model.BoQua.Add(new(h.Id, context.Room.MaPhong, string.Join(" ", problems)) { ThieuChiSo = missing.Count > 0 });
                continue;
            }
            var assignedServices = await db.DichVuPhongs.AsNoTracking()
                .Where(x => x.PhongId == context.Room.Id)
                .Select(x => x.DichVuToaNha.DichVuId).Distinct().OrderBy(x => x).ToListAsync();
            foreach (var serviceId in assignedServices)
            {
                if (invoice.ChiTiet.Any(x => x.DichVuId == serviceId)
                    || (agreed.Count > 0 && !agreed.Contains(serviceId))) continue;
                var price = await pricing.LayGiaHoaDonAsync(actor, context.Room.Id, serviceId, cutoff);
                if (price is null || price.CachTinh is not (CachTinhDichVu.CoDinh or CachTinhDichVu.TheoNguoi)) continue;
                var quantity = price.CachTinh == CachTinhDichVu.TheoNguoi ? occupancy.SoNguoi : 1;
                invoice.ChiTiet.Add(new()
                {
                    SoThuTu = invoice.ChiTiet.Count + 1, DichVuId = serviceId, CauHinhDichVuId = price.CauHinhId,
                    TenKhoan = price.TenDichVu, CachTinhApDung = price.CachTinh, DonViTinh = price.DonViTinh,
                    SoLuong = quantity, DonGia = price.DonGia, ThanhTien = ThanhTien(quantity, price.DonGia)
                });
            }
            invoice.TongTien = TongCong(invoice.ChiTiet);
            var tenant = await db.KhachThues.AsNoTracking().Where(x => x.Id == h.KhachDungTenId).Select(x => x.HoTen).SingleOrDefaultAsync();
            model.DuKien.Add(new(context.Room.MaPhong, invoice, readings) { Tang = context.Room.Tang, TenKhach = tenant ?? "", PhienBanPhong = context.Room.PhienBan });
        }
        var history = await (from invoice in db.HoaDons.AsNoTracking().Include(x => x.ChiTiet)
            join h in db.HopDongs on invoice.HopDongId equals h.Id join p in db.PhongTros on h.PhongId equals p.Id
            where p.ToaNhaId == building && invoice.Nam == year && invoice.Thang == month
            orderby p.MaPhong select new { p.MaPhong, Invoice = invoice }).ToListAsync();
        model.DaPhatHanh = history.Select(x => new HoaDonThangDuKien(x.MaPhong, x.Invoice, [])).ToList();
        return model;
    }

    public async Task<int> PhatHanhThangAsync(int actor, int building, int year, int month, int? contractId = null,
        DateOnly? issueDate = null, DateOnly? dueDate = null) =>
        (await PhatHanhDanhSachAsync(actor, building, year, month, contractId.HasValue ? [contractId.Value] : null, issueDate, dueDate)).SoDaPhatHanh;

    public async Task<KetQuaPhatHanhThang> PhatHanhDanhSachAsync(int actor, int building, int year, int month,
        IReadOnlyCollection<int>? selectedIds = null, DateOnly? issueDate = null, DateOnly? dueDate = null,
        IReadOnlyDictionary<int, string>? expected = null, bool taoNhap = false)
    {
        var timer = Stopwatch.StartNew();
        var started = (clock ?? new SystemTimeProvider()).UtcNow;
        await db.Database.OpenConnectionAsync();
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite);
        var model = await XemThangAsync(actor, building, year, month, issueDate, dueDate);
        if (!model.SanSang) throw new InvalidOperationException("Chức năng hóa đơn chưa được thiết lập.");
        var results = model.BoQua.Select(x => new KetQuaPhongHoaDon(x.HopDongId, x.MaPhong, x.TrangThai, x.LyDo, x.HoaDonId)
            { ThieuChiSo = x.ThieuChiSo }).ToList();
        if (selectedIds is not null)
        {
            var belongs = await (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id
                where selectedIds.Contains(h.Id) && p.ToaNhaId == building select new { h.Id, p.MaPhong }).ToListAsync();
            if (selectedIds.Distinct().Count() != belongs.Count) throw new UnauthorizedAccessException();
            foreach (var context in belongs.Where(x => !model.DuKien.Any(row => row.HoaDon.HopDongId == x.Id)
                && !results.Any(row => row.MaPhong == x.MaPhong)))
                results.Add(new(context.Id, context.MaPhong, "BO_QUA", "Phòng hoặc hợp đồng không còn đủ điều kiện. Hãy kiểm tra lại trước khi phát hành."));
            foreach (var row in model.DuKien.Where(x => !selectedIds.Contains(x.HoaDon.HopDongId)))
                results.Add(new(row.HoaDon.HopDongId, row.MaPhong, "KHONG_CHON", "Không nằm trong danh sách đã xác nhận phát hành."));
            model.DuKien = model.DuKien.Where(x => selectedIds.Contains(x.HoaDon.HopDongId)).ToList();
        }
        if (expected is not null)
        {
            foreach (var row in model.DuKien.Where(x => !expected.TryGetValue(x.HoaDon.HopDongId, out var fingerprint) || fingerprint != ReviewFingerprint(x)).ToList())
            {
                results.Add(new(row.HoaDon.HopDongId, row.MaPhong, "BO_QUA", "Dữ liệu đã thay đổi sau khi kiểm tra. Hãy tải lại trước khi phát hành."));
                model.DuKien.Remove(row);
            }
        }
        foreach (var row in model.DuKien)
        {
            if (taoNhap) row.HoaDon.NgayPhatHanhNghiepVu = null;
            db.HoaDons.Add(row.HoaDon);
            foreach (var reading in row.ChiSo.Where(_ => !taoNhap))
            {
                var tracked = await db.ChiSoDienNuocs.FindAsync(reading.Id);
                tracked!.DaKhoa = true; tracked.PhienBan++;
            }
        }
        await db.SaveChangesAsync();
        foreach (var row in model.DuKien.Where(_ => !taoNhap))
        {
            row.HoaDon.TrangThai = "DA_PHAT_HANH";
            row.HoaDon.NgayPhatHanh = (clock ?? new SystemTimeProvider()).UtcNow;
            row.HoaDon.NguoiPhatHanhId = actor;
        }
        await db.SaveChangesAsync();
        results.AddRange(model.DuKien.Select(x => new KetQuaPhongHoaDon(x.HoaDon.HopDongId, x.MaPhong, taoNhap ? "NHAP" : "DA_PHAT_HANH", taoNhap ? "Đã tạo Nháp, chưa khóa chỉ số và chưa thông báo cho khách." : "Đã phát hành và khóa chỉ số điện nước.", x.HoaDon.Id)
            { MaHoaDon = x.HoaDon.MaHoaDon, TenKhach = x.TenKhach, Tang = x.Tang, TongTien = x.HoaDon.TongTien }));
        var buildingName = await db.ToaNhas.Where(x => x.Id == building).Select(x => x.TenToaNha).SingleAsync();
        var actorName = await db.TaiKhoans.Where(x => x.Id == actor).Select(x => x.HoTen).SingleAsync();
        await sqlite.CommitAsync();
        timer.Stop();
        return new(results.OrderBy(x => x.MaPhong, StringComparer.Ordinal).ToList())
        {
            ToaNhaId = building, TenToaNha = buildingName, NguoiThucHienId = actor, NguoiThucHien = actorName,
            Nam = year, Thang = month, NgayPhatHanh = model.NgayPhatHanh, HanThanhToan = model.HanThanhToan,
            BatDauUtc = started, HoanTatUtc = (clock ?? new SystemTimeProvider()).UtcNow, SoGiay = timer.Elapsed.TotalSeconds
        };
    }
}
