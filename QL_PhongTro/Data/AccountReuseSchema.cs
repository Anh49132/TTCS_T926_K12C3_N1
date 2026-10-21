using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

// Narrow, automatic v4 -> v5 update. Newer application schema versions keep the
// same account-reuse contract, so validation remains safe after unrelated upgrades.
public static class AccountReuseSchema
{
    private static readonly Dictionary<string, string> Indexes = new()
    {
        ["ux_account_email_normalized"] = "CREATE UNIQUE INDEX ux_account_email_normalized ON tai_khoan(lower(trim(email)))",
        ["ux_account_phone"] = "CREATE UNIQUE INDEX ux_account_phone ON tai_khoan(so_dien_thoai)"
    };

    public static void Ensure(string path)
    {
        using var c = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true }.ToString());
        c.Open();
        using var tx = c.BeginTransaction(deferred: false);
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT MAX(version) FROM app_schema_version";
        var version = Convert.ToInt32(cmd.ExecuteScalar());
        if (version < 4 || version > DatabaseUpdates.CurrentVersion) throw new InvalidOperationException($"Account reuse requires schema v4-v{DatabaseUpdates.CurrentVersion}; no changes made.");
        // Refuse unknown UNIQUE constraints (including SQLite autoindexes) rather than
        // rebuild a referenced table or silently leave a constraint blocking reuse.
        cmd.CommandText = "PRAGMA index_list(tai_khoan)";
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
                if (reader.GetInt32(2) == 1 && !Indexes.ContainsKey(reader.GetString(1)))
                    throw new InvalidOperationException("Unexpected account UNIQUE index: " + reader.GetString(1));
        foreach (var (name, sql) in Indexes)
        {
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name=$name AND tbl_name='tai_khoan'";
            cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$name", name);
            var expected = sql + (version >= 5 ? " WHERE is_deleted = 0" : "");
            if (cmd.ExecuteScalar() as string != expected)
                throw new InvalidOperationException("Unexpected account index definition: " + name);
        }
        cmd.Parameters.Clear();
        if (version >= 5) { tx.Commit(); return; }
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var reader = cmd.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("Existing foreign key violations; account update cancelled.");
        var backupPath = path + ".before-account-reuse-" + Guid.NewGuid().ToString("N") + ".bak";
        // The write transaction serializes other writers; a separate read connection
        // backs up the committed pre-update snapshot without backing up an active writer.
        using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString()))
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
        { source.Open(); backup.Open(); source.BackupDatabase(backup); }
        foreach (var (name, sql) in Indexes)
        {
            cmd.CommandText = $"DROP INDEX {name}; {sql} WHERE is_deleted = 0;";
            cmd.ExecuteNonQuery();
        }
        cmd.CommandText = """
            UPDATE tai_khoan SET dang_hoat_dong=0, refresh_token_hash=NULL, refresh_token_expiry=NULL
                WHERE is_deleted=1;
            UPDATE toa_nha SET quan_ly_id=NULL WHERE quan_ly_id IN
                (SELECT id FROM tai_khoan WHERE is_deleted=1 AND vai_tro='QUAN_LY');
            INSERT INTO account_session_version(account_id,version)
                SELECT id,lower(hex(randomblob(16))) FROM tai_khoan WHERE is_deleted=1
                ON CONFLICT(account_id) DO UPDATE SET version=excluded.version;
            UPDATE password_reset_token SET used_at=COALESCE(used_at,0)
                WHERE account_id IN (SELECT id FROM tai_khoan WHERE is_deleted=1);
            DELETE FROM email_confirmation WHERE account_id IN (SELECT id FROM tai_khoan WHERE is_deleted=1);
            INSERT INTO app_schema_version(version,applied_at) VALUES(5,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        cmd.ExecuteNonQuery();
        tx.Commit();
        Console.WriteLine("Account reuse schema v5 ready. Backup: " + backupPath);
    }
}
