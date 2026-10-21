using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;
public static class RepairEmailSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx=connection.BeginTransaction();using var cmd=connection.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="""
            CREATE TABLE IF NOT EXISTS email_bao_hong(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                bao_hong_id INTEGER NOT NULL REFERENCES bao_hong(id) ON DELETE RESTRICT,
                su_kien TEXT NOT NULL,
                nguoi_nhan_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                vai_tro_nhan TEXT NOT NULL CHECK(vai_tro_nhan IN ('CHU_NHA','QUAN_LY','KHACH_THUE')),
                email_nhan TEXT NOT NULL,tieu_de TEXT NOT NULL,noi_dung TEXT NOT NULL,duong_dan TEXT NOT NULL,
                trang_thai TEXT NOT NULL DEFAULT 'CHO_GUI' CHECK(trang_thai IN ('CHO_GUI','DANG_GUI','DA_GUI','THAT_BAI','DA_HUY')),
                so_lan_gui INTEGER NOT NULL DEFAULT 0 CHECK(so_lan_gui BETWEEN 0 AND 4),
                ngay_tao TEXT NOT NULL,lan_gui_tiep TEXT,khoa_xu_ly TEXT,khoa_den TEXT,ngay_gui TEXT,loi TEXT,
                UNIQUE(su_kien,nguoi_nhan_id)
            );
            CREATE INDEX IF NOT EXISTS ix_email_bao_hong_pending ON email_bao_hong(trang_thai,lan_gui_tiep,khoa_den);
            """;
        cmd.ExecuteNonQuery();cmd.CommandText="PRAGMA integrity_check";
        if(cmd.ExecuteScalar()?.ToString()!="ok")throw new InvalidOperationException("Integrity check không đạt; hoàn tác v41.");
        cmd.CommandText="PRAGMA foreign_key_check";using(var r=cmd.ExecuteReader())if(r.Read())throw new InvalidOperationException("FK check không đạt; hoàn tác v41.");
        cmd.CommandText="INSERT INTO app_schema_version(version,applied_at) VALUES(41,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery();tx.Commit();
    }
}
