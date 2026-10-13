using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class InvoiceCollectionSchema
{
    public static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='thanh_toan'";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) return;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS lan_thu_hoa_don (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                thanh_toan_id INTEGER NOT NULL UNIQUE REFERENCES thanh_toan(id) ON DELETE RESTRICT,
                hinh_thuc TEXT NOT NULL CHECK(hinh_thuc IN ('TIEN_MAT','CHUYEN_KHOAN','KHAC')),
                ghi_chu TEXT CHECK(ghi_chu IS NULL OR length(ghi_chu)<=255),
                nguoi_xac_nhan_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ten_nguoi_xac_nhan TEXT NOT NULL,
                thoi_diem_ghi TEXT NOT NULL,
                khoa_chong_trung TEXT NOT NULL UNIQUE
            );
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT id,thanh_toan_id,hinh_thuc,ghi_chu,nguoi_xac_nhan_id,ten_nguoi_xac_nhan,thoi_diem_ghi,khoa_chong_trung FROM lan_thu_hoa_don LIMIT 0";
        using (cmd.ExecuteReader()) { }
        Validate(connection, transaction);
    }

    public static void Validate(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var cmd = connection.CreateCommand(); cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT COUNT(*) FROM pragma_foreign_key_list('lan_thu_hoa_don')
            WHERE on_delete='RESTRICT' AND (("table"='thanh_toan' AND "from"='thanh_toan_id' AND "to"='id')
                OR ("table"='tai_khoan' AND "from"='nguoi_xac_nhan_id' AND "to"='id'))
            """;
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 2) throw new InvalidOperationException("Bảng lần thu thiếu khóa ngoại RESTRICT; cần rà soát schema.");
        foreach (var column in new[] { "thanh_toan_id", "khoa_chong_trung" })
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM pragma_index_list('lan_thu_hoa_don') i
                WHERE i."unique"=1 AND i.partial=0 AND
                    (SELECT group_concat(name,',') FROM pragma_index_info(i.name))=$column
                """;
            cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$column", column);
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) throw new InvalidOperationException("Bảng lần thu thiếu khóa unique: " + column);
        }
    }

    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        Ensure(connection, transaction);
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(26,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery();
        transaction.Commit();
    }
}
