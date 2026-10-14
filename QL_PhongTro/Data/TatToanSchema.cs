using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class TatToanSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        // Compatible branch schemas are validated without rewriting their tables or rows.
        // A partial/unknown schema fails closed and requires manual comparison.
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('giao_dich_coc','thanh_ly_hop_dong','chi_tiet_khau_tru')";
        var existing=Convert.ToInt32(cmd.ExecuteScalar());
        if(existing!=0)
        {
            if(existing!=3)throw new InvalidOperationException("Đã có schema cọc/tất toán không đầy đủ từ nhánh khác; cần đối chiếu thủ công trước khi nâng v31.");
            Validate(connection,tx);
            cmd.CommandText="INSERT INTO app_schema_version(version,applied_at) VALUES(31,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";cmd.ExecuteNonQuery();tx.Commit();return;
        }
        cmd.CommandText = """
            CREATE TABLE giao_dich_coc (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ma_giao_dich_coc TEXT NOT NULL UNIQUE,
                khoa_chong_trung TEXT NOT NULL UNIQUE,
                hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id) ON DELETE RESTRICT,
                hoa_don_id INTEGER REFERENCES hoa_don(id) ON DELETE RESTRICT,
                loai_giao_dich TEXT NOT NULL CHECK(loai_giao_dich IN ('THU_COC','HOAN_COC','CAN_TRU')),
                so_tien INTEGER NOT NULL CHECK(typeof(so_tien)='integer' AND so_tien>0),
                hinh_thuc TEXT,
                trang_thai TEXT NOT NULL CHECK(trang_thai IN ('DA_XAC_NHAN','DA_HUY')),
                nguoi_thuc_hien_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ngay_giao_dich TEXT NOT NULL,
                CHECK((loai_giao_dich='CAN_TRU' AND hoa_don_id IS NOT NULL AND hinh_thuc IS NULL) OR
                    (loai_giao_dich IN ('THU_COC','HOAN_COC') AND hoa_don_id IS NULL AND hinh_thuc IN ('TIEN_MAT','CHUYEN_KHOAN')))
            );
            CREATE INDEX ix_coc_hop_dong ON giao_dich_coc(hop_dong_id,trang_thai);
            CREATE INDEX ix_coc_hoa_don ON giao_dich_coc(hoa_don_id,trang_thai);
            CREATE TABLE thanh_ly_hop_dong (
                id INTEGER PRIMARY KEY AUTOINCREMENT, ma_tat_toan TEXT NOT NULL UNIQUE,
                hop_dong_id INTEGER NOT NULL UNIQUE REFERENCES hop_dong(id) ON DELETE RESTRICT,
                hoa_don_cuoi_id INTEGER NOT NULL REFERENCES hoa_don(id) ON DELETE RESTRICT,
                ngay_tra_phong TEXT NOT NULL,
                coc_con_giu_truoc_chot INTEGER NOT NULL CHECK(coc_con_giu_truoc_chot>=0),
                no_hoa_don_cuoi INTEGER NOT NULL CHECK(no_hoa_don_cuoi>=0),
                cong_no_cu INTEGER NOT NULL CHECK(cong_no_cu>=0),
                tien_hu_hong INTEGER NOT NULL CHECK(tien_hu_hong>=0),
                tien_phai_hoan INTEGER NOT NULL CHECK(tien_phai_hoan>=0),
                tien_khach_tra_them INTEGER NOT NULL CHECK(tien_khach_tra_them>=0),
                chi_tiet_chot TEXT NOT NULL, dau_van_tay_nguon TEXT NOT NULL,
                ghi_chu TEXT CHECK(ghi_chu IS NULL OR length(ghi_chu)<=1000),
                trang_thai TEXT NOT NULL DEFAULT 'NHAP' CHECK(trang_thai IN ('NHAP','DA_CHOT')),
                phien_ban INTEGER NOT NULL DEFAULT 0 CHECK(phien_ban>=0),
                nguoi_lap_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ngay_tao TEXT NOT NULL, cap_nhat_luc TEXT NOT NULL,
                CHECK(NOT(tien_phai_hoan>0 AND tien_khach_tra_them>0))
            );
            CREATE TABLE chi_tiet_khau_tru (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                thanh_ly_id INTEGER NOT NULL REFERENCES thanh_ly_hop_dong(id) ON DELETE RESTRICT,
                so_thu_tu INTEGER NOT NULL CHECK(so_thu_tu>0),
                ten_khoan TEXT NOT NULL CHECK(length(trim(ten_khoan)) BETWEEN 1 AND 200),
                so_tien INTEGER NOT NULL CHECK(typeof(so_tien)='integer' AND so_tien>=0),
                ghi_chu TEXT CHECK(ghi_chu IS NULL OR length(ghi_chu)<=500),
                UNIQUE(thanh_ly_id,so_thu_tu)
            );
            CREATE TRIGGER tat_toan_no_delete BEFORE DELETE ON thanh_ly_hop_dong
                BEGIN SELECT RAISE(ABORT,'Khong xoa bang tat toan'); END;
            CREATE TRIGGER tat_toan_da_chot BEFORE UPDATE ON thanh_ly_hop_dong WHEN OLD.trang_thai='DA_CHOT'
                BEGIN SELECT RAISE(ABORT,'Bang tat toan da chot bat bien'); END;
            CREATE TRIGGER khau_tru_chot_insert BEFORE INSERT ON chi_tiet_khau_tru WHEN (SELECT trang_thai FROM thanh_ly_hop_dong WHERE id=NEW.thanh_ly_id)<>'NHAP'
                BEGIN SELECT RAISE(ABORT,'Chi sua khau tru ban nhap'); END;
            CREATE TRIGGER khau_tru_chot_update BEFORE UPDATE ON chi_tiet_khau_tru WHEN
                (SELECT trang_thai FROM thanh_ly_hop_dong WHERE id=OLD.thanh_ly_id)<>'NHAP' OR NEW.thanh_ly_id<>OLD.thanh_ly_id
                BEGIN SELECT RAISE(ABORT,'Chi sua khau tru ban nhap'); END;
            CREATE TRIGGER khau_tru_chot_delete BEFORE DELETE ON chi_tiet_khau_tru WHEN (SELECT trang_thai FROM thanh_ly_hop_dong WHERE id=OLD.thanh_ly_id)<>'NHAP'
                BEGIN SELECT RAISE(ABORT,'Chi sua khau tru ban nhap'); END;
            INSERT INTO app_schema_version(version,applied_at) VALUES(31,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        cmd.ExecuteNonQuery();
        EnsureInvoiceGuard(connection,tx);
        cmd.CommandText = "PRAGMA integrity_check";
        if (cmd.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Integrity check thất bại khi nâng tất toán.");
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var reader = cmd.ExecuteReader()) if (reader.Read()) throw new InvalidOperationException("Foreign key check thất bại khi nâng tất toán.");
        Validate(connection,tx); tx.Commit();
    }
    public static void EnsureInvoiceGuard(SqliteConnection connection,SqliteTransaction tx)
    {
        using var cmd=connection.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','giao_dich_coc')";
        if(Convert.ToInt32(cmd.ExecuteScalar())!=2)return;
        cmd.CommandText="""
            CREATE TRIGGER IF NOT EXISTS coc_cung_hop_dong BEFORE INSERT ON giao_dich_coc WHEN NEW.hoa_don_id IS NOT NULL
                AND NOT EXISTS(SELECT 1 FROM hoa_don WHERE id=NEW.hoa_don_id AND hop_dong_id=NEW.hop_dong_id)
                BEGIN SELECT RAISE(ABORT,'Can tru phai cung hop dong'); END;
            """;
        cmd.ExecuteNonQuery();
    }
    public static void Validate(SqliteConnection connection,SqliteTransaction? tx=null)
    {
        using var cmd=connection.CreateCommand();cmd.Transaction=tx;
        var expectedColumns=new Dictionary<string,string>
        {
            ["giao_dich_coc"]="id,ma_giao_dich_coc,khoa_chong_trung,hop_dong_id,hoa_don_id,loai_giao_dich,so_tien,hinh_thuc,trang_thai,nguoi_thuc_hien_id,ngay_giao_dich",
            ["thanh_ly_hop_dong"]="id,ma_tat_toan,hop_dong_id,hoa_don_cuoi_id,ngay_tra_phong,coc_con_giu_truoc_chot,no_hoa_don_cuoi,cong_no_cu,tien_hu_hong,tien_phai_hoan,tien_khach_tra_them,chi_tiet_chot,dau_van_tay_nguon,ghi_chu,trang_thai,phien_ban,nguoi_lap_id,ngay_tao,cap_nhat_luc",
            ["chi_tiet_khau_tru"]="id,thanh_ly_id,so_thu_tu,ten_khoan,so_tien,ghi_chu"
        };
        foreach(var entry in expectedColumns)
        {
            cmd.CommandText=$"SELECT name FROM pragma_table_info('{entry.Key}')";
            var actual=new HashSet<string>();using(var reader=cmd.ExecuteReader())while(reader.Read())actual.Add(reader.GetString(0));
            if(entry.Value.Split(',').Any(x=>!actual.Contains(x)))throw new InvalidOperationException("Schema tất toán không tương thích: "+entry.Key);
        }
        foreach(var (table,expected) in new[] {("giao_dich_coc",3),("thanh_ly_hop_dong",3),("chi_tiet_khau_tru",1)})
        {
            cmd.CommandText=$"SELECT COUNT(*) FROM pragma_foreign_key_list('{table}') WHERE on_delete='RESTRICT'";
            if(Convert.ToInt32(cmd.ExecuteScalar())!=expected)throw new InvalidOperationException("Schema tất toán thiếu FK RESTRICT: "+table);
        }
        cmd.CommandText="SELECT COUNT(*) FROM pragma_index_list('thanh_ly_hop_dong') i WHERE i.\"unique\"=1 AND (SELECT group_concat(name,',') FROM pragma_index_info(i.name))='hop_dong_id'";
        if(Convert.ToInt32(cmd.ExecuteScalar())!=1)throw new InvalidOperationException("Bảng tất toán thiếu unique hợp đồng.");
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='hoa_don'";
        var expectedTriggers=Convert.ToInt32(cmd.ExecuteScalar())==1?6:5;
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name IN ('tat_toan_no_delete','tat_toan_da_chot','khau_tru_chot_insert','khau_tru_chot_update','khau_tru_chot_delete','coc_cung_hop_dong')";
        if(Convert.ToInt32(cmd.ExecuteScalar())!=expectedTriggers)throw new InvalidOperationException("Schema tất toán thiếu trigger bảo vệ dữ liệu.");
    }
}
