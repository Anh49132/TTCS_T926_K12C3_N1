using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels.Auth;

namespace QL_PhongTro.Services;

public sealed class CredentialValidationResult
{
    public bool Success { get; init; }
    public TaiKhoan? User { get; init; }
    public string? Error { get; init; }
    public bool IsLocked { get; init; }
    public int LockoutRemainingMinutes { get; init; }
}

public interface ICredentialValidationService
{
    Task<CredentialValidationResult> ValidateAsync(LoginRequest request);
    Task<(bool blocked, int remainingMinutes)> CheckLockoutAsync(string taiKhoanDangNhap);
}

public class CredentialValidationService(AppDbContext db, ITimeProvider timeProvider) : ICredentialValidationService
{
    private readonly AppDbContext _db = db;
    private readonly ITimeProvider _timeProvider = timeProvider;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;

    // S1-02: thong bao loi khong duoc tiet lo tai khoan co ton tai hay khong, ke ca qua
    // thoi gian phan hoi. Khi khong tim thay tai khoan van phai chay BCrypt voi mot hash
    // gia de thoi gian tra ve ngang bang truong hop tai khoan co that.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    private static bool BurnPasswordTiming(string? password)
    {
        try { BCrypt.Net.BCrypt.Verify(password ?? string.Empty, DummyHash); }
        catch (BCrypt.Net.SaltParseException) { }
        return false;
    }

    public async Task<CredentialValidationResult> ValidateAsync(LoginRequest request)
    {
        if (_db.Database.CurrentTransaction is not null) return await ValidateInTransactionAsync(request);
        await _db.Database.OpenConnectionAsync();
        await using var sqlite = ((SqliteConnection)_db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var transaction = await _db.Database.UseTransactionAsync(sqlite);
        var result = await ValidateInTransactionAsync(request);
        await sqlite.CommitAsync();
        return result;
    }

    private async Task<CredentialValidationResult> ValidateInTransactionAsync(LoginRequest request)
    {
        var input = request.TaiKhoanDangNhap?.Trim() ?? "";
        if (string.IsNullOrEmpty(input))
        {
            return new CredentialValidationResult { Success = false, Error = "Phải nhập email hoặc số điện thoại" };
        }

        if (string.IsNullOrEmpty(request.MatKhau))
        {
            return new CredentialValidationResult { Success = false, Error = "Nhập mật khẩu." };
        }

        bool isEmail = input.Contains("@");
        var user = await _db.TaiKhoans
            .FirstOrDefaultAsync(u => !u.IsDeleted && (isEmail ? u.Email == input.ToLower() : u.SoDienThoai == input));
        if (user is not null) await _db.Entry(user).ReloadAsync();

        // Tai khoan da xoa hoa chua xac nhan email deu tra cung thong bao chung
        // "khong chinh xac" de khong lo tai khoan co that ra.
        if (user is null || user.IsDeleted || !user.DangHoatDong || !user.EmailConfirmed)
        {
            BurnPasswordTiming(request.MatKhau);
            return new CredentialValidationResult { Success = false, Error = "Thông tin đăng nhập không chính xác" };
        }

        var now = _timeProvider.UtcNow;
        if (user.LockedUntil is not null && user.LockedUntil > now)
        {
            return Locked(user.LockedUntil.Value, now);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.MatKhau ?? string.Empty, user.MatKhau))
        {
            await HandleFailedLogin(user, now);
            if (user.LockedUntil > now) return Locked(user.LockedUntil.Value, now);
            return new CredentialValidationResult { Success = false, Error = "Thông tin đăng nhập không chính xác" };
        }

        await HandleSuccessfulLogin(user, now);
        await _db.SaveChangesAsync();

        return new CredentialValidationResult { Success = true, User = user };
    }

    public async Task<(bool blocked, int remainingMinutes)> CheckLockoutAsync(string taiKhoanDangNhap)
    {
        var input = taiKhoanDangNhap?.Trim() ?? "";
        if (string.IsNullOrEmpty(input)) return (false, 0);

        bool isEmail = input.Contains("@");
        var user = await _db.TaiKhoans
            .FirstOrDefaultAsync(u => !u.IsDeleted && (isEmail ? u.Email == input.ToLower() : u.SoDienThoai == input));

        if (user is null || !user.DangHoatDong || !user.EmailConfirmed) return (false, 0);

        var now = _timeProvider.UtcNow;
        if (user.LockedUntil is not null && user.LockedUntil > now)
        {
            return (true, (int)Math.Ceiling((user.LockedUntil.Value - now).TotalMinutes));
        }

        return (false, 0);
    }

    private static CredentialValidationResult Locked(DateTime until, DateTime now)
    {
        var remaining = (int)Math.Ceiling((until - now).TotalMinutes);
        return new() { Success = false, Error = $"Tài khoản đã khoá. Vui lòng thử lại sau {remaining} phút",
            IsLocked = true, LockoutRemainingMinutes = remaining };
    }

    private async Task HandleFailedLogin(TaiKhoan user, DateTime now)
    {
        var timestamp = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var cutoff = timestamp - (long)TimeSpan.FromMinutes(LockoutMinutes).TotalMilliseconds;
        // Rolling window (cutoff, now], persisted across requests and app restarts.
        await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM login_failed_attempt WHERE attempted_at <= {cutoff}");
        await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO login_failed_attempt(account_id,attempted_at) VALUES({user.Id},{timestamp})");
        user.FailedLoginCount = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM login_failed_attempt WHERE account_id={0} AND attempted_at > {1} AND attempted_at <= {2}",
            user.Id, cutoff, timestamp).SingleAsync();
        user.LockedUntil = null;
        user.NgayCapNhat = now;

        if (user.FailedLoginCount >= MaxFailedAttempts)
        {
            user.LockedUntil = now.AddMinutes(LockoutMinutes);
        }

        await _db.SaveChangesAsync();
    }

    private async Task HandleSuccessfulLogin(TaiKhoan user, DateTime now)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM login_failed_attempt WHERE account_id={user.Id}");
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.NgayCapNhat = now;
        await _db.SaveChangesAsync();
    }
}
