using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class InvoicePaymentReportSchema
{
    public static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='thanh_toan'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return;

        command.CommandText = "PRAGMA table_info('thanh_toan')";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                if (string.Equals(reader.GetString(1), "hinh_thuc_bao_cao", StringComparison.OrdinalIgnoreCase))
                    return;
        }

        command.CommandText = """
            ALTER TABLE thanh_toan ADD COLUMN hinh_thuc_bao_cao TEXT
                CHECK(hinh_thuc_bao_cao IS NULL OR hinh_thuc_bao_cao IN ('TIEN_MAT','CHUYEN_KHOAN'))
            """;
        command.ExecuteNonQuery();
    }

    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        Ensure(connection, transaction);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(35,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
