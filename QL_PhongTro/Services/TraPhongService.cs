using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

// Reuses invoice snapshots: DenNgay is the actual handover date, utility lines keep final readings.
// Creating the last invoice does not close the contract or settle its deposit.
public sealed class TraPhongService(AppDbContext db, ITimeProvider clock)
{
    public DateOnly HomNay => DateOnly.FromDateTime(clock.UtcNow.AddHours(7));

    public async Task KiemTraQuyenAsync(int actor, int contract)
    {
        if (!await (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id join a in db.TaiKhoans on b.ChuNhaId equals a.Id
            where h.Id == contract && a.Id == actor && a.VaiTro == "CHU_NHA" && a.DangHoatDong && !a.IsDeleted
            select h.Id).AnyAsync()) throw new UnauthorizedAccessException();
    }

    public async Task<TraPhongViewModel> NhapAsync(int actor, TraPhongInput input, bool tinh = false)
    {
        await KiemTraQuyenAsync(actor, input.HopDongId);
        var h = await db.HopDongs.AsNoTracking().SingleAsync(x => x.Id == input.HopDongId);
        var p = await db.PhongTros.AsNoTracking().SingleAsync(x => x.Id == h.PhongId);
        var b = await db.ToaNhas.AsNoTracking().SingleAsync(x => x.Id == p.ToaNhaId);
        var terms = await db.KyHopDongs.AsNoTracking().Where(x => x.HopDongId == h.Id).OrderBy(x => x.NgayBatDau).ToListAsync();
        if (terms.Count == 0) throw new InvalidOperationException("Hợp đồng chưa có kỳ thuê.");
        var model = new TraPhongViewModel { Input = input, MaHopDong = h.MaHopDong, Phong = p.MaPhong, ToaNha = b.TenToaNha,
            Khach = await db.KhachThues.Where(x => x.Id == h.KhachDungTenId).Select(x => x.HoTen).SingleOrDefaultAsync() ?? "",
            NgayKetThuc = terms.Max(x => x.NgayKetThuc), PhienBanPhong = p.PhienBan };
        model.HoaDonDaCo = await db.HoaDons.Where(x => x.HopDongId == h.Id && x.LoaiHoaDon == "KY_CUOI" && x.TrangThai != "DA_HUY")
            .Select(x => (int?)x.Id).SingleOrDefaultAsync();
        if (model.HoaDonDaCo.HasValue)
        {
            if (tinh) throw new InvalidOperationException("Hợp đồng đã có hóa đơn kỳ cuối. Hãy mở hóa đơn đã lưu.");
            return model;
        }
        if (h.TrangThai != "DANG_HIEU_LUC" || p.TrangThai != "DANG_THUE")
            throw new InvalidOperationException("Chỉ ghi nhận trả phòng cho hợp đồng đang hiệu lực và phòng đang thuê.");
        if (tinh && !input.NgayTra.HasValue) throw new InvalidOperationException("Chọn ngày trả phòng thực tế.");
        var day = input.NgayTra ?? (model.NgayKetThuc < HomNay ? model.NgayKetThuc : HomNay);
        if (day.Year is < 1900 or > 9998 || day > HomNay || !terms.Any(x => x.NgayBatDau <= day && x.NgayKetThuc >= day))
            throw new InvalidOperationException("Ngày trả phải nằm trong kỳ hợp đồng và không được sau hôm nay.");
        if (!tinh && input.NgayTra is null) input.NgayTra = day;
        var first = new DateOnly(day.Year, day.Month, 1);
        var start = terms[0].NgayBatDau > first ? terms[0].NgayBatDau : first;
        if (tinh && await db.HoaDons.AnyAsync(x => x.HopDongId == h.Id && x.Nam == day.Year && x.Thang == day.Month && x.TrangThai != "DA_HUY"))
            throw new InvalidOperationException("Tháng trả phòng đã có hóa đơn chưa hủy. Hãy xử lý hóa đơn cũ trước để tránh tính tiền trùng.");
        if (tinh && await db.HoaDons.AnyAsync(x => x.HopDongId == h.Id && x.TrangThai != "DA_HUY" && x.DenNgay > day))
            throw new InvalidOperationException("Có hóa đơn sau ngày trả phòng. Hãy kiểm tra và xử lý trước.");
        var invoice = new HoaDon { HopDongId = h.Id, LoaiHoaDon = "KY_CUOI", Nam = day.Year, Thang = day.Month,
            TuNgay = start, DenNgay = day, NgayChot = day, SoNguoiTinhPhi = 1, HanThanhToan = HomNay.AddDays(7),
            NguoiLapId = actor, NgayLap = clock.UtcNow, TrangThai = "NHAP" };
        if (tinh)
        {
            invoice.ChiTiet.AddRange(HoaDonDichVuService.TinhChiTietTienPhong(terms, start, day));
            foreach (var line in invoice.ChiTiet)
                line.ThanhTien = TienPhongTheoNgay(line.DonGia, line.SoNgayTinhTien ?? DateTime.DaysInMonth(day.Year, day.Month),
                    DateTime.DaysInMonth(day.Year, day.Month));
        }
        var baseline = await db.HopDongChiSoDauKys.AsNoTracking().SingleOrDefaultAsync(x => x.HopDongId == h.Id);
        var utilities = await db.DichVus.AsNoTracking().Where(x => x.MaDichVu == "DIEN" || x.MaDichVu == "NUOC").OrderBy(x => x.MaDichVu).ToListAsync();
        if (utilities.Count != 2) throw new InvalidOperationException("Chưa cấu hình đủ dịch vụ điện và nước.");
        var pricing = new DichVuPhongService(db, new DichVuService(db, time: clock), clock);
        foreach (var utility in utilities)
        {
            var price = await pricing.LayGiaHoaDonAsync(actor, p.Id, utility.Id, day);
            if (price is null || price.DonGia <= 0 || price.CachTinh != CachTinhDichVu.TheoChiSo)
                throw new InvalidOperationException($"{utility.TenDichVu}: cần dịch vụ theo chỉ số và đơn giá dương có hiệu lực tại ngày trả phòng.");
            var agreed = await db.HopDongDichVus.AnyAsync(x => x.HopDongId == h.Id);
            if (agreed && !await db.HopDongDichVus.AnyAsync(x => x.HopDongId == h.Id && x.DichVuId == utility.Id))
                throw new InvalidOperationException($"{utility.TenDichVu} không thuộc dịch vụ của hợp đồng.");
            var previous = await db.ChiSoDienNuocs.AsNoTracking().Where(x => x.HopDongId == h.Id && x.DichVuId == utility.Id)
                .OrderByDescending(x => x.DenNgay).FirstOrDefaultAsync();
            if (previous is null && baseline is null) throw new InvalidOperationException("Chưa có chỉ số bàn giao hoặc chỉ số đã chốt của hợp đồng.");
            var lastDay = previous?.DenNgay ?? baseline!.NgayBanGiao;
            var lastValue = previous?.ChiSoCuoi ?? (utility.MaDichVu == "DIEN" ? baseline!.ChiSoDien : baseline!.ChiSoNuoc);
            if (lastDay > day) throw new InvalidOperationException($"Ngày trả phòng trước lần chốt {utility.TenDichVu} gần nhất ({lastDay:dd/MM/yyyy}).");
            model.CongTo.Add(new(utility.MaDichVu, utility.TenDichVu, price.DonViTinh, lastValue, lastDay, price.CachTinh, price.DonGia, previous?.Id, previous?.PhienBan ?? 0));
            if (!tinh) continue;
            var final = utility.MaDichVu == "DIEN" ? input.ChiSoDien : input.ChiSoNuoc;
            KiemTraChiSo(final, lastValue, utility.TenDichVu);
            invoice.ChiTiet.Add(new() { SoThuTu = invoice.ChiTiet.Count + 1, DichVuId = utility.Id,
                CauHinhDichVuId = price.CauHinhId, TenKhoan = price.TenDichVu, CachTinhApDung = price.CachTinh,
                DonViTinh = price.DonViTinh, DonGia = price.DonGia, ChiSoDau = lastValue, ChiSoCuoi = final,
                SoLuong = final!.Value - lastValue, ThanhTien = HoaDonDichVuService.ThanhTien(final.Value - lastValue, price.DonGia),
                GhiChu = $"Chỉ số gần nhất ngày {lastDay:dd/MM/yyyy}; trả phòng ngày {day:dd/MM/yyyy}." });
        }
        if (tinh) { invoice.TongTien = HoaDonDichVuService.TongCong(invoice.ChiTiet); model.HoaDon = invoice; }
        return model;
    }

