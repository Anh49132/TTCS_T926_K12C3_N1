using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed partial class GhiThuHoaDonService(AppDbContext db, ITimeProvider clock)
{
    public DateOnly HomNay => DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
    public static string TenHinhThuc(string? code) => code switch {
        "TIEN_MAT" => "Tiền mặt", "CHUYEN_KHOAN" => "Chuyển khoản", "KHAC" => "Khác", _ => "Chưa có thông tin" };

    public async Task<bool> SanSangAsync(CancellationToken ct = default) =>
        await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='lan_thu_hoa_don'").SingleAsync(ct) == 1;

    private async Task<HoaDon> OwnedAsync(int actor, int invoiceId, CancellationToken ct)
    {
        var owned = await (from invoice in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs on invoice.HopDongId equals contract.Id
            join room in db.PhongTros on contract.PhongId equals room.Id
            join building in db.ToaNhas on room.ToaNhaId equals building.Id
            join account in db.TaiKhoans on actor equals account.Id
            where invoice.Id == invoiceId && account.DangHoatDong && !account.IsDeleted
                  && (building.ChuNhaId == actor && account.VaiTro == "CHU_NHA" || account.VaiTro == "ADMIN")
            select invoice).SingleOrDefaultAsync(ct);
        return owned ?? throw new UnauthorizedAccessException();
    }

    public async Task<ThanhToanHoaDonSummary> ChiTietAsync(int actor, int invoiceId, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null) return await SummaryAsync(await OwnedAsync(actor, invoiceId, ct), ct);
        // One read snapshot for receipt states and cancellation history, even if another request cancels during a GET.
        await db.Database.OpenConnectionAsync(ct);
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: true);
        await using var tx = await db.Database.UseTransactionAsync(sqlite, ct);
        var invoice = await OwnedAsync(actor, invoiceId, ct);
        var summary = await SummaryAsync(invoice, ct);
        await tx!.CommitAsync(ct);
        return summary;
    }

    private async Task<ThanhToanHoaDonSummary> SummaryAsync(HoaDon invoice, CancellationToken ct)
    {
        var payments = await db.ThanhToans.AsNoTracking().Where(x => x.HoaDonId == invoice.Id && (x.TrangThai == "DA_XAC_NHAN" || x.TrangThai == "DA_HUY"))
            .OrderBy(x => x.NgayThanhToan).ThenBy(x => x.Id).ToListAsync(ct);
        var ready = await SanSangAsync(ct);
        var ids = payments.Select(x => x.Id).ToList();
        var metadata = ready ? await db.LanThuHoaDons.AsNoTracking().Where(x => ids.Contains(x.ThanhToanId)).ToListAsync(ct) : [];
        var cancelReady = await HuySanSangAsync(ct);
        var cancellations = cancelReady ? await db.HuyLanThuHoaDons.AsNoTracking().Where(x => ids.Contains(x.ThanhToanId)).ToListAsync(ct) : [];
        var paid = payments.Where(x => x.TrangThai == "DA_XAC_NHAN").Aggregate(0L, (sum, x) => checked(sum + x.SoTien));
        var remaining = checked(invoice.TongTien - paid);
        // The optional deposit ledger is owned by S4-04. Never count an offset as cash collected.
        if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='giao_dich_coc'").SingleAsync(ct) != 0)
            remaining = checked(remaining - await db.Database.SqlQuery<long>($"SELECT CAST(COALESCE(SUM(so_tien),0) AS BIGINT) AS Value FROM giao_dich_coc WHERE hoa_don_id={invoice.Id} AND loai_giao_dich='CAN_TRU' AND trang_thai='DA_XAC_NHAN'").SingleAsync(ct));
        var status = invoice.TrangThai == "DA_HUY" ? "DA_HUY" : invoice.TrangThai != "DA_PHAT_HANH" ? "NHAP" :
            remaining <= 0 ? "DA_THANH_TOAN" : paid > 0 || remaining < invoice.TongTien ? "THANH_TOAN_MOT_PHAN" : "CHUA_THANH_TOAN";
        var name = status switch { "DA_HUY" => "Đã hủy", "NHAP" => "Chưa phát hành", "DA_THANH_TOAN" => "Đã thanh toán",
            "THANH_TOAN_MOT_PHAN" => "Trả một phần", _ => "Chưa thanh toán" };
        return new(invoice.TongTien, paid, remaining, status, name, ready && invoice.TrangThai == "DA_PHAT_HANH" && remaining > 0, ready,
            payments.Select(p => { var m = metadata.SingleOrDefault(x => x.ThanhToanId == p.Id);
                var canceled = cancellations.SingleOrDefault(x => x.ThanhToanId == p.Id);
                return new LanThuHoaDonItem(p.Id, p.SoTien, p.NgayThanhToan, m?.HinhThuc ?? "", m?.GhiChu,
                    m?.TenNguoiXacNhan ?? "Chưa có thông tin (dữ liệu cũ)", m?.ThoiDiemGhi, p.TrangThai == "DA_HUY",
                    canceled?.LyDoHuy, canceled?.TenNguoiHuy, canceled?.ThoiDiemHuy,
                    canceled?.PhienBan ?? (p.TrangThai == "DA_HUY" ? 1 : 0), cancelReady && p.TrangThai == "DA_XAC_NHAN"); }).ToList(), cancelReady);
    }

    public async Task<ThanhToanHoaDonSummary> GhiAsync(int actorId, GhiThuHoaDonInput input, CancellationToken ct = default)
    {
        if (!await SanSangAsync(ct)) throw new InvalidOperationException("Chưa cài chức năng ghi thu. Cần nâng cấp CSDL bằng bước v26 trên bản sao trước.");
        await db.Database.OpenConnectionAsync(ct);
        // IMMEDIATE reserves the writer BEFORE reading balance, across requests/processes.
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite, ct);
        var invoice = await OwnedAsync(actorId, input.HoaDonId, ct);
        var actor = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.DangHoatDong && !x.IsDeleted && x.VaiTro == "CHU_NHA", ct)
            ?? throw new UnauthorizedAccessException();
        var errors = new Dictionary<string, string[]>();
        void Error(string field, string message) => errors[field] = [message];
        if (input.SoTien is null || input.SoTien <= 0 || input.SoTien != decimal.Truncate(input.SoTien.Value) || input.SoTien > long.MaxValue)
            Error("SoTien", "Số tiền phải là số nguyên đồng lớn hơn 0.");
        if (!input.NgayThu.HasValue) Error("NgayThu", "Vui lòng nhập ngày thu.");
        else if (input.NgayThu > HomNay || input.NgayThu < HomNay.AddDays(-30))
            Error("NgayThu", $"Ngày thu phải từ {HomNay.AddDays(-30):dd/MM/yyyy} đến {HomNay:dd/MM/yyyy}.");
        if (input.HinhThuc is not ("TIEN_MAT" or "CHUYEN_KHOAN" or "KHAC")) Error("HinhThuc", "Chọn tiền mặt, chuyển khoản hoặc khác.");
        var note = input.GhiChu?.Trim();
        if (note?.Length > 255) Error("GhiChu", "Ghi chú tối đa 255 ký tự.");
        if (!Guid.TryParseExact(input.KhoaChongTrung, "N", out _)) Error("", "Phiên ghi thu không hợp lệ. Hãy đóng biểu mẫu và mở lại.");
        if (errors.Count != 0) throw new GhiThuValidationException(errors);

        var existing = await db.LanThuHoaDons.AsNoTracking().SingleOrDefaultAsync(x => x.KhoaChongTrung == input.KhoaChongTrung, ct);
        if (existing is not null)
        {
            var payment = await db.ThanhToans.AsNoTracking().SingleAsync(x => x.Id == existing.ThanhToanId, ct);
            if (payment.HoaDonId != input.HoaDonId || existing.NguoiXacNhanId != actorId || payment.SoTien != input.SoTien ||
                payment.NgayThanhToan != input.NgayThu || existing.HinhThuc != input.HinhThuc || existing.GhiChu != note)
                throw new InvalidOperationException("Phiên ghi thu đã được dùng cho nội dung khác. Hãy tải lại hóa đơn.");
            var replay = await SummaryAsync(invoice, ct);
            await tx!.CommitAsync(ct);
            return replay;
        }
        var before = await SummaryAsync(invoice, ct);
        if (!before.ChoPhepGhiThu)
            throw new GhiThuValidationException(new() { [""] = [$"Không thể ghi thu: hóa đơn hiện {before.TenTrangThai}."] });
        if (input.SoTien > before.ConPhaiTra)
            throw new GhiThuValidationException(new() { ["SoTien"] = [$"Số tiền vượt quá số còn phải trả. Tối đa được thu {before.ConPhaiTra.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ."] });
        var receipt = new ThanhToanHoaDon { HoaDonId = input.HoaDonId, SoTien = (long)input.SoTien!.Value,
            NgayThanhToan = input.NgayThu!.Value, TrangThai = "DA_XAC_NHAN" };
        db.ThanhToans.Add(receipt);
        await db.SaveChangesAsync(ct);
        db.LanThuHoaDons.Add(new() { ThanhToanId = receipt.Id, HinhThuc = input.HinhThuc!, GhiChu = note,
            NguoiXacNhanId = actorId, TenNguoiXacNhan = actor.HoTen, ThoiDiemGhi = clock.UtcNow, KhoaChongTrung = input.KhoaChongTrung! });
        await db.SaveChangesAsync(ct);
        var audit = JsonSerializer.Serialize(new { hoa_don_id = input.HoaDonId, so_tien = receipt.SoTien,
            ngay_thanh_toan = receipt.NgayThanhToan, hinh_thuc = input.HinhThuc, trang_thai = receipt.TrangThai });
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_sau,thoi_diem) VALUES({actorId},{actor.HoTen},{actor.VaiTro},{"thanh_toan"},{receipt.Id},{"TAO"},{audit},{clock.UtcNow})", ct);
        var result = await SummaryAsync(invoice, ct);
        await tx!.CommitAsync(ct);
        return result;
    }
}
