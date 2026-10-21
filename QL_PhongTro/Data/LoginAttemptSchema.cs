using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

// Authentication infrastructure only: no password, token or submitted identifier is stored.
public static class LoginAttemptSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS login_failed_attempt (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                account_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                attempted_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_login_failed_attempt_account_time ON login_failed_attempt(account_id, attempted_at);
            """;
        cmd.ExecuteNonQuery();
        Validate(connection, tx);
        cmd.CommandText = "PRAGMA integrity_check";
        if (cmd.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Integrity check không đạt; hoàn tác v42.");
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var reader = cmd.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("FK check không đạt; hoàn tác v42.");
        cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(42,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public static void Validate(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT id, account_id, attempted_at FROM login_failed_attempt LIMIT 0";
        using (var reader = cmd.ExecuteReader()) { }
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_index_info('ix_login_failed_attempt_account_time') WHERE (seqno=0 AND name='account_id') OR (seqno=1 AND name='attempted_at')";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 2)
            throw new InvalidOperationException("Thiếu hoặc sai index login_failed_attempt v42.");
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_foreign_key_list('login_failed_attempt') WHERE \"table\"='tai_khoan' AND \"from\"='account_id' AND \"to\"='id' AND on_delete='RESTRICT'";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
            throw new InvalidOperationException("Thiếu hoặc sai FK login_failed_attempt v42.");
    }
}
