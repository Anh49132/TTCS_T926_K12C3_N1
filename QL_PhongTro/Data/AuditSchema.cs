using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

internal static class AuditSchema
{
    internal static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS nhat_ky_hoat_dong (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nguoi_thuc_hien_id INTEGER REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ten_nguoi_thuc_hien TEXT,
                vai_tro_luc_thuc_hien TEXT,
                loai_doi_tuong TEXT NOT NULL,
                doi_tuong_id INTEGER NOT NULL,
                hanh_dong TEXT NOT NULL,
                du_lieu_truoc TEXT,
                du_lieu_sau TEXT,
                ghi_chu TEXT,
                thoi_diem TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        // Existing legacy rows are never rewritten with today's actor names.
        command.CommandText = "PRAGMA table_info(nhat_ky_hoat_dong)";
        bool hasName = false;
        using (var reader = command.ExecuteReader())
            while (reader.Read()) hasName |= reader.GetString(1) == "ten_nguoi_thuc_hien";
        if (!hasName)
        {
            command.CommandText = "ALTER TABLE nhat_ky_hoat_dong ADD COLUMN ten_nguoi_thuc_hien TEXT";
            command.ExecuteNonQuery();
        }
        command.CommandText = """
            CREATE INDEX IF NOT EXISTS ix_audit_time ON nhat_ky_hoat_dong(thoi_diem,id);
            CREATE INDEX IF NOT EXISTS ix_audit_actor ON nhat_ky_hoat_dong(nguoi_thuc_hien_id,thoi_diem);
            CREATE INDEX IF NOT EXISTS ix_audit_object ON nhat_ky_hoat_dong(loai_doi_tuong,doi_tuong_id,thoi_diem);
            CREATE TRIGGER IF NOT EXISTS audit_no_update BEFORE UPDATE ON nhat_ky_hoat_dong
            BEGIN SELECT RAISE(ABORT,'Activity log is append-only'); END;
            CREATE TRIGGER IF NOT EXISTS audit_no_delete BEFORE DELETE ON nhat_ky_hoat_dong
            BEGIN SELECT RAISE(ABORT,'Activity log is append-only'); END;
            """;
        command.ExecuteNonQuery();
        Validate(connection, transaction);
        command.CommandText = "PRAGMA integrity_check";
        if ((string?)command.ExecuteScalar() != "ok") throw new InvalidOperationException("Integrity check failed.");
        command.CommandText = "PRAGMA foreign_key_check";
        using (var reader = command.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("Foreign key check failed.");
        command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(3,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    internal static void Validate(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id,nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem FROM nhat_ky_hoat_dong LIMIT 0";
        try { using var reader = command.ExecuteReader(); }
        catch (SqliteException ex) { throw new InvalidOperationException("Audit schema is not ready. Run --update-database on the selected DatabasePath.", ex); }
        foreach (var operation in new[] { "update", "delete" })
        {
            command.CommandText = "SELECT sql FROM sqlite_master WHERE type='trigger' AND tbl_name='nhat_ky_hoat_dong' AND name=$name";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$name", "audit_no_" + operation);
            var sql = command.ExecuteScalar() as string;
            var normalized = sql is null ? "" : string.Join(" ", sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
            var expected = $"CREATE TRIGGER AUDIT_NO_{operation.ToUpperInvariant()} BEFORE {operation.ToUpperInvariant()} ON NHAT_KY_HOAT_DONG BEGIN SELECT RAISE(ABORT,'ACTIVITY LOG IS APPEND-ONLY'); END";
            if (normalized.TrimEnd(';') != expected)
                throw new InvalidOperationException("Audit protection missing or incompatible. Run --update-database; inspect existing audit triggers if already version 3.");
        }
    }
}
