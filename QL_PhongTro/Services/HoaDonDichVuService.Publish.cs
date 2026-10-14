using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed partial class HoaDonDichVuService
{
    public async Task PhatHanhNhapAsync(int actor, PhatHanhNhapViewModel input)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite);
        await PhatHanhNhapCoreAsync(actor, input);
        await sqlite.CommitAsync();
    }

    // A whole batch shares one transaction, including meter locks, audit and notifications.
    public async Task<int> PhatHanhNhieuNhapAsync(int actor, int building, int year, int month,
        IReadOnlyDictionary<int, int> versions, DateOnly issue, DateOnly due, bool confirmed, ICollection<int>? publishedIds = null)
    {
        if (!confirmed || versions.Count is < 1 or > 50)
            throw new InvalidOperationException("Hãy xác nhận từ 1 đến 50 hóa đơn Nháp mỗi lần.");
        NgayHoaDon(issue, due);
        await db.Database.OpenConnectionAsync();
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite);
        await KiemTraChuNhaAsync(actor, building);
        var ids = versions.Keys.ToList();
        var bills = await (from bill in db.HoaDons join contract in db.HopDongs on bill.HopDongId equals contract.Id
            join room in db.PhongTros on contract.PhongId equals room.Id
            where ids.Contains(bill.Id) && room.ToaNhaId == building && bill.Nam == year && bill.Thang == month
            select new { bill.Id, bill.TrangThai }).ToListAsync();
        if (bills.Count != ids.Count) throw new UnauthorizedAccessException();
        var count = 0;
        var completed = new List<int>();
        foreach (var bill in bills.OrderBy(x => x.Id))
        {
            // Repeated submissions do not create another notification or change published amounts.
            if (bill.TrangThai == "DA_PHAT_HANH") continue;
            await PhatHanhNhapCoreAsync(actor, new() { Id = bill.Id, PhienBan = versions[bill.Id],
                XacNhan = true, NgayPhatHanh = issue, HanThanhToan = due });
            count++;
            completed.Add(bill.Id);
        }
        await sqlite.CommitAsync();
        if (publishedIds is not null) foreach(var id in completed) publishedIds.Add(id);
        return count;
    }

    private async Task PhatHanhNhapCoreAsync(int actor, PhatHanhNhapViewModel input)
    {
        var context = await (from invoice in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs on invoice.HopDongId equals contract.Id
            join room in db.PhongTros on contract.PhongId equals room.Id
            where invoice.Id == input.Id select new { room.Id, room.ToaNhaId, room.MaPhong, contract.KhachDungTenId }).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Không tìm thấy hóa đơn.");
        await KiemTraChuNhaAsync(actor, context.ToaNhaId);
        var bill = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == input.Id);
        if (bill.TrangThai != "NHAP") throw new InvalidOperationException("Hóa đơn đã được phát hành hoặc không còn là Nháp. Hãy tải lại trang.");
        if (bill.PhienBan != input.PhienBan) throw new InvalidOperationException("Hóa đơn đã thay đổi sau khi kiểm tra. Hãy tải lại và xác nhận số tiền mới.");
        await ValidateRepairSnapshotsAsync(bill);
        if (!input.XacNhan) throw new InvalidOperationException("Hãy xác nhận nội dung và tổng tiền trước khi phát hành.");
        var dates = NgayHoaDon(input.NgayPhatHanh, input.HanThanhToan);
        if (bill.ChiTiet.Count == 0 || bill.ChiTiet.Any(x => x.DonGia < 0 || x.SoLuong < 0 || string.IsNullOrWhiteSpace(x.TenKhoan)))
            throw new InvalidOperationException("Chi tiết hóa đơn chưa hợp lệ.");
        foreach (var line in bill.ChiTiet) {
            if (line.LoaiKhoan is not ("TIEN_PHONG" or "DICH_VU" or "PHAT_SINH" or "GIAM_TRU"))
                throw new InvalidOperationException("Loại khoản trên hóa đơn không hợp lệ.");
            if (line.LoaiKhoan is "PHAT_SINH" or "GIAM_TRU" && (line.ThanhTien <= 0 || string.IsNullOrWhiteSpace(line.GhiChu)))
                throw new InvalidOperationException("Khoản phát sinh/giảm trừ phải có số tiền dương và ghi chú.");
            if (line.CachTinhApDung == CachTinhDichVu.TheoChiSo && (!line.ChiSoDau.HasValue || !line.ChiSoCuoi.HasValue
                || line.ChiSoDau < 0 || line.ChiSoCuoi < line.ChiSoDau || line.ChiSoCuoi > 99999999999.999m
                || decimal.Round(line.ChiSoDau.Value, 3) != line.ChiSoDau || decimal.Round(line.ChiSoCuoi.Value, 3) != line.ChiSoCuoi
                || line.SoLuong != line.ChiSoCuoi.Value - line.ChiSoDau.Value))
                throw new InvalidOperationException("Chỉ số trên hóa đơn không hợp lệ.");
            if (ThanhTien(line.SoLuong, line.DonGia) != line.ThanhTien)
                throw new InvalidOperationException("Thành tiền không khớp chi tiết. Hãy lưu và kiểm tra lại bản nháp.");
        }
        if (TongCong(bill.ChiTiet) < 0 || TongCong(bill.ChiTiet) != bill.TongTien)
            throw new InvalidOperationException("Tổng tiền không khớp các khoản trên hóa đơn.");
        var recipient = await (from tenant in db.KhachThues join account in db.TaiKhoans on tenant.TaiKhoanId equals account.Id
            where tenant.Id == context.KhachDungTenId && account.VaiTro == "KHACH_THUE" && account.DangHoatDong && !account.IsDeleted
            select new { account.Id, account.Email }).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Khách đứng tên chưa có tài khoản khách thuê hoạt động để nhận thông báo.");
        if (string.IsNullOrWhiteSpace(recipient.Email)) throw new InvalidOperationException("Khách đứng tên chưa có email nhận thông báo.");
        var now = (clock ?? new SystemTimeProvider()).UtcNow;
        // Lock original readings without rewriting them to match edited invoice snapshots.
        var serviceIds = bill.ChiTiet.Where(x => x.CachTinhApDung == CachTinhDichVu.TheoChiSo && x.DichVuId.HasValue).Select(x => x.DichVuId!.Value).ToList();
        var readings = await db.ChiSoDienNuocs.Where(x => x.HopDongId == bill.HopDongId && serviceIds.Contains(x.DichVuId)
            && x.TuNgay == bill.TuNgay && x.DenNgay == bill.DenNgay).ToListAsync();
        var replacement = bill.ThayTheHoaDonId.HasValue && await db.HoaDons.AnyAsync(x => x.Id == bill.ThayTheHoaDonId
            && x.TrangThai == "DA_HUY" && x.HopDongId == bill.HopDongId && x.Nam == bill.Nam && x.Thang == bill.Thang
            && x.TuNgay == bill.TuNgay && x.DenNgay == bill.DenNgay);
        if (bill.ThayTheHoaDonId.HasValue && !replacement) throw new InvalidOperationException("Liên kết hóa đơn thay thế chưa hợp lệ.");
        if (readings.Any(x => x.DaKhoa) && !replacement) throw new InvalidOperationException("Bản chỉ số gốc đã được khóa; hãy kiểm tra lại trước khi phát hành.");
        foreach (var reading in readings.Where(x => !x.DaKhoa)) { reading.DaKhoa = true; reading.PhienBan++; }
        bill.TrangThai = "DA_PHAT_HANH"; bill.NgayPhatHanh = now; bill.NgayPhatHanhNghiepVu = dates.Issue;
        bill.HanThanhToan = dates.Due; bill.NguoiPhatHanhId = actor; bill.PhienBan++;
        db.ThongBaoHoaDons.Add(new() { NguoiNhanId = recipient.Id, HoaDonId = bill.Id, NgayTao = now, EmailNhan = recipient.Email,
            TieuDe = $"Hóa đơn phòng {context.MaPhong} · {bill.Thang:00}/{bill.Nam}", DuongDan = $"/ThongBao/HoaDon/{bill.Id}",
            NoiDung = $"Hóa đơn {bill.MaHoaDon} đã được phát hành. Tổng tiền: {bill.TongTien.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))} đ. Hạn thanh toán: {dates.Due:dd/MM/yyyy}." });
        // Invoice, source meter locks and durable notification are committed together. No email is sent inside this transaction.
        await db.SaveChangesAsync();
    }
}
