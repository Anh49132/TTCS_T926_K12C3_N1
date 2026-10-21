using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QL_PhongTro.Data;

namespace QL_PhongTro.Services;

public sealed class PasswordResetService(AppDbContext db, ITimeProvider clock, IPasswordEmailSender sender,
    IOptions<PasswordResetOptions> options, IWebHostEnvironment environment)
{
    public const string AcceptedMessage = "Nếu email thuộc tài khoản Khách thuê đang hoạt động, bạn sẽ nhận được liên kết đặt lại mật khẩu có hiệu lực trong 30 phút.";
    private SqliteConnection Open()
    {
        var c = new SqliteConnection(db.Database.GetConnectionString()); c.Open(); return c;
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static bool TokenShape(string? token) => token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public async Task<bool> RequestAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();
        var accounts = await db.TaiKhoans.AsNoTracking().Where(a => !a.IsDeleted && a.EmailConfirmed && a.Email.Trim().ToLower() == email && a.VaiTro == "KHACH_THUE" && a.DangHoatDong).Take(2).ToListAsync();
        var account = accounts.Count == 1 ? accounts[0] : null;
        if (account is not null && (!Uri.TryCreate(options.Value.PublicBaseUrl, UriKind.Absolute, out var origin) ||
            (origin.Scheme != "https" && !(environment.IsDevelopment() && origin.Scheme == "http" && origin.IsLoopback)) ||
            !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment)))
            throw new InvalidOperationException("Configure a trusted PublicBaseUrl (HTTPS in production).");
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var c = Open();
        using (var tx = c.BeginTransaction(deferred: false))
        using (var cmd = c.CreateCommand())
        {
            cmd.Transaction = tx;
            var now = clock.UtcNow;
            cmd.Parameters.AddWithValue("$email",Hash(email));cmd.Parameters.AddWithValue("$window",now.AddHours(-1).Ticks);
            cmd.CommandText = "SELECT COUNT(*) FROM password_reset_request WHERE email_key=$email AND requested_at>$window";
            if (Convert.ToInt32(cmd.ExecuteScalar()) >= 3) return false;
            cmd.Parameters.AddWithValue("$now",now.Ticks);
            cmd.CommandText = "INSERT INTO password_reset_request(email_key,requested_at) VALUES ($email,$now)";cmd.ExecuteNonQuery();
            if (account is not null)
            {
                cmd.CommandText = "INSERT INTO password_reset_token(token_hash,account_id,expires_at) VALUES ($hash,$id,$expires)";
                cmd.Parameters.AddWithValue("$hash",Hash(token));cmd.Parameters.AddWithValue("$id",account.Id);
                cmd.Parameters.AddWithValue("$expires",now.AddMinutes(30).Ticks);cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        if (account is null) return true;
        try { await sender.SendAsync(account.Email, options.Value.PublicBaseUrl.TrimEnd('/') + "/Account/ResetPassword?token=" + token); }
        catch
        {
            using var cleanup = c.CreateCommand();cleanup.CommandText = "DELETE FROM password_reset_token WHERE token_hash=$hash";
            cleanup.Parameters.AddWithValue("$hash",Hash(token));cleanup.ExecuteNonQuery();
            // Failed deliveries still consume a request, preventing retry floods during SMTP failures.
            throw;
        }
        return true;
    }
    public bool IsValid(string? token)
    {
        if (!TokenShape(token)) return false;
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM password_reset_token t JOIN tai_khoan a ON a.id=t.account_id
            WHERE t.token_hash=$hash AND t.used_at IS NULL AND t.expires_at>$now AND a.dang_hoat_dong=1 AND a.is_deleted=0 AND a.email_confirmed=1 AND a.vai_tro='KHACH_THUE'
            """;
        cmd.Parameters.AddWithValue("$hash",Hash(token!));cmd.Parameters.AddWithValue("$now",clock.UtcNow.Ticks);
        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }
    public bool Reset(string? token, string newPassword)
    {
        if (!TokenShape(token)) return false;
        var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        using var c = Open(); using var tx = c.BeginTransaction(deferred: false); using var cmd = c.CreateCommand();cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT t.account_id FROM password_reset_token t JOIN tai_khoan a ON a.id=t.account_id
            WHERE t.token_hash=$token AND t.used_at IS NULL AND t.expires_at>$now AND a.dang_hoat_dong=1 AND a.is_deleted=0 AND a.email_confirmed=1 AND a.vai_tro='KHACH_THUE'
            """;
        var now = clock.UtcNow;
        cmd.Parameters.AddWithValue("$token",Hash(token!)); cmd.Parameters.AddWithValue("$now",now.Ticks);
        var id = cmd.ExecuteScalar(); if (id is null) return false;
        cmd.Parameters.AddWithValue("$id",id);cmd.Parameters.AddWithValue("$password",hash);cmd.Parameters.AddWithValue("$updated",now);
        cmd.CommandText = "UPDATE tai_khoan SET mat_khau=$password,ngay_cap_nhat=$updated WHERE id=$id";cmd.ExecuteNonQuery();
        cmd.CommandText = "UPDATE password_reset_token SET used_at=$now WHERE token_hash=$token";cmd.ExecuteNonQuery();
        cmd.CommandText = "UPDATE password_reset_token SET used_at=$now WHERE account_id=$id AND used_at IS NULL";cmd.ExecuteNonQuery();
        cmd.Parameters.AddWithValue("$version",Guid.NewGuid().ToString("N"));
        cmd.CommandText = "INSERT INTO account_session_version(account_id,version) VALUES ($id,$version) ON CONFLICT(account_id) DO UPDATE SET version=excluded.version";cmd.ExecuteNonQuery();
        cmd.CommandText = "UPDATE tai_khoan SET refresh_token_hash=NULL,refresh_token_expiry=NULL,failed_login_count=0,locked_until=NULL WHERE id=$id";cmd.ExecuteNonQuery();
        tx.Commit(); return true;
    }
}
