using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed class BaoCaoThanhToanService(AppDbContext db) : IBaoCaoThanhToanService
{
    public async Task<ThanhToanHoaDon?> LayBaoCaoChoXacNhanAsync(
        int actorId, int invoiceId, CancellationToken cancellationToken = default)
    {
        if (!await LaHoaDonCuaKhachAsync(actorId, invoiceId, cancellationToken))
            throw new UnauthorizedAccessException();

        return await db.ThanhToans.AsNoTracking()
            .Where(x => x.HoaDonId == invoiceId && x.TrangThai == "CHO_XAC_NHAN")
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task GuiAsync(int actorId, int invoiceId, decimal? amount, DateOnly? paymentDate, string? paymentMethod,
        CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqlite, cancellationToken);
        var invoice = await (from bill in db.HoaDons
            join contract in db.HopDongs on bill.HopDongId equals contract.Id
            join tenant in db.KhachThues on contract.KhachDungTenId equals tenant.Id
            where bill.Id == invoiceId && tenant.TaiKhoanId == actorId
            select bill).SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) throw new UnauthorizedAccessException();

        var errors = new Dictionary<string, string[]>();
        void Error(string field, string message) => errors[field] = [message];
        if (invoice.TrangThai != "DA_PHAT_HANH")
            Error("", "Chỉ có thể báo thanh toán hóa đơn đã phát hành.");
        if (amount is null || amount <= 0 || amount != decimal.Truncate(amount.Value) || amount > long.MaxValue)
            Error("SoTien", "Số tiền phải là số nguyên đồng lớn hơn 0.");
        if (!paymentDate.HasValue)
            Error("NgayThanhToan", "Vui lòng chọn ngày thanh toán.");
        if (paymentMethod is not ("TIEN_MAT" or "CHUYEN_KHOAN"))
            Error("HinhThucThanhToan", "Chọn tiền mặt hoặc chuyển khoản.");
        if (await db.ThanhToans.AnyAsync(x => x.HoaDonId == invoiceId && x.TrangThai == "CHO_XAC_NHAN", cancellationToken))
            Error("", "Hóa đơn đã có báo cáo đang chờ xác nhận.");

        if (errors.Count == 0)
        {
            var paid = await db.ThanhToans.Where(x => x.HoaDonId == invoiceId && x.TrangThai == "DA_XAC_NHAN")
                .SumAsync(x => (long?)x.SoTien, cancellationToken) ?? 0L;
            var offset = (await CanTruHoaDonQueries.LayAsync(db, [invoiceId])).GetValueOrDefault(invoiceId);
            var remaining = Math.Max(0L, checked(invoice.TongTien - paid - offset));
            if (amount > remaining)
                Error("SoTien", $"Số tiền báo trả không được vượt quá số còn phải trả ({remaining:N0} đ).");
        }

        if (errors.Count != 0) throw new GhiThuValidationException(errors);

        db.ThanhToans.Add(new ThanhToanHoaDon
        {
            HoaDonId = invoiceId,
            SoTien = decimal.ToInt64(amount!.Value),
            NgayThanhToan = paymentDate!.Value,
            HinhThucBaoCao = paymentMethod,
            TrangThai = "CHO_XAC_NHAN"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction!.CommitAsync(cancellationToken);
    }

    private Task<bool> LaHoaDonCuaKhachAsync(int actorId, int invoiceId, CancellationToken cancellationToken) =>
        (from bill in db.HoaDons.AsNoTracking()
         join contract in db.HopDongs.AsNoTracking() on bill.HopDongId equals contract.Id
         join tenant in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals tenant.Id
         where bill.Id == invoiceId && tenant.TaiKhoanId == actorId
         select bill.Id).AnyAsync(cancellationToken);
}
