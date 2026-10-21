using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed class RequestCodeExhaustedException() : Exception("Đã hết mã yêu cầu trong tháng. Vui lòng liên hệ quản lý.");
public sealed class DesiredDateException(string message) : Exception(message);
public sealed class OpenRequestExistsException(int requestId) : Exception("Bạn đã có yêu cầu đang mở cho tin đăng này. Không tạo thêm yêu cầu mới.")
{
    public int RequestId { get; } = requestId;
}
public sealed class RoomCapacityException(int maximum) : Exception($"Phòng chỉ cho phép tối đa {maximum} người.")
{
    public int Maximum { get; } = maximum;
}

public class YeuCauThueService(AppDbContext db, ITimeProvider clock)
{
    // PO: MOI, DA_HEN_LICH and DA_DUYET remain open; TU_CHOI/DA_HUY allow resending.
    public Task<YeuCauThue?> FindOpenRequest(int listingId, int accountId) =>
        db.YeuCauThues.AsNoTracking().Where(r => r.TinDangId == listingId
            && (r.TrangThai == "MOI" || r.TrangThai == "DA_HEN_LICH" || r.TrangThai == "DA_DUYET")
            && db.KhachThues.Any(k => k.Id == r.KhachThueId && k.TaiKhoanId == accountId))
            .OrderBy(r => r.Id).FirstOrDefaultAsync();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc),
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));

    public string? ValidateDesiredDate(DateOnly? date)
    {
        var today = Today;
        if (date is null) return "Vui lòng nhập ngày mong muốn hợp lệ.";
        if (date < today) return "Ngày mong muốn không được là ngày trong quá khứ.";
        if (date > today.AddDays(60)) return "Ngày mong muốn không được quá 60 ngày kể từ hôm nay.";
        return null;
    }

    public IQueryable<TinDang> PublicListings()
    {
        var now = clock.UtcNow;
        return db.TinDangs.AsNoTracking().Where(t => t.TrangThai == "DANG_HIEN_THI"
            && (t.NgayHetHan == null || t.NgayHetHan > now)
            && db.PhongTros.Any(p => p.Id == t.PhongId && p.TrangThai == "TRONG"
                && db.ToaNhas.Any(b => b.Id == p.ToaNhaId && b.DangHoatDong)));
    }

    public async Task<bool> IsInstalled()
    {
        await db.Database.OpenConnectionAsync();
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('tin_dang','yeu_cau_thue','rental_request_counter','rental_request_schema')";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 4;
    }

    public async Task<YeuCauThue?> Send(int listingId, int accountId, GuiYeuCauViewModel form)
    {
        if (ValidateDesiredDate(form.NgayMongMuon) is { } dateError)
            throw new DesiredDateException(dateError);
        // Serialize counter allocation and profile creation across all web processes.
        await db.Database.OpenConnectionAsync();
        using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var efTransaction = await db.Database.UseTransactionAsync(tx);
        var account = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId
            && a.VaiTro == "KHACH_THUE" && a.DangHoatDong && !a.IsDeleted && a.EmailConfirmed && !a.MustChangePassword);
        if (account is null) throw new UnauthorizedAccessException();
        var capacity = await (from listing in PublicListings()
                              join room in db.PhongTros.AsNoTracking() on listing.PhongId equals room.Id
                              where listing.Id == listingId
                              select (int?)room.SoNguoiToiDa).SingleOrDefaultAsync();
        if (capacity is null) return null;
        // The immediate transaction serializes this check with every Send, including other processes.
        // Reject before creating a profile, allocating a code or writing an audit entry.
        if (await FindOpenRequest(listingId, accountId) is { } existing)
            throw new OpenRequestExistsException(existing.Id);
        if (form.SoNguoiDuKien > capacity.Value) throw new RoomCapacityException(capacity.Value);
        var profile = await db.KhachThues.SingleOrDefaultAsync(k => k.TaiKhoanId == accountId);
        if (profile is null)
        {
            profile = new KhachThue { TaiKhoanId = accountId, HoTen = account.HoTen, NgayTao = clock.UtcNow };
            db.KhachThues.Add(profile);
            await db.SaveChangesAsync();
        }
        var vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var month = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), vietnam)
            .ToString("yyyyMM", CultureInfo.InvariantCulture);
        using var cmd = tx.Connection!.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO rental_request_counter(thang,so_cuoi) VALUES($month,1)
            ON CONFLICT(thang) DO UPDATE SET so_cuoi=so_cuoi+1 WHERE so_cuoi<9999
            RETURNING so_cuoi;
            """;
        cmd.Parameters.AddWithValue("$month", month);
        var number = await cmd.ExecuteScalarAsync();
        if (number is null) throw new RequestCodeExhaustedException();
        var request = new YeuCauThue
        {
            MaYeuCau = $"YC-{month}-{Convert.ToInt32(number):D4}", TinDangId = listingId,
            KhachThueId = profile.Id, LoaiYeuCau = form.LoaiYeuCau!, NgayMongMuon = form.NgayMongMuon!.Value,
            SoNguoiDuKien = form.SoNguoiDuKien!.Value, LoiNhan = form.LoiNhan?.Trim(), NgayTao = clock.UtcNow
        };
        db.YeuCauThues.Add(request);
        await db.SaveChangesAsync();
        tx.Commit();
        return request;
    }
}
