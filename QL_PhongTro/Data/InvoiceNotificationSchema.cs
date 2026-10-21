using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

// Additive v21. No invoices, contracts, prices or existing notifications are rewritten.
public static class InvoiceNotificationSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='thong_bao'";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) {
            cmd.CommandText = """
                CREATE TABLE thong_bao (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    nguoi_nhan_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                    hoa_don_id INTEGER NOT NULL REFERENCES hoa_don(id) ON DELETE RESTRICT,
                    loai_thong_bao TEXT NOT NULL, tieu_de TEXT NOT NULL, noi_dung TEXT NOT NULL,
                    duong_dan TEXT NOT NULL, email_nhan TEXT NOT NULL, ngay_tao TEXT NOT NULL, ngay_doc TEXT,
                    trang_thai_email TEXT NOT NULL DEFAULT 'CHO_GUI', so_lan_gui INTEGER NOT NULL DEFAULT 0,
                    lan_gui_tiep_theo TEXT, khoa_xu_ly_den TEXT, ngay_gui_thanh_cong TEXT, loi_gan_nhat TEXT,
                    UNIQUE(hoa_don_id,nguoi_nhan_id,loai_thong_bao)
                );
                CREATE INDEX ix_thong_bao_nguoi_doc ON thong_bao(nguoi_nhan_id,ngay_doc);
                CREATE INDEX ix_thong_bao_email ON thong_bao(trang_thai_email,lan_gui_tiep_theo);
                """;
            cmd.ExecuteNonQuery();
        }
        // Existing tables must already be compatible. Never repair an unknown schema silently.
        cmd.CommandText = "SELECT id,nguoi_nhan_id,hoa_don_id,loai_thong_bao,tieu_de,noi_dung,duong_dan,email_nhan,ngay_tao,ngay_doc,trang_thai_email,so_lan_gui,lan_gui_tiep_theo,khoa_xu_ly_den,ngay_gui_thanh_cong,loi_gan_nhat FROM thong_bao LIMIT 0";
        using (var reader = cmd.ExecuteReader()) { }
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_index_list('thong_bao') i WHERE i.\"unique\"=1 AND (SELECT group_concat(name,',') FROM pragma_index_info(i.name))='hoa_don_id,nguoi_nhan_id,loai_thong_bao'";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) throw new InvalidOperationException("Bảng thông báo thiếu khóa chống trùng; cần rà soát schema hiện có.");
        cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(21,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery(); tx.Commit();
    }
}
