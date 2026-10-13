using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class BaoHongTraoDoiSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            ALTER TABLE bao_hong ADD COLUMN phien_ban INTEGER NOT NULL DEFAULT 0 CHECK(phien_ban>=0);
            CREATE TABLE bao_hong_trao_doi (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                bao_hong_id INTEGER NOT NULL REFERENCES bao_hong(id) ON DELETE RESTRICT,
                nguoi_gui_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ten_nguoi_gui TEXT NOT NULL,
                loai TEXT NOT NULL CHECK(loai IN ('PHAN_HOI','TRANG_THAI')),
                noi_dung TEXT NOT NULL CHECK(length(trim(noi_dung)) BETWEEN 1 AND 5000),
                trang_thai_truoc TEXT,
                trang_thai_sau TEXT,
                thoi_diem TEXT NOT NULL,
                khoa_chong_trung TEXT NOT NULL UNIQUE CHECK(length(khoa_chong_trung)=32),
                CHECK((loai='PHAN_HOI' AND trang_thai_truoc IS NULL AND trang_thai_sau IS NULL) OR
                    (loai='TRANG_THAI' AND trang_thai_truoc IS NOT NULL AND trang_thai_sau IS NOT NULL AND
                        trang_thai_truoc IN ('MOI','DANG_XU_LY','HOAN_THANH','TU_CHOI') AND
                        trang_thai_sau IN ('MOI','DANG_XU_LY','HOAN_THANH','TU_CHOI') AND trang_thai_truoc<>trang_thai_sau))
            );
            CREATE INDEX ix_bao_hong_trao_doi_ngay ON bao_hong_trao_doi(bao_hong_id,thoi_diem,id);
            CREATE TRIGGER bao_hong_trao_doi_no_update BEFORE UPDATE ON bao_hong_trao_doi
                BEGIN SELECT RAISE(ABORT,'Lich su bao hong chi duoc them'); END;
            CREATE TRIGGER bao_hong_trao_doi_no_delete BEFORE DELETE ON bao_hong_trao_doi
                BEGIN SELECT RAISE(ABORT,'Lich su bao hong chi duoc them'); END;
            INSERT INTO app_schema_version(version,applied_at) VALUES(30,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        cmd.ExecuteNonQuery(); Validate(connection, tx); tx.Commit();
    }

    public static void Validate(SqliteConnection connection, SqliteTransaction? tx = null)
    {
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT COUNT(*) FROM pragma_foreign_key_list('bao_hong_trao_doi') WHERE on_delete='RESTRICT' AND
            (("table"='bao_hong' AND "from"='bao_hong_id' AND "to"='id') OR
             ("table"='tai_khoan' AND "from"='nguoi_gui_id' AND "to"='id'))
            """;
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 2) throw new InvalidOperationException("Lịch sử báo hỏng thiếu khóa ngoại RESTRICT.");
        cmd.CommandText = """
            SELECT COUNT(*) FROM pragma_index_list('bao_hong_trao_doi') i WHERE i."unique"=1 AND i.partial=0
            AND (SELECT group_concat(name,',') FROM pragma_index_info(i.name))='khoa_chong_trung'
            """;
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) throw new InvalidOperationException("Lịch sử báo hỏng thiếu khóa chống trùng unique.");
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND tbl_name='bao_hong_trao_doi' AND name IN ('bao_hong_trao_doi_no_update','bao_hong_trao_doi_no_delete')";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 2) throw new InvalidOperationException("Lịch sử báo hỏng thiếu trigger bất biến.");
    }
}
