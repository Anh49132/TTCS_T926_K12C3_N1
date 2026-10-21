using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;
// v31 is additive. Invoice guards reserve a source even in a draft;
// cancelled snapshots are retained and never automatically requeued or charged.
public static class BaoHongChiPhiSchema
{
    private static bool HasInvoices(SqliteConnection c, SqliteTransaction? tx)
    {
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','chi_tiet_hoa_don')";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        if (count == 1) throw new InvalidOperationException("Incomplete invoice schema; inspect before upgrading repair costs.");
        return count == 2;
    }
    public static void Upgrade(SqliteConnection c) => Install(c, true);
    internal static void InstallForReconciliation(SqliteConnection c) => Install(c, false);
    private static void Install(SqliteConnection c, bool recordVersion)
    {
        using var tx = c.BeginTransaction(deferred: false);
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('chi_phi_bao_hong','lich_su_chi_phi_bao_hong','chi_phi_bao_hong_trang_thai')";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 0)
            throw new InvalidOperationException("Repair cost schema already exists without v31; refusing to adopt or overwrite it.");
        var invoices = HasInvoices(c, tx);
        if (invoices)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM chi_tiet_hoa_don WHERE bao_hong_id IS NOT NULL";
            if (Convert.ToInt32(cmd.ExecuteScalar()) != 0)
                throw new InvalidOperationException("Legacy repair-linked invoice rows require review; no cost records will be invented.");
        }
        cmd.CommandText = """
            CREATE TABLE chi_phi_bao_hong (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                bao_hong_id INTEGER NOT NULL UNIQUE REFERENCES bao_hong(id) ON DELETE RESTRICT,
                hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id) ON DELETE RESTRICT,
                so_tien INTEGER NOT NULL CHECK(typeof(so_tien)='integer' AND so_tien>0),
                ben_chiu_phi TEXT NOT NULL CHECK(ben_chiu_phi IN ('CHU_NHA','KHACH_THUE')),
                cho_thu_khach INTEGER NOT NULL CHECK(cho_thu_khach IN (0,1)),
                ky_du_kien TEXT CHECK(ky_du_kien IS NULL OR
                    (typeof(ky_du_kien)='text' AND length(ky_du_kien)=10 AND substr(ky_du_kien,9,2)='01'
                     AND date(ky_du_kien) IS NOT NULL AND date(ky_du_kien)=ky_du_kien)),
                nguoi_tao_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                nguoi_cap_nhat_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ngay_tao TEXT NOT NULL CHECK(julianday(ngay_tao) IS NOT NULL),
                ngay_cap_nhat TEXT NOT NULL CHECK(julianday(ngay_cap_nhat) IS NOT NULL AND julianday(ngay_cap_nhat)>=julianday(ngay_tao)),
                phien_ban INTEGER NOT NULL CHECK(typeof(phien_ban)='integer' AND phien_ban>=1),
                CHECK((ben_chiu_phi='CHU_NHA' AND cho_thu_khach=0 AND ky_du_kien IS NULL)
                   OR (ben_chiu_phi='KHACH_THUE' AND cho_thu_khach=1)),
                CHECK(ky_du_kien IS NULL OR ky_du_kien>=date(ngay_tao,'+7 hours','start of month','+1 month'))
            );
            CREATE INDEX ix_chi_phi_hop_dong_ky ON chi_phi_bao_hong(hop_dong_id,cho_thu_khach,ky_du_kien);
            CREATE TABLE lich_su_chi_phi_bao_hong (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                chi_phi_id INTEGER NOT NULL REFERENCES chi_phi_bao_hong(id) ON DELETE RESTRICT,
                nguoi_cap_nhat_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                thoi_diem TEXT NOT NULL CHECK(julianday(thoi_diem) IS NOT NULL),
                loai TEXT NOT NULL CHECK(loai IN ('TAO','SUA')),
                so_tien_truoc INTEGER CHECK(so_tien_truoc IS NULL OR (typeof(so_tien_truoc)='integer' AND so_tien_truoc>0)),
                ben_chiu_phi_truoc TEXT CHECK(ben_chiu_phi_truoc IS NULL OR ben_chiu_phi_truoc IN ('CHU_NHA','KHACH_THUE')),
                ky_du_kien_truoc TEXT,
                so_tien_sau INTEGER NOT NULL CHECK(typeof(so_tien_sau)='integer' AND so_tien_sau>0),
                ben_chiu_phi_sau TEXT NOT NULL CHECK(ben_chiu_phi_sau IN ('CHU_NHA','KHACH_THUE')),
                ky_du_kien_sau TEXT,
                phien_ban_truoc INTEGER NOT NULL CHECK(typeof(phien_ban_truoc)='integer' AND phien_ban_truoc>=0),
                phien_ban_sau INTEGER NOT NULL CHECK(typeof(phien_ban_sau)='integer' AND phien_ban_sau=phien_ban_truoc+1),
                khoa_chong_trung TEXT NOT NULL UNIQUE CHECK(length(khoa_chong_trung)=32 AND khoa_chong_trung NOT GLOB '*[^0-9a-f]*'),
                UNIQUE(chi_phi_id,phien_ban_sau),
                CHECK((loai='TAO' AND phien_ban_truoc=0 AND so_tien_truoc IS NULL AND ben_chiu_phi_truoc IS NULL AND ky_du_kien_truoc IS NULL)
                   OR (loai='SUA' AND phien_ban_truoc>=1 AND so_tien_truoc IS NOT NULL AND ben_chiu_phi_truoc IS NOT NULL)),
                CHECK(ky_du_kien_truoc IS NULL OR (length(ky_du_kien_truoc)=10 AND substr(ky_du_kien_truoc,9,2)='01'
                    AND date(ky_du_kien_truoc) IS NOT NULL AND date(ky_du_kien_truoc)=ky_du_kien_truoc)),
                CHECK(ky_du_kien_sau IS NULL OR (length(ky_du_kien_sau)=10 AND substr(ky_du_kien_sau,9,2)='01'
                    AND date(ky_du_kien_sau) IS NOT NULL AND date(ky_du_kien_sau)=ky_du_kien_sau)),
                CHECK(ben_chiu_phi_truoc IS NOT 'CHU_NHA' OR ky_du_kien_truoc IS NULL),
                CHECK(ben_chiu_phi_sau<>'CHU_NHA' OR ky_du_kien_sau IS NULL)
            );
            CREATE INDEX ix_lich_su_chi_phi_ngay ON lich_su_chi_phi_bao_hong(chi_phi_id,thoi_diem,id);
            CREATE TRIGGER chi_phi_no_delete BEFORE DELETE ON chi_phi_bao_hong
                BEGIN SELECT RAISE(ABORT,'Repair costs must be retained'); END;
            CREATE TRIGGER lich_su_chi_phi_no_update BEFORE UPDATE ON lich_su_chi_phi_bao_hong
                BEGIN SELECT RAISE(ABORT,'Repair cost history is append-only'); END;
            CREATE TRIGGER lich_su_chi_phi_no_delete BEFORE DELETE ON lich_su_chi_phi_bao_hong
                BEGIN SELECT RAISE(ABORT,'Repair cost history is append-only'); END;
            CREATE TRIGGER lich_su_chi_phi_snapshot BEFORE INSERT ON lich_su_chi_phi_bao_hong
            WHEN NOT EXISTS(SELECT 1 FROM chi_phi_bao_hong c WHERE c.id=NEW.chi_phi_id
                AND c.so_tien=NEW.so_tien_sau AND c.ben_chiu_phi=NEW.ben_chiu_phi_sau
                AND c.ky_du_kien IS NEW.ky_du_kien_sau AND c.phien_ban=NEW.phien_ban_sau
                AND c.nguoi_cap_nhat_id=NEW.nguoi_cap_nhat_id AND c.ngay_cap_nhat=NEW.thoi_diem)
              OR (NEW.loai='SUA' AND NOT EXISTS(SELECT 1 FROM lich_su_chi_phi_bao_hong h
                WHERE h.chi_phi_id=NEW.chi_phi_id AND h.phien_ban_sau=NEW.phien_ban_truoc
                AND h.so_tien_sau=NEW.so_tien_truoc AND h.ben_chiu_phi_sau=NEW.ben_chiu_phi_truoc
                AND h.ky_du_kien_sau IS NEW.ky_du_kien_truoc))
            BEGIN SELECT RAISE(ABORT,'Repair cost history snapshot or version chain is invalid'); END;
            """;
        cmd.ExecuteNonQuery();
        const string actor = """
            EXISTS(SELECT 1 FROM bao_hong b JOIN phong_tro p ON p.id=b.phong_id
                JOIN toa_nha t ON t.id=p.toa_nha_id JOIN tai_khoan a ON a.id=NEW.nguoi_cap_nhat_id
                JOIN role_permission rp ON rp.RoleCode=a.vai_tro AND rp.ModuleCode='BAO_HONG'
                WHERE b.id=NEW.bao_hong_id AND b.hop_dong_id=NEW.hop_dong_id AND b.trang_thai='HOAN_THANH'
                AND t.quan_ly_id=a.id AND a.vai_tro='QUAN_LY' AND a.dang_hoat_dong=1 AND a.is_deleted=0
                AND a.email_confirmed=1 AND a.must_change_password=0 AND rp.AccessLevel IN ('WRITE','FULL'))
            """;
        var frozen = invoices ? "EXISTS(SELECT 1 FROM chi_tiet_hoa_don d WHERE d.bao_hong_id=OLD.bao_hong_id)" : "0";
        cmd.CommandText = $"""
            CREATE TRIGGER chi_phi_insert_guard BEFORE INSERT ON chi_phi_bao_hong
            WHEN NOT {actor} OR NEW.phien_ban<>1 OR NEW.nguoi_tao_id<>NEW.nguoi_cap_nhat_id OR NEW.ngay_tao<>NEW.ngay_cap_nhat
            BEGIN SELECT RAISE(ABORT,'Repair cost requires a completed report and its assigned authorized manager'); END;
            CREATE TRIGGER chi_phi_update_guard BEFORE UPDATE ON chi_phi_bao_hong
            WHEN NOT {actor} OR {frozen}
              OR NEW.id<>OLD.id OR NEW.bao_hong_id<>OLD.bao_hong_id OR NEW.hop_dong_id<>OLD.hop_dong_id
              OR NEW.nguoi_tao_id<>OLD.nguoi_tao_id OR NEW.ngay_tao<>OLD.ngay_tao OR NEW.phien_ban<>OLD.phien_ban+1
            BEGIN SELECT RAISE(ABORT,'Repair cost is locked, unauthorized or has an invalid version'); END;
            """;
        cmd.ExecuteNonQuery();
        if (invoices) InstallInvoiceGuards(cmd);
        cmd.CommandText = invoices ? """
            CREATE VIEW chi_phi_bao_hong_trang_thai AS SELECT c.*,
                CASE WHEN c.ben_chiu_phi='CHU_NHA' THEN 'KHONG_THU_KHACH'
                WHEN EXISTS(SELECT 1 FROM chi_tiet_hoa_don d JOIN hoa_don h ON h.id=d.hoa_don_id
                    WHERE d.bao_hong_id=c.bao_hong_id AND h.trang_thai='DA_PHAT_HANH') THEN 'DA_DUA_VAO_HOA_DON'
                WHEN EXISTS(SELECT 1 FROM chi_tiet_hoa_don d JOIN hoa_don h ON h.id=d.hoa_don_id
                    WHERE d.bao_hong_id=c.bao_hong_id AND h.trang_thai='NHAP') THEN 'TRONG_HOA_DON_NHAP'
                WHEN EXISTS(SELECT 1 FROM chi_tiet_hoa_don d WHERE d.bao_hong_id=c.bao_hong_id) THEN 'HOA_DON_DA_HUY'
                ELSE 'CHO_DUA_VAO_HOA_DON' END AS trang_thai_phan_bo
            FROM chi_phi_bao_hong c;
            """ : """
            CREATE VIEW chi_phi_bao_hong_trang_thai AS SELECT c.*,
                CASE WHEN c.ben_chiu_phi='CHU_NHA' THEN 'KHONG_THU_KHACH'
                ELSE 'CHO_DUA_VAO_HOA_DON' END AS trang_thai_phan_bo FROM chi_phi_bao_hong c;
            """;
        cmd.ExecuteNonQuery();
        Validate(c, tx);
        if (recordVersion)
        {
            cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(31,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
            cmd.ExecuteNonQuery();
        } tx.Commit();
    }
    private static void InstallInvoiceGuards(SqliteCommand cmd)
    {
        const string source = """
            EXISTS(SELECT 1 FROM chi_phi_bao_hong c JOIN hoa_don h ON h.id=NEW.hoa_don_id
                WHERE c.bao_hong_id=NEW.bao_hong_id AND c.hop_dong_id=h.hop_dong_id AND c.ben_chiu_phi='KHACH_THUE'
                AND h.trang_thai='NHAP' AND NEW.loai_khoan='PHAT_SINH' AND CAST(NEW.so_luong AS NUMERIC)=1
                AND NEW.don_gia=c.so_tien AND NEW.thanh_tien=c.so_tien AND length(trim(coalesce(NEW.ghi_chu,'')))>0)
            """;
        const string duplicate = """
            EXISTS(SELECT 1 FROM chi_tiet_hoa_don d JOIN hoa_don h ON h.id=d.hoa_don_id
                WHERE d.bao_hong_id=NEW.bao_hong_id AND h.trang_thai IN ('NHAP','DA_PHAT_HANH'))
            """;
        const string duplicateOnUpdate = """
            EXISTS(SELECT 1 FROM chi_tiet_hoa_don d JOIN hoa_don h ON h.id=d.hoa_don_id
                WHERE d.bao_hong_id=NEW.bao_hong_id AND h.trang_thai IN ('NHAP','DA_PHAT_HANH')
                AND d.id<>OLD.id)
            """;
        cmd.CommandText = $"""
            CREATE UNIQUE INDEX ux_hoa_don_bao_hong ON chi_tiet_hoa_don(hoa_don_id,bao_hong_id) WHERE bao_hong_id IS NOT NULL;
            CREATE TRIGGER chi_phi_invoice_insert BEFORE INSERT ON chi_tiet_hoa_don
            WHEN NEW.bao_hong_id IS NOT NULL AND (NOT {source} OR {duplicate})
            BEGIN SELECT RAISE(ABORT,'Invalid or duplicate repair charge'); END;
            CREATE TRIGGER chi_phi_invoice_update BEFORE UPDATE ON chi_tiet_hoa_don
            WHEN (OLD.bao_hong_id IS NOT NULL AND (NEW.bao_hong_id IS NOT OLD.bao_hong_id OR NEW.hoa_don_id<>OLD.hoa_don_id))
              OR (NEW.bao_hong_id IS NOT NULL AND (NOT {source} OR {duplicateOnUpdate}))
            BEGIN SELECT RAISE(ABORT,'Invalid, moved or duplicate repair charge'); END;
            CREATE TRIGGER chi_phi_invoice_delete BEFORE DELETE ON chi_tiet_hoa_don WHEN OLD.bao_hong_id IS NOT NULL
            BEGIN SELECT RAISE(ABORT,'Repair charge snapshots must be retained; use invoice cancellation/replacement'); END;
            CREATE TRIGGER chi_phi_invoice_parent BEFORE UPDATE OF hop_dong_id,trang_thai ON hoa_don
            WHEN EXISTS(SELECT 1 FROM chi_tiet_hoa_don d JOIN chi_phi_bao_hong c ON c.bao_hong_id=d.bao_hong_id
                WHERE d.hoa_don_id=OLD.id AND (c.hop_dong_id<>NEW.hop_dong_id OR
                  (NEW.trang_thai IN ('NHAP','DA_PHAT_HANH') AND EXISTS(
                    SELECT 1 FROM chi_tiet_hoa_don other JOIN hoa_don h ON h.id=other.hoa_don_id
                    WHERE other.bao_hong_id=d.bao_hong_id AND h.id<>OLD.id AND h.trang_thai IN ('NHAP','DA_PHAT_HANH')))))
            BEGIN SELECT RAISE(ABORT,'Repair charge contract or active allocation conflict'); END;
            """;
        cmd.ExecuteNonQuery();
    }
    // Only for explicit invoice installation after v31 was installed without invoices.
    // Existing databases with the invoice module never enter this path.
    public static void InstallForNewInvoiceModule(SqliteConnection c, SqliteTransaction tx)
    {
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(*) FROM chi_tiet_hoa_don";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 0) throw new InvalidOperationException("New invoice module must be empty before repair guards are installed.");
        InstallInvoiceGuards(cmd);
        cmd.CommandText = """
            CREATE TRIGGER chi_phi_invoice_cost_lock BEFORE UPDATE ON chi_phi_bao_hong
            WHEN EXISTS(SELECT 1 FROM chi_tiet_hoa_don d WHERE d.bao_hong_id=OLD.bao_hong_id)
            BEGIN SELECT RAISE(ABORT,'Repair cost is locked by an invoice snapshot'); END;
            DROP VIEW chi_phi_bao_hong_trang_thai;
            CREATE VIEW chi_phi_bao_hong_trang_thai AS SELECT c.*,
                CASE WHEN c.ben_chiu_phi='CHU_NHA' THEN 'KHONG_THU_KHACH'
                WHEN EXISTS(SELECT 1 FROM chi_tiet_hoa_don d JOIN hoa_don h ON h.id=d.hoa_don_id
                    WHERE d.bao_hong_id=c.bao_hong_id AND h.trang_thai='DA_PHAT_HANH') THEN 'DA_DUA_VAO_HOA_DON'
                WHEN EXISTS(SELECT 1 FROM chi_tiet_hoa_don d JOIN hoa_don h ON h.id=d.hoa_don_id
                    WHERE d.bao_hong_id=c.bao_hong_id AND h.trang_thai='NHAP') THEN 'TRONG_HOA_DON_NHAP'
                WHEN EXISTS(SELECT 1 FROM chi_tiet_hoa_don d WHERE d.bao_hong_id=c.bao_hong_id) THEN 'HOA_DON_DA_HUY'
                ELSE 'CHO_DUA_VAO_HOA_DON' END AS trang_thai_phan_bo FROM chi_phi_bao_hong c;
            """;
        cmd.ExecuteNonQuery(); Validate(c, tx);
    }
    public static void Validate(SqliteConnection c, SqliteTransaction? tx = null)
    {
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        foreach (var (table, count) in new[] { ("chi_phi_bao_hong", 4), ("lich_su_chi_phi_bao_hong", 2) })
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_foreign_key_list('{table}') WHERE on_delete='RESTRICT'";
            if (Convert.ToInt32(cmd.ExecuteScalar()) != count) throw new InvalidOperationException(table + ": missing RESTRICT foreign keys.");
        }
        foreach (var (table, columns) in new[] { ("chi_phi_bao_hong", "bao_hong_id"),
            ("lich_su_chi_phi_bao_hong", "khoa_chong_trung"), ("lich_su_chi_phi_bao_hong", "chi_phi_id,phien_ban_sau") })
        {
            cmd.CommandText = $"""
                SELECT COUNT(*) FROM pragma_index_list('{table}') i WHERE i."unique"=1 AND i.partial=0
                AND (SELECT group_concat(name,',') FROM pragma_index_info(i.name))='{columns}'
                """;
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) throw new InvalidOperationException(table + ": missing unique constraint.");
        }
        var expected = new List<string> { "chi_phi_insert_guard", "chi_phi_update_guard", "chi_phi_no_delete",
            "lich_su_chi_phi_no_update", "lich_su_chi_phi_no_delete", "lich_su_chi_phi_snapshot" };
        if (HasInvoices(c, tx)) expected.AddRange(["chi_phi_invoice_insert", "chi_phi_invoice_update", "chi_phi_invoice_delete", "chi_phi_invoice_parent"]);
        foreach (var name in expected)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name=$name";
            cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$name", name);
            if (Convert.ToInt32(cmd.ExecuteScalar()) != 1) throw new InvalidOperationException("Missing repair cost trigger: " + name);
        }
        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name='chi_phi_bao_hong_trang_thai'";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 1) throw new InvalidOperationException("Missing repair cost status view.");
        if (HasInvoices(c, tx))
        {
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_index_list('chi_tiet_hoa_don') WHERE name='ux_hoa_don_bao_hong' AND \"unique\"=1 AND partial=1";
            if (Convert.ToInt32(cmd.ExecuteScalar()) != 1) throw new InvalidOperationException("Missing repair invoice unique index.");
        }
    }
}
