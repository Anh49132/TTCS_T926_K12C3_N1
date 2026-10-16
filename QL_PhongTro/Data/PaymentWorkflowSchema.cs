using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class PaymentWorkflowSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS bao_cao_thanh_toan (
                thanh_toan_id INTEGER NOT NULL PRIMARY KEY REFERENCES thanh_toan(id) ON DELETE RESTRICT,
                nguoi_gui_id INTEGER REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                gui_luc TEXT, ghi_chu TEXT CHECK(ghi_chu IS NULL OR length(ghi_chu)<=300),
                ten_tep TEXT CHECK(ten_tep IS NULL OR length(ten_tep)<=120),
                ly_do_tu_choi TEXT CHECK(ly_do_tu_choi IS NULL OR length(trim(ly_do_tu_choi)) BETWEEN 1 AND 500),
                nguoi_xu_ly_id INTEGER REFERENCES tai_khoan(id) ON DELETE RESTRICT, xu_ly_luc TEXT
            );
            CREATE TABLE IF NOT EXISTS thong_bao_thanh_toan (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                thanh_toan_id INTEGER NOT NULL REFERENCES thanh_toan(id) ON DELETE RESTRICT,
                nguoi_nhan_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                loai TEXT NOT NULL CHECK(loai IN ('BAO_MOI','DA_XAC_NHAN','TU_CHOI','DA_HUY')),
                tieu_de TEXT NOT NULL, noi_dung TEXT NOT NULL, duong_dan TEXT NOT NULL,
                ngay_tao TEXT NOT NULL, ngay_doc TEXT,
                UNIQUE(thanh_toan_id,nguoi_nhan_id,loai)
            );
            CREATE INDEX IF NOT EXISTS ix_thong_bao_thanh_toan_nguoi_doc ON thong_bao_thanh_toan(nguoi_nhan_id,ngay_doc,ngay_tao);
            CREATE TRIGGER IF NOT EXISTS bao_cao_no_delete BEFORE DELETE ON bao_cao_thanh_toan
            BEGIN SELECT RAISE(ABORT,'Payment report history is immutable'); END;
            CREATE TRIGGER IF NOT EXISTS bao_cao_original_immutable BEFORE UPDATE ON bao_cao_thanh_toan
            WHEN NEW.thanh_toan_id<>OLD.thanh_toan_id OR NEW.nguoi_gui_id IS NOT OLD.nguoi_gui_id OR NEW.gui_luc IS NOT OLD.gui_luc
                OR NEW.ghi_chu IS NOT OLD.ghi_chu OR NEW.ten_tep IS NOT OLD.ten_tep
                OR (OLD.xu_ly_luc IS NOT NULL AND (NEW.xu_ly_luc IS NOT OLD.xu_ly_luc OR NEW.nguoi_xu_ly_id IS NOT OLD.nguoi_xu_ly_id OR NEW.ly_do_tu_choi IS NOT OLD.ly_do_tu_choi))
            BEGIN SELECT RAISE(ABORT,'Submitted report contents are immutable'); END;
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA integrity_check";
        if (cmd.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Integrity check không đạt; hoàn tác v39.");
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var reader = cmd.ExecuteReader()) if (reader.Read()) throw new InvalidOperationException("Khóa ngoại không hợp lệ; hoàn tác v39.");
        cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(39,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery(); tx.Commit();
    }
}
