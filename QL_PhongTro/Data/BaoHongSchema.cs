using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class BaoHongSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        // A conflicting pre-existing module must be reviewed; never silently adopt it.
        cmd.CommandText = """
            CREATE TABLE bao_hong (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ma_bao_hong TEXT NOT NULL UNIQUE,
                nguoi_bao_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                khach_thue_id INTEGER NOT NULL REFERENCES khach_thue(id) ON DELETE RESTRICT,
                phong_id INTEGER NOT NULL REFERENCES phong_tro(id) ON DELETE RESTRICT,
                hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id) ON DELETE RESTRICT,
                loai_thiet_bi TEXT NOT NULL CHECK(length(trim(loai_thiet_bi)) BETWEEN 1 AND 100),
                mo_ta TEXT NOT NULL CHECK(length(trim(mo_ta)) BETWEEN 1 AND 5000),
                muc_do TEXT NOT NULL CHECK(muc_do IN ('THUONG','GAP')),
                trang_thai TEXT NOT NULL DEFAULT 'MOI' CHECK(trang_thai IN ('MOI','DANG_XU_LY','HOAN_THANH','TU_CHOI')),
                ngay_tao TEXT NOT NULL,
                khoa_chong_trung TEXT NOT NULL UNIQUE
            );
            CREATE INDEX ix_bao_hong_nguoi_ngay ON bao_hong(nguoi_bao_id,ngay_tao);
            CREATE INDEX ix_bao_hong_hop_dong ON bao_hong(hop_dong_id,trang_thai);
            CREATE TABLE bao_hong_so_ma (
                thang TEXT PRIMARY KEY CHECK(length(thang)=6),
                so_cuoi INTEGER NOT NULL CHECK(so_cuoi BETWEEN 1 AND 9999)
            );
            INSERT INTO app_schema_version(version,applied_at) VALUES(28,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        cmd.ExecuteNonQuery(); Validate(connection, tx); tx.Commit();
    }

    public static void Validate(SqliteConnection connection, SqliteTransaction? tx = null)
    {
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT COUNT(*) FROM pragma_foreign_key_list('bao_hong') WHERE on_delete='RESTRICT' AND
            (("table"='tai_khoan' AND "from"='nguoi_bao_id' AND "to"='id') OR
             ("table"='khach_thue' AND "from"='khach_thue_id' AND "to"='id') OR
             ("table"='phong_tro' AND "from"='phong_id' AND "to"='id') OR
             ("table"='hop_dong' AND "from"='hop_dong_id' AND "to"='id'))
            """;
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 4) throw new InvalidOperationException("Schema báo hỏng thiếu khóa ngoại RESTRICT.");
        foreach (var column in new[] { "ma_bao_hong", "khoa_chong_trung" })
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM pragma_index_list('bao_hong') i WHERE i."unique"=1 AND i.partial=0
                AND (SELECT group_concat(name,',') FROM pragma_index_info(i.name))=$column
                """;
            cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$column", column);
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) throw new InvalidOperationException("Schema báo hỏng thiếu unique: " + column);
        }
    }
}
