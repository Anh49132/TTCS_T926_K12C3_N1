using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class ContractSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS hop_dong (
                id INTEGER PRIMARY KEY AUTOINCREMENT, ma_hop_dong TEXT NOT NULL UNIQUE,
                phong_id INTEGER NOT NULL REFERENCES phong_tro(id) ON DELETE RESTRICT,
                trang_thai TEXT NOT NULL DEFAULT 'NHAP', ngay_tra_phong TEXT);
            CREATE TABLE IF NOT EXISTS ky_hop_dong (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id) ON DELETE RESTRICT,
                ngay_bat_dau TEXT NOT NULL, ngay_ket_thuc TEXT NOT NULL,
                gia_thue INTEGER NOT NULL CHECK(gia_thue > 0));
            """;
        command.ExecuteNonQuery();
        Add("hop_dong", "yeu_cau_thue_id", "INTEGER REFERENCES yeu_cau_thue(id) ON DELETE RESTRICT");
        Add("hop_dong", "khach_dung_ten_id", "INTEGER REFERENCES khach_thue(id) ON DELETE RESTRICT");
        Add("hop_dong", "tien_coc_thoa_thuan", "INTEGER NOT NULL DEFAULT 0");
        Add("hop_dong", "ngay_chot_hang_thang", "INTEGER NOT NULL DEFAULT 1");
        Add("hop_dong", "nguoi_lap_id", "INTEGER REFERENCES tai_khoan(id) ON DELETE RESTRICT");
        Add("hop_dong", "ngay_tao", "TEXT NOT NULL DEFAULT '2000-01-01T00:00:00'");
        Add("ky_hop_dong", "so_thu_tu", "INTEGER NOT NULL DEFAULT 1");
        Add("ky_hop_dong", "so_thang", "INTEGER NOT NULL DEFAULT 1");
        Add("ky_hop_dong", "nguoi_lap_id", "INTEGER REFERENCES tai_khoan(id) ON DELETE RESTRICT");
        Add("ky_hop_dong", "ngay_tao", "TEXT NOT NULL DEFAULT '2000-01-01T00:00:00'");
        command.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS ux_hop_dong_yeu_cau ON hop_dong(yeu_cau_thue_id) WHERE yeu_cau_thue_id IS NOT NULL;
            INSERT INTO app_schema_version(version, applied_at) VALUES(14,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        command.ExecuteNonQuery();
        transaction.Commit();

        void Add(string table, string column, string definition)
        {
            command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
            if (Convert.ToInt64(command.ExecuteScalar()) != 0) return;
            command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            command.ExecuteNonQuery();
        }
    }
}
