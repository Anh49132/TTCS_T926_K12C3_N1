using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class CheckoutRequestSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction(); using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS yeu_cau_tra_phong(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id) ON DELETE RESTRICT,
                nguoi_gui_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ngay_de_nghi TEXT NOT NULL, ly_do TEXT NOT NULL CHECK(length(trim(ly_do)) BETWEEN 1 AND 500),
                trang_thai TEXT NOT NULL CHECK(trang_thai IN ('MOI','DA_TIEP_NHAN','TU_CHOI','DA_HUY','HOAN_TAT')),
                ngay_ban_giao TEXT, phan_hoi TEXT CHECK(phan_hoi IS NULL OR length(phan_hoi)<=500),
                nguoi_xu_ly_id INTEGER REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ngay_tao TEXT NOT NULL, cap_nhat_luc TEXT NOT NULL, phien_ban INTEGER NOT NULL DEFAULT 0,
                CHECK(trang_thai<>'DA_TIEP_NHAN' OR (ngay_ban_giao IS NOT NULL AND nguoi_xu_ly_id IS NOT NULL)),
                CHECK(trang_thai<>'TU_CHOI' OR length(trim(COALESCE(phan_hoi,'')))>0)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_tra_phong_dang_mo ON yeu_cau_tra_phong(hop_dong_id) WHERE trang_thai IN ('MOI','DA_TIEP_NHAN');
            CREATE INDEX IF NOT EXISTS ix_tra_phong_nguoi_gui ON yeu_cau_tra_phong(nguoi_gui_id,ngay_tao);
            CREATE TRIGGER IF NOT EXISTS tra_phong_no_delete BEFORE DELETE ON yeu_cau_tra_phong
            BEGIN SELECT RAISE(ABORT,'Checkout request history cannot be deleted'); END;
            CREATE TRIGGER IF NOT EXISTS tra_phong_original_immutable BEFORE UPDATE ON yeu_cau_tra_phong
            WHEN NEW.hop_dong_id<>OLD.hop_dong_id OR NEW.nguoi_gui_id<>OLD.nguoi_gui_id OR NEW.ngay_de_nghi<>OLD.ngay_de_nghi
              OR NEW.ly_do<>OLD.ly_do OR NEW.ngay_tao<>OLD.ngay_tao OR OLD.trang_thai IN ('TU_CHOI','DA_HUY','HOAN_TAT')
              OR NEW.phien_ban<>OLD.phien_ban+1
              OR NOT ((OLD.trang_thai='MOI' AND NEW.trang_thai IN ('DA_TIEP_NHAN','TU_CHOI','DA_HUY','HOAN_TAT'))
                  OR (OLD.trang_thai='DA_TIEP_NHAN' AND NEW.trang_thai IN ('TU_CHOI','HOAN_TAT')))
            BEGIN SELECT RAISE(ABORT,'Invalid checkout request transition'); END;
            CREATE TABLE IF NOT EXISTS thong_bao_tra_phong(
                id INTEGER PRIMARY KEY AUTOINCREMENT, yeu_cau_id INTEGER NOT NULL REFERENCES yeu_cau_tra_phong(id) ON DELETE RESTRICT,
                nguoi_nhan_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT, loai TEXT NOT NULL,
                tieu_de TEXT NOT NULL, noi_dung TEXT NOT NULL, duong_dan TEXT NOT NULL, ngay_tao TEXT NOT NULL, ngay_doc TEXT,
                UNIQUE(yeu_cau_id,nguoi_nhan_id,loai)
            );
            CREATE INDEX IF NOT EXISTS ix_tb_tra_phong_nguoi_doc ON thong_bao_tra_phong(nguoi_nhan_id,ngay_doc);
            """;
        cmd.ExecuteNonQuery(); cmd.CommandText = "PRAGMA integrity_check";
        if (cmd.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Integrity check không đạt; hoàn tác v40.");
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var r = cmd.ExecuteReader()) if (r.Read()) throw new InvalidOperationException("FK check không đạt; hoàn tác v40.");
        cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(40,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery(); tx.Commit();
    }
}
