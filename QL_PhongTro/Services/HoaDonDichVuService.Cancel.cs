using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed partial class HoaDonDichVuService
{
    public async Task FillRelationsAsync(ChiTietHoaDonViewModel model, int? recipient = null)
    {
        var related = db.HoaDons.AsNoTracking().Where(x => x.HopDongId == model.HoaDon.HopDongId);
        if (recipient.HasValue) related = related.Where(x => x.TrangThai != "NHAP"
            && db.ThongBaoHoaDons.Any(n => n.HoaDonId == x.Id && n.NguoiNhanId == recipient.Value));
        if (model.HoaDon.ThayTheHoaDonId.HasValue) {
            var parent = await related.SingleOrDefaultAsync(x => x.Id == model.HoaDon.ThayTheHoaDonId);
            if (parent is not null) { model.HoaDonGocId = parent.Id; model.MaHoaDonGoc = parent.MaHoaDon; }
        }
        var replacement = await related.SingleOrDefaultAsync(x => x.ThayTheHoaDonId == model.HoaDon.Id);
        if (replacement is not null) {
            model.HoaDonThayTheId = replacement.Id; model.MaHoaDonThayThe = replacement.MaHoaDon;
            model.TrangThaiThayThe = replacement.TrangThai;
        }
    }

    public async Task<string?> LyDoChanHuyAsync(int id)
    {
        if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='thanh_ly_hop_dong'").SingleAsync()!=0
            && await db.BangTatToans.AnyAsync(x=>x.TrangThai=="DA_CHOT" && db.HoaDons.Any(h=>h.Id==id && h.HopDongId==x.HopDongId)))
            return "Hóa đơn thuộc bảng tất toán đã xác nhận, không được hủy.";
        // Optional future payment modules: refuse active/pending money instead of deleting or moving it.
        foreach (var (table, query) in new[] {
            ("thanh_toan", "SELECT COUNT(*) AS Value FROM thanh_toan WHERE hoa_don_id={0} AND (trang_thai NOT IN ('DA_HUY','TU_CHOI') OR trang_thai IS NULL)"),
            ("giao_dich_coc", "SELECT COUNT(*) AS Value FROM giao_dich_coc WHERE hoa_don_id={0} AND (trang_thai<>'DA_HUY' OR trang_thai IS NULL)") }) {
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name={table}").SingleAsync() == 0) continue;
            try {
                if (await db.Database.SqlQueryRaw<int>(query, id).SingleAsync() != 0)
                    return "Hóa đơn có khoản thanh toán hoặc cấn cọc đang hiệu lực/chờ xử lý. Hãy xử lý khoản tiền trước khi hủy.";
            } catch (DbException) { return "Schema thanh toán/cọc chưa tương thích. Cần kiểm tra trước khi hủy hóa đơn."; }
        }
        if (await db.ThongBaoHoaDons.AnyAsync(x => x.HoaDonId == id && x.TrangThaiEmail == "DANG_GUI"))
            return "Email hóa đơn đang được gửi. Hãy đợi xử lý xong rồi tải lại trang để hủy.";
        return null;
    }

    private async Task<HoaDon> OwnedInvoiceAsync(int actor, int id)
    {
        var building = await (from h in db.HoaDons.AsNoTracking() join c in db.HopDongs on h.HopDongId equals c.Id
            join p in db.PhongTros on c.PhongId equals p.Id where h.Id == id select (int?)p.ToaNhaId).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Không tìm thấy hóa đơn.");
        await KiemTraChuNhaAsync(actor, building);
        return await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == id);
    }

    public async Task HuyAsync(int actor, HuyHoaDonViewModel input)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite);
        var bill = await OwnedInvoiceAsync(actor, input.Id);
        if (bill.TrangThai != "DA_PHAT_HANH") throw new InvalidOperationException("Chỉ hủy hóa đơn đã phát hành. Hãy tải lại trang.");
        if (bill.PhienBan != input.PhienBan) throw new InvalidOperationException("Hóa đơn đã thay đổi. Hãy tải lại trước khi hủy.");
        var reason = input.LyDo?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new InvalidOperationException("Nhập lý do hủy, tối đa 1.000 ký tự.");
        if (!input.XacNhan) throw new InvalidOperationException("Hãy xác nhận trước khi hủy hóa đơn.");
        if (await LyDoChanHuyAsync(bill.Id) is string blocked) throw new InvalidOperationException(blocked);
        bill.TrangThai = "DA_HUY"; bill.LyDoHuy = reason; bill.NguoiHuyId = actor;
        bill.NgayHuy = (clock ?? new SystemTimeProvider()).UtcNow; bill.PhienBan++;
        // Keep notices/history; suppress undelivered email of the cancelled bill in the same transaction.
        await db.ThongBaoHoaDons.Where(x => x.HoaDonId == bill.Id && x.TrangThaiEmail != "DA_GUI")
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.TrangThaiEmail, "DA_HUY").SetProperty(x => x.KhoaXuLyDen, (DateTime?)null));
        await db.SaveChangesAsync(); await sqlite.CommitAsync();
    }

    public async Task<int> TaoNhapThayTheAsync(int actor, int id, int version)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite);
        var old = await OwnedInvoiceAsync(actor, id);
        if(await db.HopDongs.AnyAsync(x=>x.Id==old.HopDongId && x.TrangThai=="DA_KET_THUC"))throw new InvalidOperationException("Hợp đồng đã kết thúc, không được tạo hóa đơn thay thế.");
        if(old.LoaiHoaDon=="KY_CUOI")throw new InvalidOperationException("Cần tính lại kỳ cuối từ thông tin bàn giao; không sao chép hóa đơn tháng.");
        if (old.TrangThai != "DA_HUY" || old.PhienBan != version) throw new InvalidOperationException("Hóa đơn chưa hủy hoặc dữ liệu đã thay đổi. Hãy tải lại trang.");
        var existing = await db.HoaDons.Where(x => x.ThayTheHoaDonId == id).Select(x => (int?)x.Id).SingleOrDefaultAsync();
        if (existing.HasValue) return existing.Value; // Same request is safe to retry.
        if (await db.HoaDons.AnyAsync(x => x.HopDongId == old.HopDongId && x.Nam == old.Nam && x.Thang == old.Thang && x.TrangThai != "DA_HUY"))
            throw new InvalidOperationException("Kỳ này đã có hóa đơn khác chưa hủy. Hãy mở bản hiện có.");
        await ValidateRepairSnapshotsAsync(old);
        var bill = new HoaDon { MaHoaDon = $"HD-{old.Nam}{old.Thang:00}-{Guid.NewGuid().ToString("N")[..12]}",
            HopDongId = old.HopDongId, Nam = old.Nam, Thang = old.Thang, TuNgay = old.TuNgay, DenNgay = old.DenNgay,
            NgayChot = old.NgayChot, SoNguoiTinhPhi = old.SoNguoiTinhPhi, LoaiHoaDon = old.LoaiHoaDon,
            NgayLap = (clock ?? new SystemTimeProvider()).UtcNow, HanThanhToan = old.HanThanhToan, TongTien = old.TongTien,
            TrangThai = "NHAP", NguoiLapId = actor, ThayTheHoaDonId = old.Id, GhiChu = old.GhiChu,
            ChiTiet = old.ChiTiet.OrderBy(x => x.SoThuTu).Select(x => new ChiTietHoaDon {
                SoThuTu=x.SoThuTu,DichVuId=x.DichVuId,CauHinhDichVuId=x.CauHinhDichVuId,KyHopDongId=x.KyHopDongId,
                ChiSoId=x.ChiSoId,BaoHongId=x.BaoHongId,SoNgayTinhTien=x.SoNgayTinhTien,SoNgayTrongThang=x.SoNgayTrongThang,
                LoaiKhoan=x.LoaiKhoan,TenKhoan=x.TenKhoan,CachTinhApDung=x.CachTinhApDung,DonViTinh=x.DonViTinh,
                SoLuong=x.SoLuong,DonGia=x.DonGia,ChiSoDau=x.ChiSoDau,ChiSoCuoi=x.ChiSoCuoi,ThanhTien=x.ThanhTien,GhiChu=x.GhiChu
            }).ToList() };
        db.HoaDons.Add(bill); await db.SaveChangesAsync(); await sqlite.CommitAsync(); return bill.Id;
    }
}
