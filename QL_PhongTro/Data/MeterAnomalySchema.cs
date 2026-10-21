using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class MeterAnomalySchema
{
    public static void Upgrade(SqliteConnection connection, bool recordVersion = true)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "PRAGMA table_info(chi_so_dien_nuoc)";
        bool exists = false;
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
                if (reader.GetString(1) == "da_xac_nhan_bat_thuong")
                {
                    if (reader.GetString(2) != "INTEGER" || reader.GetInt32(3) != 1 || reader.GetValue(4).ToString() != "0")
                        throw new InvalidOperationException("Cột xác nhận bất thường không khớp v18.");
                    exists = true;
                }
        if (!exists)
        {
            cmd.CommandText = "ALTER TABLE chi_so_dien_nuoc ADD COLUMN da_xac_nhan_bat_thuong INTEGER NOT NULL DEFAULT 0 CHECK(da_xac_nhan_bat_thuong IN (0,1))";
            cmd.ExecuteNonQuery();
        }
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='chi_so_dien_nuoc'";
        var sql = cmd.ExecuteScalar()?.ToString() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(sql, @"CHECK\s*\(\s*da_xac_nhan_bat_thuong\s+IN\s*\(0,\s*1\)\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new InvalidOperationException("Thiếu CHECK xác nhận bất thường; không nhận schema lạ.");
        cmd.CommandText = "PRAGMA integrity_check";
        if (cmd.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Meter anomaly integrity check failed.");
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var reader = cmd.ExecuteReader()) if (reader.Read()) throw new InvalidOperationException("Meter anomaly FK check failed.");
        if (recordVersion) cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(18,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        if (recordVersion) cmd.ExecuteNonQuery(); tx.Commit();
    }
}
