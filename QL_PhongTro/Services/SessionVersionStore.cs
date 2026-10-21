using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;

namespace QL_PhongTro.Services;

public sealed class SessionVersionStore(AppDbContext db)
{
    public const string ClaimType = "session_version";
    // Legacy cookies/JWTs without this claim have version 0, valid only before the first reset.
    public string? Capture(int accountId, string passwordHash)
    {
        using var c = new SqliteConnection(db.Database.GetConnectionString()); c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE((SELECT version FROM account_session_version WHERE account_id=a.id),'0')
            FROM tai_khoan a WHERE id=$id AND mat_khau=$hash AND dang_hoat_dong=1 AND is_deleted=0 AND email_confirmed=1
            """;
        cmd.Parameters.AddWithValue("$id",accountId);cmd.Parameters.AddWithValue("$hash",passwordHash);
        return cmd.ExecuteScalar() as string;
    }
    public bool IsValid(int accountId, string? version)
    {
        using var c = new SqliteConnection(db.Database.GetConnectionString()); c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE((SELECT version FROM account_session_version WHERE account_id=a.id),'0')
            FROM tai_khoan a WHERE id=$id AND dang_hoat_dong=1 AND is_deleted=0 AND email_confirmed=1
            """;
        cmd.Parameters.AddWithValue("$id",accountId);
        return cmd.ExecuteScalar() is string current && current == (version ?? "0");
    }
}
