using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class XacNhanTatToanSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx=connection.BeginTransaction();using var cmd=connection.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('xac_nhan_tat_toan','lich_su_trang_thai_phong')";
        var existing=Convert.ToInt32(cmd.ExecuteScalar());
        if(existing==1)throw new InvalidOperationException("Schema xác nhận/lịch sử phòng không đầy đủ; cần đối chiếu thủ công.");
        if(existing==0)
        {
            cmd.CommandText="""
                CREATE TABLE xac_nhan_tat_toan (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    thanh_ly_id INTEGER NOT NULL UNIQUE REFERENCES thanh_ly_hop_dong(id) ON DELETE RESTRICT,
                    hop_dong_id INTEGER NOT NULL UNIQUE REFERENCES hop_dong(id) ON DELETE RESTRICT,
                    nguoi_xac_nhan_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                    ten_nguoi_xac_nhan TEXT NOT NULL, thoi_diem TEXT NOT NULL,
                    so_tien_tat_toan INTEGER NOT NULL CHECK(typeof(so_tien_tat_toan)='integer'),
                    tien_can_tru INTEGER NOT NULL CHECK(typeof(tien_can_tru)='integer' AND tien_can_tru>=0)
                );
                CREATE TRIGGER xac_nhan_no_update BEFORE UPDATE ON xac_nhan_tat_toan BEGIN SELECT RAISE(ABORT,'Xac nhan bat bien'); END;
                CREATE TRIGGER xac_nhan_no_delete BEFORE DELETE ON xac_nhan_tat_toan BEGIN SELECT RAISE(ABORT,'Xac nhan bat bien'); END;
                CREATE TRIGGER xac_nhan_doi_chieu BEFORE INSERT ON xac_nhan_tat_toan WHEN NOT EXISTS (
                    SELECT 1 FROM thanh_ly_hop_dong b JOIN hop_dong h ON h.id=b.hop_dong_id
                    JOIN phong_tro p ON p.id=h.phong_id JOIN toa_nha t ON t.id=p.toa_nha_id
                    JOIN tai_khoan a ON a.id=NEW.nguoi_xac_nhan_id
                    WHERE b.id=NEW.thanh_ly_id AND b.hop_dong_id=NEW.hop_dong_id AND b.trang_thai='DA_CHOT'
                    AND h.trang_thai='DA_KET_THUC' AND a.vai_tro='CHU_NHA' AND a.dang_hoat_dong=1 AND a.is_deleted=0
                    AND t.chu_nha_id=a.id AND NEW.so_tien_tat_toan=b.tien_phai_hoan-b.tien_khach_tra_them)
                    BEGIN SELECT RAISE(ABORT,'Xac nhan khong khop bang tat toan'); END;
                CREATE TABLE lich_su_trang_thai_phong (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    phong_id INTEGER NOT NULL REFERENCES phong_tro(id) ON DELETE RESTRICT,
                    hop_dong_id INTEGER REFERENCES hop_dong(id) ON DELETE RESTRICT,
                    trang_thai TEXT NOT NULL CHECK(trang_thai IN ('TRONG','DA_DAT_COC','DANG_THUE','NGUNG_CHO_THUE')),
                    tu_thoi_diem TEXT NOT NULL, den_thoi_diem TEXT CHECK(den_thoi_diem IS NULL OR den_thoi_diem>tu_thoi_diem),
                    UNIQUE(phong_id,tu_thoi_diem)
                );
                CREATE UNIQUE INDEX ux_lich_su_phong_hien_tai ON lich_su_trang_thai_phong(phong_id) WHERE den_thoi_diem IS NULL;
                CREATE TRIGGER lich_su_phong_no_delete BEFORE DELETE ON lich_su_trang_thai_phong BEGIN SELECT RAISE(ABORT,'Khong xoa lich su phong'); END;
                CREATE TRIGGER lich_su_phong_close_only BEFORE UPDATE ON lich_su_trang_thai_phong WHEN
                    OLD.den_thoi_diem IS NOT NULL OR NEW.den_thoi_diem IS NULL OR NEW.phong_id<>OLD.phong_id OR
                    NEW.hop_dong_id IS NOT OLD.hop_dong_id OR NEW.trang_thai<>OLD.trang_thai OR NEW.tu_thoi_diem<>OLD.tu_thoi_diem
                    BEGIN SELECT RAISE(ABORT,'Chi dong khoang lich su phong'); END;
                """;
            cmd.ExecuteNonQuery();
        }
        EnsureInvoiceGuards(connection,tx);Validate(connection,tx);
        cmd.CommandText="PRAGMA integrity_check";if(cmd.ExecuteScalar()?.ToString()!="ok")throw new InvalidOperationException("Integrity v33 thất bại.");
        cmd.CommandText="PRAGMA foreign_key_check";using(var reader=cmd.ExecuteReader())if(reader.Read())throw new InvalidOperationException("FK v33 thất bại.");
        cmd.CommandText="INSERT INTO app_schema_version(version,applied_at) VALUES(33,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";cmd.ExecuteNonQuery();tx.Commit();
    }
    public static void EnsureInvoiceGuards(SqliteConnection connection,SqliteTransaction tx)
    {
        using var cmd=connection.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='hoa_don'";if(Convert.ToInt32(cmd.ExecuteScalar())==0)return;
        cmd.CommandText="""
            CREATE TRIGGER IF NOT EXISTS hop_dong_ket_thuc_no_invoice BEFORE INSERT ON hoa_don
                WHEN (SELECT trang_thai FROM hop_dong WHERE id=NEW.hop_dong_id)='DA_KET_THUC'
                BEGIN SELECT RAISE(ABORT,'Hop dong da ket thuc khong tao hoa don moi'); END;
            CREATE TRIGGER IF NOT EXISTS hop_dong_ket_thuc_no_publish BEFORE UPDATE ON hoa_don
                WHEN (SELECT trang_thai FROM hop_dong WHERE id=NEW.hop_dong_id)='DA_KET_THUC'
                AND NEW.trang_thai='DA_PHAT_HANH' AND OLD.trang_thai<>'DA_PHAT_HANH'
                BEGIN SELECT RAISE(ABORT,'Hop dong da ket thuc khong phat hanh hoa don moi'); END;
            CREATE TRIGGER IF NOT EXISTS tat_toan_no_cancel_invoice BEFORE UPDATE ON hoa_don
                WHEN NEW.trang_thai='DA_HUY' AND OLD.trang_thai<>'DA_HUY' AND
                EXISTS(SELECT 1 FROM thanh_ly_hop_dong WHERE hop_dong_id=OLD.hop_dong_id AND trang_thai='DA_CHOT')
                BEGIN SELECT RAISE(ABORT,'Hoa don thuoc bang tat toan da chot'); END;
            """;cmd.ExecuteNonQuery();
    }
    public static void Validate(SqliteConnection connection,SqliteTransaction? tx=null)
    {
        using var cmd=connection.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="SELECT id,thanh_ly_id,hop_dong_id,nguoi_xac_nhan_id,ten_nguoi_xac_nhan,thoi_diem,so_tien_tat_toan,tien_can_tru FROM xac_nhan_tat_toan LIMIT 0";using(var reader=cmd.ExecuteReader()){}
        cmd.CommandText="SELECT id,phong_id,hop_dong_id,trang_thai,tu_thoi_diem,den_thoi_diem FROM lich_su_trang_thai_phong LIMIT 0";using(var reader=cmd.ExecuteReader()){}
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name IN ('xac_nhan_no_update','xac_nhan_no_delete','xac_nhan_doi_chieu','lich_su_phong_no_delete','lich_su_phong_close_only')";
        if(Convert.ToInt32(cmd.ExecuteScalar())!=5)throw new InvalidOperationException("Schema v33 thiếu trigger xác nhận/lịch sử.");
        foreach(var (table,count) in new[] {("xac_nhan_tat_toan",3),("lich_su_trang_thai_phong",2)})
        {
            cmd.CommandText=$"SELECT COUNT(*) FROM pragma_foreign_key_list('{table}') WHERE on_delete='RESTRICT'";
            if(Convert.ToInt32(cmd.ExecuteScalar())!=count)throw new InvalidOperationException("Schema v33 thiếu FK RESTRICT.");
        }
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='hoa_don'";
        if(Convert.ToInt32(cmd.ExecuteScalar())==0)return;
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name IN ('hop_dong_ket_thuc_no_invoice','hop_dong_ket_thuc_no_publish','tat_toan_no_cancel_invoice')";
        if(Convert.ToInt32(cmd.ExecuteScalar())!=3)throw new InvalidOperationException("Schema v33 thiếu khóa hóa đơn sau kết thúc.");
    }
}
