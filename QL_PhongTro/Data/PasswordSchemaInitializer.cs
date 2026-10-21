using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class PasswordSchemaInitializer
{
    public static void Initialize(string databasePath)
    {
        using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        c.Open();
        using var check = c.CreateCommand();
        check.CommandText = "SELECT id, email, mat_khau, dang_hoat_dong, vai_tro, ngay_cap_nhat FROM tai_khoan LIMIT 0";
        using (check.ExecuteReader()) { }
        var backupPath = databasePath + ".before-s105-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString()))
        { backup.Open(); c.BackupDatabase(backup); }
        using var tx = c.BeginTransaction();
        using var command = c.CreateCommand(); command.Transaction = tx;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS password_reset_token (
                token_hash TEXT PRIMARY KEY NOT NULL,
                account_id INTEGER NOT NULL REFERENCES tai_khoan(id),
                expires_at INTEGER NOT NULL,
                used_at INTEGER NULL
            );
            CREATE TABLE IF NOT EXISTS account_session_version (
                account_id INTEGER PRIMARY KEY REFERENCES tai_khoan(id),
                version TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS password_reset_request (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                email_key TEXT NOT NULL,
                requested_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_password_reset_request_email_time ON password_reset_request(email_key,requested_at);
            """;
        command.ExecuteNonQuery(); tx.Commit();
        Console.WriteLine("Password schema ready. Backup: " + backupPath);
    }
}
