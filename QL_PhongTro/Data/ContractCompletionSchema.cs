using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class ContractCompletionSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS hop_dong_chi_so_dau_ky (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                hop_dong_id INTEGER NOT NULL UNIQUE REFERENCES hop_dong(id) ON DELETE RESTRICT,
                ngay_ban_giao TEXT NOT NULL,
                chi_so_dien TEXT NOT NULL CHECK(CAST(chi_so_dien AS REAL) >= 0),
                chi_so_nuoc TEXT NOT NULL CHECK(CAST(chi_so_nuoc AS REAL) >= 0),
                nguoi_nhap_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ngay_nhap TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS hop_dong_so_ma (
                nam INTEGER PRIMARY KEY CHECK(nam BETWEEN 1 AND 9999),
                so_cuoi INTEGER NOT NULL CHECK(so_cuoi BETWEEN 0 AND 9999));
            INSERT INTO app_schema_version(version, applied_at) VALUES(15,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
