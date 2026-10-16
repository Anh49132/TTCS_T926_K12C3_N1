using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class ReportPerformanceSchema
{
    private static readonly (string Table, string Name, string Columns)[] Indexes =
    [
        ("hoa_don", "ix_hoa_don_report_status_period_contract", "trang_thai,nam,thang,hop_dong_id"),
        ("hop_dong", "ix_hop_dong_report_room_status_return", "phong_id,trang_thai,ngay_tra_phong"),
        ("ky_hop_dong", "ix_ky_hop_dong_report_contract_dates", "hop_dong_id,ngay_bat_dau,ngay_ket_thuc"),
        ("thanh_toan", "ix_thanh_toan_hoa_don_trang_thai", "hoa_don_id,trang_thai"),
        ("lich_su_trang_thai_phong", "ix_lich_su_trang_thai_phong_report_room_times", "phong_id,tu_thoi_diem,den_thoi_diem")
    ];

    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        Ensure(connection, transaction);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(35,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        command.ExecuteNonQuery();
        transaction.Commit();
        Validate(connection);
    }

    public static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        foreach (var (table, name, columns) in Indexes)
        {
            if (!HasTable(connection, transaction, table))
                continue;

            command.CommandText = $"CREATE INDEX IF NOT EXISTS {name} ON {table}({columns})";
            command.ExecuteNonQuery();
        }

    }

    public static void Validate(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        foreach (var (table, name, columns) in Indexes)
        {
            if (!HasTable(connection, transaction, table))
                continue;

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                SELECT COUNT(*) FROM pragma_index_list('{table}') i
                WHERE i.name=$name
                  AND (SELECT group_concat(name,',') FROM pragma_index_info(i.name))=$columns
                """;
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$columns", columns);
            if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                throw new InvalidOperationException($"Required report index {name} is missing or has an unexpected definition.");
        }
    }

    private static bool HasTable(SqliteConnection connection, SqliteTransaction? transaction, string name)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
        command.Parameters.AddWithValue("$name", name);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }
}
