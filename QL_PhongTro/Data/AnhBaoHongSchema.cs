using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class AnhBaoHongSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE anh_bao_hong (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                bao_hong_id INTEGER NOT NULL REFERENCES bao_hong(id) ON DELETE RESTRICT,
                thu_tu INTEGER NOT NULL CHECK(thu_tu BETWEEN 1 AND 3),
                khoa_luu TEXT NOT NULL UNIQUE CHECK(length(khoa_luu)=32),
                content_type TEXT NOT NULL CHECK(content_type IN ('image/jpeg','image/png')),
                so_byte INTEGER NOT NULL CHECK(so_byte BETWEEN 1 AND 5242880),
                sha256 TEXT NOT NULL CHECK(length(sha256)=64),
                UNIQUE(bao_hong_id,thu_tu)
            );
            INSERT INTO app_schema_version(version,applied_at) VALUES(29,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        cmd.ExecuteNonQuery(); Validate(connection, tx); tx.Commit();
    }
    public static void Validate(SqliteConnection connection, SqliteTransaction? tx = null)
    {
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT COUNT(*) FROM pragma_foreign_key_list('anh_bao_hong')
            WHERE "table"='bao_hong' AND "from"='bao_hong_id' AND "to"='id' AND on_delete='RESTRICT'
            """;
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 1) throw new InvalidOperationException("Ảnh báo hỏng thiếu khóa ngoại RESTRICT.");
        foreach (var columns in new[] { "khoa_luu", "bao_hong_id,thu_tu" })
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM pragma_index_list('anh_bao_hong') i WHERE i."unique"=1 AND i.partial=0
                AND (SELECT group_concat(name,',') FROM pragma_index_info(i.name))=$columns
                """;
            cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$columns", columns);
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) throw new InvalidOperationException("Ảnh báo hỏng thiếu unique: " + columns);
        }
    }
}
