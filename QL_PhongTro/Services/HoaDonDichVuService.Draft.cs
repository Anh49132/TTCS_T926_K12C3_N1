using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed partial class HoaDonDichVuService
{
    public static long TongCong(IEnumerable<ChiTietHoaDon> lines) => lines.Aggregate(0L,
        (total, line) => checked(total + (line.LoaiKhoan == "GIAM_TRU" ? -line.ThanhTien : line.ThanhTien)));

    public async Task<int> TaoNhapThangAsync(int actor, int building, int contract, int year, int month, DateOnly? issueDate = null, DateOnly? dueDate = null)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite);
        await KiemTraChuNhaAsync(actor, building);
        if (!await (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id
            where h.Id == contract && p.ToaNhaId == building select h.Id).AnyAsync()) throw new UnauthorizedAccessException();
        var preview = await XemThangAsync(actor, building, year, month, issueDate, dueDate);
        var row = preview.DuKien.SingleOrDefault(x => x.HoaDon.HopDongId == contract);
        if (row is null)
        {
            var reason = preview.BoQua.SingleOrDefault(x => x.HopDongId == contract)?.LyDo;
            throw new InvalidOperationException(reason ?? "Hợp đồng chưa đủ dữ liệu hoặc đã có hóa đơn kỳ này. Hãy kiểm tra lại.");
        }
        row.HoaDon.NgayPhatHanhNghiepVu = null;
        db.HoaDons.Add(row.HoaDon);
        await db.SaveChangesAsync();
        await sqlite.CommitAsync();
        return row.HoaDon.Id;
    }

    public async Task LuuNhapAsync(int actor, SuaHoaDonNhapViewModel input)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite);
        var building = await (from existing in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs on existing.HopDongId equals contract.Id
            join room in db.PhongTros on contract.PhongId equals room.Id
            where existing.Id == input.Id select (int?)room.ToaNhaId).SingleOrDefaultAsync();
        if (!building.HasValue || !await services.SoHuuToaNhaAsync(actor, building.Value)) throw new UnauthorizedAccessException();
        await KiemTraChuNhaAsync(actor, building.Value);
        var invoice = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == input.Id);
        if (invoice.TrangThai != "NHAP") throw new InvalidOperationException("Chỉ được sửa hóa đơn Nháp.");
        if (invoice.PhienBan != input.PhienBan) throw new InvalidOperationException("Hóa đơn đã được sửa bởi người khác. Hãy tải lại trang.");
        var beforeHistory = SnapshotNhap(invoice);
        var meters = invoice.ChiTiet.Where(x => x.CachTinhApDung == CachTinhDichVu.TheoChiSo).ToDictionary(x => x.Id);
        if (input.ChiSo.Count != meters.Count || input.ChiSo.Select(x => x.Id).Distinct().Count() != meters.Count
            || input.ChiSo.Any(x => !meters.ContainsKey(x.Id))) throw new InvalidOperationException("Danh sách chỉ số không hợp lệ.");
        if (input.Khoan.Count > 100) throw new InvalidOperationException("Tối đa 100 khoản điều chỉnh mỗi lần lưu.");
        foreach (var reading in input.ChiSo)
        {
            if (reading.ChiSoDau < 0 || reading.ChiSoCuoi < reading.ChiSoDau || reading.ChiSoCuoi > 99999999999.999m
                || decimal.Round(reading.ChiSoDau, 3) != reading.ChiSoDau || decimal.Round(reading.ChiSoCuoi, 3) != reading.ChiSoCuoi)
                throw new InvalidOperationException("Chỉ số không âm, tối đa 3 số thập phân; số cuối phải lớn hơn hoặc bằng số đầu.");
        }
        var additions = input.Khoan.Where(x => !string.IsNullOrWhiteSpace(x.TenKhoan) || x.SoTien.HasValue || !string.IsNullOrWhiteSpace(x.GhiChu)).ToList();
        foreach (var item in additions)
            if (item.LoaiKhoan is not ("PHAT_SINH" or "GIAM_TRU") || string.IsNullOrWhiteSpace(item.TenKhoan)
                || item.TenKhoan.Trim().Length > 200 || !item.SoTien.HasValue || item.SoTien <= 0 || string.IsNullOrWhiteSpace(item.GhiChu) || item.GhiChu.Trim().Length > 1000)
                throw new InvalidOperationException("Khoản phát sinh/giảm trừ phải có tên (tối đa 200 ký tự), số tiền dương và ghi chú (tối đa 1000 ký tự).");
        foreach (var reading in input.ChiSo)
        {
            var line = meters[reading.Id];
            line.ChiSoDau = reading.ChiSoDau; line.ChiSoCuoi = reading.ChiSoCuoi;
            line.SoLuong = reading.ChiSoCuoi - reading.ChiSoDau;
            line.ThanhTien = ThanhTien(line.SoLuong, line.DonGia);
        }
        var adjustments = invoice.ChiTiet.Where(x => x.LoaiKhoan is "PHAT_SINH" or "GIAM_TRU").ToDictionary(x => x.Id);
        var editIds = additions.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToList();
        if (editIds.Distinct().Count() != editIds.Count || editIds.Any(x => !adjustments.ContainsKey(x))
            || input.XoaKhoanIds.Any(x => !adjustments.ContainsKey(x)) || editIds.Intersect(input.XoaKhoanIds).Any())
            throw new InvalidOperationException("Danh sách khoản điều chỉnh không hợp lệ.");
        foreach (var id in input.XoaKhoanIds.Distinct()) { db.ChiTietHoaDons.Remove(adjustments[id]); invoice.ChiTiet.Remove(adjustments[id]); }
        foreach (var item in additions.Where(x => x.Id.HasValue)) {
            var line = adjustments[item.Id!.Value];
            line.LoaiKhoan=item.LoaiKhoan; line.TenKhoan=item.TenKhoan!.Trim(); line.GhiChu=item.GhiChu!.Trim();
            line.SoLuong=1;line.DonGia=item.SoTien!.Value;line.ThanhTien=item.SoTien.Value;
        }
        if(input.HanThanhToan.HasValue) {
            if(input.HanThanhToan.Value.Year < 1900 || input.HanThanhToan.Value.Year > 9998)
                throw new InvalidOperationException("Hạn thanh toán không hợp lệ.");
            invoice.HanThanhToan=input.HanThanhToan.Value;
        }
        var order = invoice.ChiTiet.Select(x=>x.SoThuTu).DefaultIfEmpty().Max();
        foreach (var item in additions.Where(x=>!x.Id.HasValue)) invoice.ChiTiet.Add(new ChiTietHoaDon {
            SoThuTu = ++order, LoaiKhoan = item.LoaiKhoan, TenKhoan = item.TenKhoan!.Trim(),
            GhiChu = item.GhiChu!.Trim(), SoLuong = 1, DonGia = item.SoTien!.Value, ThanhTien = item.SoTien.Value, DonViTinh = "khoản" });
        invoice.TongTien = TongCong(invoice.ChiTiet);
        if (invoice.TongTien < 0) throw new InvalidOperationException("Tổng giảm trừ không được vượt tổng các khoản thu.");
        if (beforeHistory == SnapshotNhap(invoice)) return;
        invoice.PhienBan++;
        await db.SaveChangesAsync();
        await GhiLichSuNhapAsync(actor, invoice, beforeHistory);
        await sqlite.CommitAsync();
    }
}
