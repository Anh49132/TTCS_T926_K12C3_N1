using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class EmailGuiLogSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='email_gui_log'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
        {
            command.CommandText = """
                CREATE TABLE email_gui_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    hoa_don_id INTEGER NOT NULL REFERENCES hoa_don(id) ON DELETE RESTRICT,
                    loai_thong_bao TEXT NOT NULL CHECK(length(loai_thong_bao) BETWEEN 1 AND 40),
                    nguoi_nhan_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                    email_nguoi_nhan TEXT NOT NULL CHECK(length(email_nguoi_nhan) BETWEEN 1 AND 254),
                    trang_thai TEXT NOT NULL CHECK(length(trang_thai) BETWEEN 1 AND 20),
                    so_lan_thu INTEGER NOT NULL DEFAULT 0 CHECK(so_lan_thu >= 0),
                    loi_gan_nhat TEXT,
                    ngay_gui TEXT NOT NULL,
                    ngay_thu_tiep TEXT,
                    UNIQUE(hoa_don_id, nguoi_nhan_id, loai_thong_bao)
                );
                CREATE INDEX ix_email_gui_log_trang_thai_ngay_thu
                    ON email_gui_log(trang_thai, ngay_thu_tiep);
                """;
            command.ExecuteNonQuery();
        }

        Validate(connection, transaction);
        command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(36,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        command.ExecuteNonQuery();
        transaction.Commit();
        Validate(connection);
    }

    public static void Validate(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id,hoa_don_id,loai_thong_bao,nguoi_nhan_id,email_nguoi_nhan,trang_thai,so_lan_thu,loi_gan_nhat,ngay_gui,ngay_thu_tiep FROM email_gui_log LIMIT 0";
        using (command.ExecuteReader()) { }

        command.CommandText = """
            SELECT COUNT(*) FROM pragma_index_list('email_gui_log') i
            WHERE i."unique"=1
              AND (SELECT group_concat(name, ',') FROM pragma_index_info(i.name))
                  ='hoa_don_id,nguoi_nhan_id,loai_thong_bao'
            """;
        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
            throw new InvalidOperationException("email_gui_log is missing its duplicate-prevention index.");

        command.CommandText = """
            SELECT COUNT(*) FROM pragma_index_list('email_gui_log') i
            WHERE i.name='ix_email_gui_log_trang_thai_ngay_thu'
              AND (SELECT group_concat(name, ',') FROM pragma_index_info(i.name))='trang_thai,ngay_thu_tiep'
            """;
        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
            throw new InvalidOperationException("email_gui_log retry index is missing or has an unexpected definition.");
    }
}
