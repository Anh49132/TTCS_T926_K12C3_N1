using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class ChamDutHopDongSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx=connection.BeginTransaction();using var cmd=connection.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='cham_dut_hop_dong'";
        if(Convert.ToInt32(cmd.ExecuteScalar())==0)
        {
            cmd.CommandText="""
                ALTER TABLE thanh_ly_hop_dong ADD COLUMN tien_phat INTEGER NOT NULL DEFAULT 0 CHECK(typeof(tien_phat)='integer' AND tien_phat>=0);
                ALTER TABLE chi_tiet_khau_tru ADD COLUMN loai_khoan TEXT NOT NULL DEFAULT 'HU_HONG' CHECK(loai_khoan IN ('HU_HONG','PHAT_COC'));
                CREATE UNIQUE INDEX ux_khau_tru_phat_coc ON chi_tiet_khau_tru(thanh_ly_id) WHERE loai_khoan='PHAT_COC';
                CREATE TABLE cham_dut_hop_dong (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    hop_dong_id INTEGER NOT NULL UNIQUE REFERENCES hop_dong(id) ON DELETE RESTRICT,
                    hoa_don_cuoi_id INTEGER NOT NULL UNIQUE REFERENCES hoa_don(id) ON DELETE RESTRICT,
                    ngay_tra_phong TEXT NOT NULL, ngay_ket_thuc_doi_chieu TEXT NOT NULL,
                    coc_thoa_thuan_doi_chieu INTEGER NOT NULL CHECK(coc_thoa_thuan_doi_chieu>=0),
                    truoc_han INTEGER NOT NULL CHECK(truoc_han IN (0,1)),
                    ly_do TEXT,ly_do_khac TEXT CHECK(ly_do_khac IS NULL OR length(trim(ly_do_khac)) BETWEEN 1 AND 500),
                    ap_dung_phat INTEGER NOT NULL CHECK(ap_dung_phat IN (0,1)),
                    tien_phat INTEGER NOT NULL CHECK(typeof(tien_phat)='integer' AND tien_phat>=0),
                    phien_ban INTEGER NOT NULL CHECK(phien_ban>0),
                    nguoi_cap_nhat_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                    cap_nhat_luc TEXT NOT NULL,
                    CHECK(truoc_han=(ngay_tra_phong<ngay_ket_thuc_doi_chieu)),
                    CHECK((truoc_han=0 AND ly_do IS NULL AND ly_do_khac IS NULL AND ap_dung_phat=0) OR
                        (truoc_han=1 AND ly_do IS NOT NULL AND ly_do IN ('KHACH_CHUYEN_DI','THOA_THUAN','VI_PHAM','KHAC') AND
                        ((ly_do='KHAC' AND ly_do_khac IS NOT NULL) OR (ly_do<>'KHAC' AND ly_do_khac IS NULL)))),
                    CHECK((ap_dung_phat=0 AND tien_phat=0) OR
                        (ap_dung_phat=1 AND truoc_han=1 AND tien_phat>0 AND tien_phat<=coc_thoa_thuan_doi_chieu))
                );
                CREATE TRIGGER cham_dut_no_delete BEFORE DELETE ON cham_dut_hop_dong
                    BEGIN SELECT RAISE(ABORT,'Khong xoa thong tin cham dut'); END;
                CREATE TRIGGER cham_dut_insert BEFORE INSERT ON cham_dut_hop_dong WHEN
                    NOT EXISTS(SELECT 1 FROM hoa_don WHERE id=NEW.hoa_don_cuoi_id AND hop_dong_id=NEW.hop_dong_id AND loai_hoa_don='KY_CUOI' AND den_ngay=NEW.ngay_tra_phong AND trang_thai<>'DA_HUY') OR
                    EXISTS(SELECT 1 FROM thanh_ly_hop_dong WHERE hop_dong_id=NEW.hop_dong_id AND trang_thai='DA_CHOT')
                    BEGIN SELECT RAISE(ABORT,'Chi ghi cham dut cung hop dong ky cuoi chua chot'); END;
                CREATE TRIGGER cham_dut_chot_update BEFORE UPDATE ON cham_dut_hop_dong WHEN
                    NEW.hop_dong_id<>OLD.hop_dong_id OR NEW.hoa_don_cuoi_id<>OLD.hoa_don_cuoi_id OR
                    NOT EXISTS(SELECT 1 FROM hoa_don WHERE id=NEW.hoa_don_cuoi_id AND hop_dong_id=NEW.hop_dong_id AND loai_hoa_don='KY_CUOI' AND den_ngay=NEW.ngay_tra_phong AND trang_thai<>'DA_HUY') OR
                    EXISTS(SELECT 1 FROM thanh_ly_hop_dong WHERE hop_dong_id=OLD.hop_dong_id AND trang_thai='DA_CHOT')
                    BEGIN SELECT RAISE(ABORT,'Thong tin cham dut da chot bat bien'); END;
                """;
            cmd.ExecuteNonQuery();
        }
        Validate(connection,tx);
        cmd.CommandText="PRAGMA integrity_check";
        if(cmd.ExecuteScalar()?.ToString()!="ok")throw new InvalidOperationException("Integrity check v32 thất bại.");
        cmd.CommandText="PRAGMA foreign_key_check";
        using(var reader=cmd.ExecuteReader())if(reader.Read())throw new InvalidOperationException("Foreign key check v32 thất bại.");
        cmd.CommandText="INSERT INTO app_schema_version(version,applied_at) VALUES(32,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery();tx.Commit();
    }
    public static void Validate(SqliteConnection connection,SqliteTransaction? tx=null)
    {
        using var cmd=connection.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="SELECT id,hop_dong_id,hoa_don_cuoi_id,ngay_tra_phong,ngay_ket_thuc_doi_chieu,coc_thoa_thuan_doi_chieu,truoc_han,ly_do,ly_do_khac,ap_dung_phat,tien_phat,phien_ban,nguoi_cap_nhat_id,cap_nhat_luc FROM cham_dut_hop_dong LIMIT 0";
        using(var reader=cmd.ExecuteReader()){}
        cmd.CommandText="SELECT tien_phat FROM thanh_ly_hop_dong LIMIT 0";using(var reader=cmd.ExecuteReader()){}
        cmd.CommandText="SELECT loai_khoan FROM chi_tiet_khau_tru LIMIT 0";using(var reader=cmd.ExecuteReader()){}
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE (type='index' AND name='ux_khau_tru_phat_coc') OR (type='trigger' AND name IN ('cham_dut_no_delete','cham_dut_chot_update','cham_dut_insert'))";
        if(Convert.ToInt32(cmd.ExecuteScalar())!=4)throw new InvalidOperationException("Schema v32 thiếu bảo vệ khoản phạt/chấm dứt.");
        cmd.CommandText="SELECT COUNT(*) FROM pragma_foreign_key_list('cham_dut_hop_dong') WHERE on_delete='RESTRICT'";
        if(Convert.ToInt32(cmd.ExecuteScalar())!=3)throw new InvalidOperationException("Schema v32 thiếu FK RESTRICT.");
    }
}
