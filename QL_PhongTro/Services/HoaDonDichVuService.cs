using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed partial class HoaDonDichVuService(AppDbContext db, DichVuService services, ITimeProvider? clock = null)
{
    public async Task<bool> SanSangAsync()
    {
        var connection = db.Database.GetDbConnection();
        var close = connection.State != System.Data.ConnectionState.Open;
        if (close) await connection.OpenAsync();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','chi_tiet_hoa_don','hop_dong','ky_hop_dong','hop_dong_dich_vu')";
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 5 && await services.SanSangAsync();
        }
        finally { if (close) await connection.CloseAsync(); }
    }

    public static long ThanhTien(decimal quantity, long price) => checked((long)decimal.Round(checked(quantity * price), 0, MidpointRounding.AwayFromZero));

    public sealed record SoNguoiHoaDon(int SoNguoi, DateOnly NgayChot, int PhienBanPhong);

    public async Task<SoNguoiHoaDon> LaySoNguoiAsync(int accountId, int contractId, int buildingId, DateOnly period)
    {
        if (!await services.SoHuuToaNhaAsync(accountId, buildingId)) throw new UnauthorizedAccessException();
        if (period.Year is < 1900 or > 9998) throw new InvalidOperationException("Kỳ hóa đơn ngoài phạm vi hỗ trợ.");
        var context = await (from h in db.HopDongs.AsNoTracking() join p in db.PhongTros on h.PhongId equals p.Id
            where h.Id == contractId && p.ToaNhaId == buildingId && h.TrangThai == "DANG_HIEU_LUC"
            select new { h.KhachDungTenId, h.NgayChot, p.SoNguoiToiDa, p.PhienBan }).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Hợp đồng không hợp lệ hoặc không thuộc tòa nhà.");
        if (context.KhachDungTenId == null || !await db.KhachThues.AnyAsync(k => k.Id == context.KhachDungTenId))
            throw new InvalidOperationException("Hợp đồng phải có một người đứng tên để tính phí.");
        if (context.NgayChot is < 1 or > 31) throw new InvalidOperationException("Ngày chốt hợp đồng không hợp lệ.");
        var first = new DateOnly(period.Year, period.Month, 1);
        var cutoff = new DateOnly(period.Year, period.Month, Math.Min(context.NgayChot, DateTime.DaysInMonth(period.Year, period.Month)));
        // New arrivals start being billed on the first of the following month, for a full month.
        // Departure month is billed in full; the following month removes the person.
        var count = 1 + await db.NguoiOGheps.AsNoTracking().Where(g => g.HopDongId == contractId
            && g.KhachThueId != context.KhachDungTenId && g.NgayVao < first
            && (g.NgayRa == null || g.NgayRa >= first)).Select(g => g.KhachThueId).Distinct().CountAsync();
        if (count > context.SoNguoiToiDa) throw new InvalidOperationException($"Số người tính phí vượt sức chứa {context.SoNguoiToiDa} người của phòng.");
        return new(count, cutoff, context.PhienBan);
    }

    public async Task<int> PhatHanhAsync(int accountId, LapHoaDonDichVuViewModel input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), validateAllProperties: true);
        if (input.Dong.Count > 100)
            throw new InvalidOperationException("Chọn tối đa 100 dịch vụ.");
        var chosen = input.Dong.Where(x => x.Chon).ToList();
        if (chosen.Select(x => x.DichVuId).Distinct().Count() != chosen.Count)
            throw new InvalidOperationException("Một dịch vụ chỉ được chọn một lần.");
        await db.Database.OpenConnectionAsync();
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqlite);
        if (!await services.SoHuuToaNhaAsync(accountId, input.ToaNhaId)) throw new UnauthorizedAccessException();
        var occupancy = await LaySoNguoiAsync(accountId, input.HopDongId!.Value, input.ToaNhaId, input.NgayApDung!.Value);
        if (input.PhienBanPhong.HasValue && input.PhienBanPhong != occupancy.PhienBanPhong)
            throw new InvalidOperationException("Danh sách người ở hoặc thông tin phòng đã thay đổi. Hãy tải lại bản xem trước trước khi phát hành.");
        var date = occupancy.NgayChot;
        var contractServices = await db.HopDongDichVus.Where(x => x.HopDongId == input.HopDongId).Select(x => x.DichVuId).ToListAsync();
        // Legacy contracts have no service snapshots; retain their assigned-room service list.
        if (contractServices.Count > 0 && chosen.Any(x => !contractServices.Contains(x.DichVuId)))
            throw new InvalidOperationException("Dịch vụ không nằm trong danh sách dịch vụ của hợp đồng.");
        if (date.Year < 1900 || date.Year > 9998) throw new InvalidOperationException("Ngày áp dụng ngoài phạm vi hỗ trợ.");
        var from = new DateOnly(date.Year, date.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var contract = await (from h in db.HopDongs
                              join p in db.PhongTros on h.PhongId equals p.Id
                              where h.Id == input.HopDongId && p.ToaNhaId == input.ToaNhaId && h.TrangThai == "DANG_HIEU_LUC"
                              select new { HopDong = h, Phong = p }).SingleOrDefaultAsync();
        if (contract is null) throw new InvalidOperationException("Hợp đồng không hợp lệ hoặc không thuộc tòa nhà.");
        if (contract.HopDong.NgayTraPhong is { } end && end < to)
            throw new InvalidOperationException("Hợp đồng trả phòng trong kỳ cần luồng hóa đơn kỳ cuối.");
        var periods = await db.KyHopDongs.Where(x => x.HopDongId == contract.HopDong.Id && x.NgayBatDau <= from && x.NgayKetThuc >= to).Take(2).ToListAsync();
        if (periods.Count != 1) throw new InvalidOperationException("Luồng này chỉ hỗ trợ kỳ thuê trọn tháng với một mức giá phòng. Kỳ lẻ hoặc gia hạn giữa tháng cần xử lý riêng.");
        if (await db.HoaDons.AnyAsync(x => x.HopDongId == contract.HopDong.Id && x.Nam == date.Year && x.Thang == date.Month && x.TrangThai != "DA_HUY"))
            throw new InvalidOperationException("Hợp đồng đã có hóa đơn trong tháng này.");
        var invoice = new HoaDon
        {
            MaHoaDon = "HD" + Guid.NewGuid().ToString("N")[..24],
            HopDongId = contract.HopDong.Id,
            Thang = date.Month,
            Nam = date.Year,
            TuNgay = from,
            DenNgay = to,
            NgayChot = date,
            SoNguoiTinhPhi = occupancy.SoNguoi,
            NgayLap = (clock ?? new SystemTimeProvider()).UtcNow,
            HanThanhToan = DateOnly.FromDateTime((clock ?? new SystemTimeProvider()).UtcNow.AddHours(7)).AddDays(7),
            NguoiLapId = accountId
        };
        invoice.ChiTiet.Add(new ChiTietHoaDon
        {
            SoThuTu = 1,
            LoaiKhoan = "TIEN_PHONG",
            TenKhoan = "Tiền phòng",
            DonViTinh = "tháng",
            KyHopDongId = periods[0].Id,
            SoLuong = 1,
            DonGia = periods[0].GiaThue,
            ThanhTien = periods[0].GiaThue
        });
        foreach (var item in chosen)
        {
            var price = await new DichVuPhongService(db, services).LayGiaHoaDonAsync(accountId, contract.Phong.Id, item.DichVuId, date)
                ?? throw new InvalidOperationException("Dịch vụ đã ngừng áp dụng cho phòng trong kỳ này hoặc chưa có giá. Hãy tải lại danh sách theo hợp đồng và kỳ hóa đơn.");
            if (price.CauHinhId != item.CauHinhId || price.DonGia != item.DonGiaDaXem)
                throw new InvalidOperationException("Đơn giá đã thay đổi từ lúc mở form. Hãy tải lại bảng giá trước khi phát hành.");
            decimal quantity;
            if (price.CachTinh == CachTinhDichVu.TheoChiSo)
            {
                if (!item.ChiSoDau.HasValue || !item.ChiSoCuoi.HasValue || item.ChiSoDau < 0 || item.ChiSoCuoi < item.ChiSoDau ||
                    item.ChiSoCuoi > 99999999999.999m || decimal.Round(item.ChiSoDau.Value, 3) != item.ChiSoDau || decimal.Round(item.ChiSoCuoi.Value, 3) != item.ChiSoCuoi)
                    throw new InvalidOperationException("Chỉ số phải không âm, tối đa 3 chữ số thập phân, số cuối không nhỏ hơn số đầu.");
                quantity = item.ChiSoCuoi.Value - item.ChiSoDau.Value;
            }
            else quantity = price.CachTinh == CachTinhDichVu.TheoNguoi ? occupancy.SoNguoi : 1;
            invoice.ChiTiet.Add(new ChiTietHoaDon
            {
                SoThuTu = invoice.ChiTiet.Count + 1,
                DichVuId = price.DichVuId,
                CauHinhDichVuId = price.CauHinhId,
                TenKhoan = price.TenDichVu,
                CachTinhApDung = price.CachTinh,
                DonViTinh = price.DonViTinh,
                DonGia = price.DonGia,
                SoLuong = quantity,
                ChiSoDau = price.CachTinh == CachTinhDichVu.TheoChiSo ? item.ChiSoDau : null,
                ChiSoCuoi = price.CachTinh == CachTinhDichVu.TheoChiSo ? item.ChiSoCuoi : null,
                ThanhTien = ThanhTien(quantity, price.DonGia)
            });
        }
        invoice.TongTien = invoice.ChiTiet.Aggregate(0L, (sum, line) => checked(sum + line.ThanhTien));
        db.HoaDons.Add(invoice);
        await db.SaveChangesAsync(); // Insert lines while NHAP; triggers forbid line changes after publication.
        invoice.TrangThai = "DA_PHAT_HANH"; invoice.NgayPhatHanh = (clock ?? new SystemTimeProvider()).UtcNow; invoice.NguoiPhatHanhId = accountId;
        await db.SaveChangesAsync(); await sqlite.CommitAsync();
            // Create email log for notification
            var recipient = await (from kt in db.KhachThues
                               join tk in db.TaiKhoans on kt.TaiKhoanId equals tk.Id
                               where kt.Id == contract.HopDong.KhachDungTenId
                               && tk.DangHoatDong && tk.EmailConfirmed
                               select new { kt.Id, tk.Email, tk.HoTen })
                               .FirstOrDefaultAsync();
            if (recipient != null && !string.IsNullOrWhiteSpace(recipient.Email))
            {
                var emailLog = new EmailGuiLog
                {
                    HoaDonId = invoice.Id,
                    LoaiThongBao = LoaiThongBaoHoaDon.HoaDonMoi,
                    NguoiNhanId = recipient.Id,
                    EmailNguoiNhan = recipient.Email,
                    TrangThai = TrangThaiEmail.ChoGui,
                    SoLanThu = 0,
                    NgayGui = (clock ?? new SystemTimeProvider()).UtcNow
                };
                db.EmailGuiLogs.Add(emailLog);
            }
        return invoice.Id;
    }

    // Integration point for the contract module: freezes agreed information and creates a reliable reference.
    public async Task GanVaoHopDongAsync(int accountId, int contractId, int serviceId, DateOnly date)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var context = await (from h in db.HopDongs
                             join p in db.PhongTros on h.PhongId equals p.Id
                             where h.Id == contractId
                             select new { h, p.ToaNhaId }).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");
        if (context.h.TrangThai is "DA_KET_THUC" or "DA_HUY") throw new InvalidOperationException("Hợp đồng đã kết thúc hoặc hủy.");
        var price = await services.LayDonGiaAsync(accountId, context.ToaNhaId, serviceId, date)
            ?? throw new InvalidOperationException("Dịch vụ chưa có giá hoặc đã ngừng áp dụng.");
        if (await db.HopDongDichVus.AnyAsync(x => x.HopDongId == contractId && x.DichVuId == serviceId)) return;
        db.HopDongDichVus.Add(new HopDongDichVu
        {
            HopDongId = contractId,
            DichVuId = serviceId,
            CauHinhDichVuId = price.CauHinhId,
            TenDichVu = price.TenDichVu,
            CachTinh = price.CachTinh,
            DonViTinh = price.DonViTinh,
            DonGia = price.DonGia,
            NgayGhiNhan = DateTime.UtcNow
        });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }
}
