using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class InvoicePaymentProofSchema
{
    public static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='thanh_toan'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return;

        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        command.CommandText = "PRAGMA table_info('thanh_toan')";
        using (var reader = command.ExecuteReader())
            while (reader.Read()) existingColumns.Add(reader.GetString(1));

        if (!existingColumns.Contains("anh_chung_tu_key"))
            Execute("ALTER TABLE thanh_toan ADD COLUMN anh_chung_tu_key TEXT");
        if (!existingColumns.Contains("anh_chung_tu_content_type"))
            Execute("""
                ALTER TABLE thanh_toan ADD COLUMN anh_chung_tu_content_type TEXT
                    CHECK(anh_chung_tu_content_type IS NULL OR anh_chung_tu_content_type IN ('image/jpeg','image/png'))
                """);
        if (!existingColumns.Contains("anh_chung_tu_so_byte"))
            Execute("""
                ALTER TABLE thanh_toan ADD COLUMN anh_chung_tu_so_byte INTEGER
                    CHECK(anh_chung_tu_so_byte IS NULL OR anh_chung_tu_so_byte BETWEEN 1 AND 5242880)
                """);
        Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_thanh_toan_anh_chung_tu_key ON thanh_toan(anh_chung_tu_key) WHERE anh_chung_tu_key IS NOT NULL");

        command.CommandText = """
            SELECT anh_chung_tu_key,anh_chung_tu_content_type,anh_chung_tu_so_byte FROM thanh_toan LIMIT 0
            """;
        using (command.ExecuteReader()) { }
        return;

        void Execute(string sql)
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        Ensure(connection, transaction);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(36,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
