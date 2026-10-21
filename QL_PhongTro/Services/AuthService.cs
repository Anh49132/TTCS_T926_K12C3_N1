using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels.Auth;

namespace QL_PhongTro.Services;

public class AuthService(AppDbContext db, TokenService tokenService, JwtSettings jwtSettings, ITimeProvider timeProvider, ICredentialValidationService credentialValidator)
{
    private readonly AppDbContext _db = db;
    private readonly TokenService _tokenService = tokenService;
    private readonly JwtSettings _jwtSettings = jwtSettings;
    private readonly ITimeProvider _timeProvider = timeProvider;
    private readonly ICredentialValidationService _credentialValidator = credentialValidator;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;

    public async Task<(bool success, LoginResponse? response, string? error)> LoginAsync(LoginRequest request)
    {
        // Serialize credential verification/token persistence with password reset (from dev).
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var validation = await _credentialValidator.ValidateAsync(request);
        if (!validation.Success)
        {
            // Phai commit de bo dem dang nhap sai va thoi gian khoa duoc ghi lai;
            // neu bo qua, transaction se rollback va tai khoan khong bao gio bi khoa.
            await transaction.CommitAsync();
            return (false, null, validation.Error);
        }

        var user = validation.User!;
        var now = _timeProvider.UtcNow;

        if (user.MustChangePassword)
            return (false, null, "Bạn phải đăng nhập tại /Account/Login và đổi mật khẩu tạm trước khi sử dụng API.");
        await HandleSuccessfulLogin(user, now);

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = ComputeHash(refreshToken);

        user.RefreshTokenHash = refreshTokenHash;
        user.RefreshTokenExpiry = now.AddDays(_jwtSettings.RefreshTokenDays);
        user.LastLogin = now;
        user.NgayCapNhat = now;

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();
        return (true, new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresIn = _jwtSettings.AccessTokenMinutes * 60,
            RefreshTokenExpiresIn = _jwtSettings.RefreshTokenDays * 24 * 60 * 60,
            UserId = user.Id.ToString(),
            HoTen = user.HoTen,
            VaiTro = user.VaiTro ?? "KHACH_THUE"
        }, null);
    }

    public async Task<(bool success, RefreshTokenResponse? response, string? error)> RefreshTokenAsync(string refreshToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var tokenHash = ComputeHash(refreshToken);
        var now = _timeProvider.UtcNow;
        var user = await _db.TaiKhoans
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.RefreshTokenHash == tokenHash && u.RefreshTokenExpiry > now && u.DangHoatDong);

        if (user is null || !user.EmailConfirmed || user.MustChangePassword)
        {
            return (false, null, "Refresh token không hợp lệ hoặc đã hết hạn");
        }

        if (user.LockedUntil is not null && user.LockedUntil > now)
        {
            var remaining = (int)Math.Ceiling((user.LockedUntil.Value - now).TotalMinutes);
            return (false, null, $"Tài khoản đã khoá. Vui lòng thử lại sau {remaining} phút");
        }

        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokenHash = ComputeHash(newRefreshToken);
        user.RefreshTokenExpiry = now.AddDays(_jwtSettings.RefreshTokenDays);
        user.NgayCapNhat = now;

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();
        return (true, new RefreshTokenResponse
        {
            AccessToken = newAccessToken,
            AccessTokenExpiresIn = _jwtSettings.AccessTokenMinutes * 60
        }, null);
    }

    public async Task<(bool success, string message)> LogoutAsync(string refreshToken, string? accessToken = null)
    {
        var tokenHash = ComputeHash(refreshToken);
        var user = await _db.TaiKhoans
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.RefreshTokenHash == tokenHash);

        if (user is not null)
        {
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiry = null;
            await _db.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(accessToken))
        {
            _tokenService.BlacklistAccessToken(accessToken, TimeSpan.FromMinutes(_jwtSettings.AccessTokenMinutes));
        }

        return (true, "Đăng xuất thành công");
    }

    public async Task<(bool blocked, int remainingMinutes)> CheckLockoutAsync(string taiKhoanDangNhap)
    {
        return await _credentialValidator.CheckLockoutAsync(taiKhoanDangNhap);
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private async Task HandleSuccessfulLogin(TaiKhoan user, DateTime now)
    {
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.NgayCapNhat = now;
        await _db.SaveChangesAsync();
    }
}