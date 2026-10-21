using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

// Reuses the existing private upload validation and storage. Keys never leave the backend.
public sealed class OwnerBankImageStore(string root)
{
    private readonly BaoHongImageStore store = new(root);
    public async Task<PreparedBaoHongImage?> PrepareAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null) return null;
        try { return (await store.PrepareAsync([file], ct))[0]; }
        catch (BaoHongValidationException ex) { throw new OwnerBankValidationException(new() { ["AnhQr"] = [ex.Message] }); }
    }
    public string FilePath(string key) => store.FilePath(key);
    public Task SaveAsync(string key, byte[] bytes, CancellationToken ct) => store.SaveAsync(key, bytes, ct);
    public void Delete(string? key)
    {
        if (key is null) return;
        // A failed cleanup must not turn a committed update into an apparent failure.
        try { store.Delete(key); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public sealed class OwnerBankService(AppDbContext db, OwnerBankImageStore images, ITimeProvider clock)
{
    private async Task RequireRole(int actor, string role, CancellationToken ct)
    {
        if (!await db.TaiKhoans.AnyAsync(x => x.Id == actor && x.VaiTro == role && x.DangHoatDong && !x.IsDeleted, ct))
            throw new UnauthorizedAccessException();
    }
    public async Task<TaiKhoanNganHang?> OwnAsync(int actor, CancellationToken ct)
    {
        await RequireRole(actor, "CHU_NHA", ct);
        return await db.TaiKhoanNganHangs.AsNoTracking().SingleOrDefaultAsync(x => x.ChuNhaId == actor, ct);
    }
    public async Task<TaiKhoanNganHang?> ForInvoiceAsync(int actor, int invoiceId, CancellationToken ct)
    {
        await RequireRole(actor, "KHACH_THUE", ct);
        var invoice = await (from i in db.HoaDons.AsNoTracking()
            join h in db.HopDongs on i.HopDongId equals h.Id
            join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id
            where i.Id == invoiceId && i.TrangThai == "DA_PHAT_HANH"
                && (h.TrangThai == "DANG_HIEU_LUC" || h.TrangThai == "DA_KET_THUC" || h.TrangThai == "CHO_HIEU_LUC")
            select new { h.Id, h.KhachDungTenId, b.ChuNhaId, i.TuNgay, i.DenNgay }).SingleOrDefaultAsync(ct);
        if (invoice is null) throw new UnauthorizedAccessException();
        var profiles = db.KhachThues.Where(x => x.TaiKhoanId == actor).Select(x => x.Id);
        var signer = invoice.KhachDungTenId.HasValue && await profiles.ContainsAsync(invoice.KhachDungTenId.Value, ct);
        var resident = await db.NguoiOGheps.AnyAsync(x => x.HopDongId == invoice.Id && profiles.Contains(x.KhachThueId)
            && x.NgayVao <= invoice.DenNgay && (x.NgayRa == null || x.NgayRa >= invoice.TuNgay), ct);
        if (!signer && !resident) throw new UnauthorizedAccessException();
        return await db.TaiKhoanNganHangs.AsNoTracking().SingleOrDefaultAsync(x => x.ChuNhaId == invoice.ChuNhaId, ct);
    }
    public async Task<TaiKhoanNganHang> SaveAsync(int actor, OwnerBankInput input, CancellationToken ct)
    {
        await RequireRole(actor, "CHU_NHA", ct);
        var errors = new Dictionary<string, string[]>();
        var bank = input.TenNganHang?.Trim() ?? "";
        var number = input.SoTaiKhoan?.Trim() ?? "";
        if (bank.Length is < 1 or > 120 || bank.Any(char.IsControl)) errors["TenNganHang"] = ["Nhập tên ngân hàng từ 1 đến 120 ký tự."];
        if (number.Length is < 1 or > 50 || number.Any(c => !char.IsAsciiLetterOrDigit(c))) errors["SoTaiKhoan"] = ["Nhập số tài khoản từ 1 đến 50 chữ cái hoặc chữ số, không có khoảng trắng."];
        PreparedBaoHongImage? prepared = null;
        try { prepared = await images.PrepareAsync(input.AnhQr, ct); }
        catch (OwnerBankValidationException ex) { foreach (var pair in ex.Errors) errors[pair.Key] = pair.Value; }
        if (errors.Count != 0) throw new OwnerBankValidationException(errors);

        await db.Database.OpenConnectionAsync(ct);
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqlite, ct);
        var current = await db.TaiKhoanNganHangs.SingleOrDefaultAsync(x => x.ChuNhaId == actor, ct);
        if ((current?.PhienBan ?? "") != (input.PhienBan ?? ""))
            throw new InvalidOperationException("Thông tin đã thay đổi. Hãy tải lại trang trước khi lưu.");
        var oldKey = current?.QrKey;
        var row = current ?? new TaiKhoanNganHang { ChuNhaId = actor, NgayTao = clock.UtcNow };
        row.TenNganHang = bank; row.SoTaiKhoan = number; row.NgayCapNhat = clock.UtcNow; row.PhienBan = Guid.NewGuid().ToString("N");
        if (input.XoaQr) { row.QrKey = null; row.QrContentType = null; }
        string? newKey = null;
        try
        {
            if (prepared is not null)
            {
                newKey = Guid.NewGuid().ToString("N");
                await images.SaveAsync(newKey, prepared.Bytes, ct);
                row.QrKey = newKey; row.QrContentType = prepared.ContentType;
            }
            if (current is null) db.TaiKhoanNganHangs.Add(row);
            await db.SaveChangesAsync(ct);
            await transaction!.CommitAsync(ct);
        }
        catch { images.Delete(newKey); throw; }
        if (oldKey != row.QrKey) images.Delete(oldKey);
        return row;
    }
    public static OwnerBankResponse? Response(TaiKhoanNganHang? row) => row is null ? null
        : new(row.TenNganHang, row.SoTaiKhoan, row.QrKey is not null, row.PhienBan, row.NgayCapNhat);
}
