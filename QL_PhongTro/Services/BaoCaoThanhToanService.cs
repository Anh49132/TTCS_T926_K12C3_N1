using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed class BaoCaoThanhToanService(AppDbContext db, PaymentProofImageStore? proofImages = null, ITimeProvider? clock = null) : IBaoCaoThanhToanService
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

    public async Task<HuyBaoCaoThanhToanResult> HuyAsync(int actorId, int invoiceId, CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqlite, cancellationToken);

        var invoice = await (from bill in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs.AsNoTracking() on bill.HopDongId equals contract.Id
            join tenant in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals tenant.Id
            where bill.Id == invoiceId && tenant.TaiKhoanId == actorId
            select new { bill.Id, bill.MaHoaDon }).SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) throw new UnauthorizedAccessException();

        var pendingPaymentId = await db.ThanhToans
            .Where(x => x.HoaDonId == invoiceId && x.TrangThai == "CHO_XAC_NHAN")
            .OrderByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (pendingPaymentId is null)
        {
            await transaction!.CommitAsync(cancellationToken);
            return new(invoice.MaHoaDon, false);
        }

        var updated = await db.ThanhToans
            .Where(x => x.Id == pendingPaymentId.Value && x.HoaDonId == invoiceId && x.TrangThai == "CHO_XAC_NHAN")
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.TrangThai, "DA_HUY"), cancellationToken);
        if (updated == 1)
        {
            await AuditAsync(actorId, pendingPaymentId.Value, "CHO_XAC_NHAN", "DA_HUY", cancellationToken);
            var metadata = await db.BaoCaoThanhToans.SingleOrDefaultAsync(x => x.ThanhToanId == pendingPaymentId, cancellationToken);
            if (metadata is not null) { metadata.NguoiXuLyId = actorId; metadata.XuLyLuc = (clock ?? new SystemTimeProvider()).UtcNow; }
            await PaymentWorkflow.NotifyAsync(db, pendingPaymentId.Value, "DA_HUY", (clock ?? new SystemTimeProvider()).UtcNow, cancellationToken);
        }
        await transaction!.CommitAsync(cancellationToken);
        return new(invoice.MaHoaDon, updated == 1);
    }

    public async Task GuiAsync(int actorId, int invoiceId, decimal? amount, DateOnly? paymentDate, string? paymentMethod,
        CancellationToken cancellationToken = default)
        => await GuiAsync(actorId, invoiceId, amount, paymentDate, paymentMethod, null, cancellationToken);

    public async Task GuiAsync(int actorId, int invoiceId, decimal? amount, DateOnly? paymentDate, string? paymentMethod,
        IFormFile? proofImage, CancellationToken cancellationToken = default)
        => await GuiCoGhiChuAsync(actorId, invoiceId, amount, paymentDate, paymentMethod, proofImage, null, cancellationToken);

    public async Task GuiCoGhiChuAsync(int actorId, int invoiceId, decimal? amount, DateOnly? paymentDate, string? paymentMethod,
        IFormFile? proofImage, string? note, CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqlite, cancellationToken);
        var invoice = await (from bill in db.HoaDons
            join contract in db.HopDongs on bill.HopDongId equals contract.Id
            join tenant in db.KhachThues on contract.KhachDungTenId equals tenant.Id
            where bill.Id == invoiceId && tenant.TaiKhoanId == actorId
            select bill).SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) throw new UnauthorizedAccessException();

        var errors = new Dictionary<string, string[]>();
        if (note?.Length > 300) errors["GhiChu"] = ["Ghi chú tối đa 300 ký tự."];
        void Error(string field, string message) => errors[field] = [message];
        if (invoice.TrangThai != "DA_PHAT_HANH")
            Error("", "Chỉ có thể báo thanh toán hóa đơn đã phát hành.");
        if (amount is null || amount <= 0 || amount != decimal.Truncate(amount.Value) || amount > long.MaxValue)
            Error("SoTien", "Số tiền phải là số nguyên đồng lớn hơn 0.");
        if (!paymentDate.HasValue)
            Error("NgayThanhToan", "Vui lòng chọn ngày thanh toán.");
        if (paymentMethod is not ("TIEN_MAT" or "CHUYEN_KHOAN"))
            Error("HinhThucThanhToan", "Chọn tiền mặt hoặc chuyển khoản.");
        if (paymentMethod == "CHUYEN_KHOAN" &&
            (proofImage is null || (proofImage.Length == 0 && string.IsNullOrWhiteSpace(proofImage.FileName))))
            Error("AnhChungTu", "Chuyển khoản cần kèm ảnh chứng từ JPG/JPEG hoặc PNG, tối đa 5 MiB.");
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

        var imageStore = proofImages ?? new PaymentProofImageStore(db.FileStoragePath + ".payment-proofs");
        var preparedImage = await imageStore.PrepareAsync(proofImage, cancellationToken);
        var imageKey = preparedImage is null ? null : Guid.NewGuid().ToString("N");
        var report = new ThanhToanHoaDon
        {
            HoaDonId = invoiceId,
            SoTien = decimal.ToInt64(amount!.Value),
            NgayThanhToan = paymentDate!.Value,
            HinhThucBaoCao = paymentMethod,
            TrangThai = "CHO_XAC_NHAN",
            AnhChungTuKey = imageKey,
            AnhChungTuContentType = preparedImage?.ContentType,
            AnhChungTuSoByte = preparedImage?.Bytes.LongLength
        };
        db.ThanhToans.Add(report);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var filename = proofImage is null ? null : Path.GetFileName(proofImage.FileName.Replace('\\', '/'));
            if (filename?.Length > 120) filename = filename[..120];
            db.BaoCaoThanhToans.Add(new() { ThanhToanId = report.Id, NguoiGuiId = actorId,
                GuiLuc = (clock ?? new SystemTimeProvider()).UtcNow, GhiChu = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), TenTep = filename });
            await AuditAsync(actorId, report.Id, null, "CHO_XAC_NHAN", cancellationToken);
            if (preparedImage is not null)
                await imageStore.SaveAsync(imageKey!, preparedImage.Bytes, cancellationToken);
            await PaymentWorkflow.NotifyAsync(db, report.Id, "BAO_MOI", (clock ?? new SystemTimeProvider()).UtcNow, cancellationToken);
            await transaction!.CommitAsync(cancellationToken);
        }
        catch
        {
            if (imageKey is not null) imageStore.Delete(imageKey);
            throw;
        }
    }

    private async Task AuditAsync(int actorId, int paymentId, string? before, string after, CancellationToken ct)
    {
        var actor = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.DangHoatDong && !x.IsDeleted && x.VaiTro == "KHACH_THUE", ct)
            ?? throw new UnauthorizedAccessException();
        var prior = before is null ? null : JsonSerializer.Serialize(new { trang_thai = before });
        var next = JsonSerializer.Serialize(new { trang_thai = after });
        var now = (clock ?? new SystemTimeProvider()).UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) VALUES({actorId},{actor.HoTen},{actor.VaiTro},{"thanh_toan"},{paymentId},{(before == null ? "TAO" : "DOI_TRANG_THAI")},{prior},{next},{now})", ct);
    }

    private Task<bool> LaHoaDonCuaKhachAsync(int actorId, int invoiceId, CancellationToken cancellationToken) =>
        (from bill in db.HoaDons.AsNoTracking()
         join contract in db.HopDongs.AsNoTracking() on bill.HopDongId equals contract.Id
         join tenant in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals tenant.Id
         where bill.Id == invoiceId && tenant.TaiKhoanId == actorId
         select bill.Id).AnyAsync(cancellationToken);
}
