using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

// Additive v20: business issue date stays separate from UTC publication timestamps.
public static class InvoiceIssueDateSchema
{
    public static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='hoa_don'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return;
        command.CommandText = "SELECT type,\"notnull\" FROM pragma_table_info('hoa_don') WHERE name='ngay_phat_hanh_nghiep_vu'";
        using (var reader = command.ExecuteReader())
        {
            if (reader.Read())
            {
                if (!string.Equals(reader.GetString(0), "TEXT", StringComparison.OrdinalIgnoreCase) || reader.GetInt32(1) != 0)
                    throw new InvalidOperationException("Cột ngày nghiệp vụ không khớp v20; không thay đổi schema hiện có.");
                return;
            }
        }
        command.CommandText = "ALTER TABLE hoa_don ADD COLUMN ngay_phat_hanh_nghiep_vu TEXT NULL CHECK(ngay_phat_hanh_nghiep_vu IS NULL OR (date(ngay_phat_hanh_nghiep_vu) IS NOT NULL AND ngay_phat_hanh_nghiep_vu=date(ngay_phat_hanh_nghiep_vu)))";
        command.ExecuteNonQuery();
        // Old invoices retain NULL; their displayed issue date uses the old UTC timestamp.
    }

    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        Ensure(connection, tx);
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(20,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        command.ExecuteNonQuery();
        tx.Commit();
    }
}