    public static void KiemTraChiSo(decimal? final, decimal previous, string name)
    {
        if (!final.HasValue || final < previous || final < 0 || final > 99999999999.999m || decimal.Round(final.Value, 3) != final)
            throw new InvalidOperationException($"{name}: chỉ số cuối bắt buộc, không nhỏ hơn lần chốt gần nhất, tối đa 3 số thập phân và 99.999.999.999,999.");
    }

    public static long TienPhongTheoNgay(long giaThang, int soNgay, int soNgayTrongThang)
    {
        if (giaThang < 0 || soNgay < 1 || soNgayTrongThang is < 28 or > 31 || soNgay > soNgayTrongThang)
            throw new InvalidOperationException("Số ngày hoặc giá tính tiền phòng không hợp lệ.");
        // Multiply first: a recurring decimal ratio can otherwise move an exact .5 below the rounding boundary.
        return checked((long)decimal.Round(checked((decimal)giaThang * soNgay) / soNgayTrongThang, 0, MidpointRounding.AwayFromZero));
    }

    public static string DauVanTay(TraPhongViewModel model) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
        model.Input.HopDongId, model.Input.NgayTra, model.CongTo, model.PhienBanPhong,
        model.HoaDon!.TuNgay, model.HoaDon.DenNgay, model.HoaDon.TongTien, model.HoaDon.ChiTiet
    }))));

    public async Task<int> LuuAsync(int actor, TraPhongInput input)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqlite);
        var model = await NhapAsync(actor, input, true);
        if (string.IsNullOrEmpty(input.DauVanTay) || input.DauVanTay != DauVanTay(model))
            throw new InvalidOperationException("Dữ liệu hợp đồng, chỉ số hoặc giá đã thay đổi. Hãy tính lại và kiểm tra hóa đơn.");
        var invoice = model.HoaDon!;
        invoice.MaHoaDon = "KC" + Guid.NewGuid().ToString("N")[..24];
        db.HoaDons.Add(invoice);
        await db.SaveChangesAsync();
        await sqlite.CommitAsync();
        return invoice.Id;
    }
}
