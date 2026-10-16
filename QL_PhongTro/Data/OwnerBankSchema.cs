using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class OwnerBankSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tai_khoan_ngan_hang (
                chu_nha_id INTEGER NOT NULL PRIMARY KEY REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ten_ngan_hang TEXT NOT NULL CHECK(length(trim(ten_ngan_hang)) BETWEEN 1 AND 120),
                so_tai_khoan TEXT NOT NULL CHECK(length(trim(so_tai_khoan)) BETWEEN 1 AND 50),
                qr_key TEXT, qr_content_type TEXT,
                ngay_tao TEXT NOT NULL, ngay_cap_nhat TEXT NOT NULL, phien_ban TEXT NOT NULL,
                CHECK((qr_key IS NULL AND qr_content_type IS NULL) OR
                      (qr_key IS NOT NULL AND length(qr_key)=32 AND qr_content_type IN ('image/jpeg','image/png')))
            );
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var reader = cmd.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("Khóa ngoại không hợp lệ; đã hoàn tác v38.");
        cmd.CommandText = "PRAGMA integrity_check";
        if (cmd.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Integrity check không đạt; đã hoàn tác v38.");
        cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(38,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery();
        tx.Commit();
    }
}
