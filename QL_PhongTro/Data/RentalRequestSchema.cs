using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

// Optional module version 1; installed explicitly, never during web startup.
public static class RentalRequestSchema
{
    public static void Initialize(string path)
    {
        DatabaseUpdates.Check(path); // Read-only validation before any write.
        using var updateLock = new FileStream(path + ".update.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var c = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true }.ToString());
        c.Open();
        using var check = c.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('tin_dang','yeu_cau_thue','rental_request_schema','rental_request_counter')";
        var count = Convert.ToInt32(check.ExecuteScalar());
        if (count != 0 && count != 1)
        {
            if (count != 4) throw new InvalidOperationException("Unknown/partial rental schema; review manually. No changes made.");
            check.CommandText = "SELECT version FROM rental_request_schema";
            if (Convert.ToInt32(check.ExecuteScalar()) != 1) throw new InvalidOperationException("Unsupported rental module version.");
            Console.WriteLine("Rental request module v1 already installed. No changes.");
            return;
        }
        // v9 already supplies tin_dang; DatabaseUpdates.Check validated its columns read-only.
        if (count == 1)
        {
            check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='tin_dang'";
            if (Convert.ToInt32(check.ExecuteScalar()) != 1)
                throw new InvalidOperationException("Unknown/partial rental schema; review manually. No changes made.");
        }
        check.CommandText = "PRAGMA foreign_key_check";
        using (var reader = check.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("Existing FK violations; update refused.");
        var backupPath = path + ".before-rental-" + Guid.NewGuid().ToString("N") + ".bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
        { backup.Open(); c.BackupDatabase(backup); }
        using var tx = c.BeginTransaction();
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tin_dang (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                phong_id INTEGER NOT NULL REFERENCES phong_tro(id) ON DELETE RESTRICT,
                nguoi_dang_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                tin_goc_id INTEGER REFERENCES tin_dang(id) ON DELETE RESTRICT,
                tieu_de TEXT NOT NULL, noi_dung TEXT, ngay_dang TEXT, ngay_het_han TEXT,
                trang_thai TEXT NOT NULL DEFAULT 'NHAP', ngay_tao TEXT NOT NULL
            );
            CREATE INDEX ix_tin_dang_public ON tin_dang(trang_thai,ngay_het_han);
            CREATE UNIQUE INDEX ux_tin_dang_active_room ON tin_dang(phong_id) WHERE trang_thai='DANG_HIEN_THI';
            CREATE TABLE yeu_cau_thue (
                id INTEGER PRIMARY KEY AUTOINCREMENT, ma_yeu_cau TEXT NOT NULL UNIQUE,
                tin_dang_id INTEGER NOT NULL REFERENCES tin_dang(id) ON DELETE RESTRICT,
                khach_thue_id INTEGER NOT NULL REFERENCES khach_thue(id) ON DELETE RESTRICT,
                loai_yeu_cau TEXT NOT NULL CHECK(loai_yeu_cau IN ('XEM_PHONG','THUE_NGAY')),
                ngay_mong_muon TEXT NOT NULL, so_nguoi_du_kien INTEGER NOT NULL CHECK(so_nguoi_du_kien>0),
                loi_nhan TEXT, lich_hen TEXT, trang_thai TEXT NOT NULL DEFAULT 'MOI', ly_do_tu_choi TEXT,
                nguoi_xu_ly_id INTEGER REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ngay_xu_ly TEXT, ngay_tao TEXT NOT NULL, phien_ban INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX ix_yeu_cau_tenant ON yeu_cau_thue(khach_thue_id,ngay_tao);
            CREATE INDEX ix_yeu_cau_listing ON yeu_cau_thue(tin_dang_id,trang_thai);
            CREATE TABLE rental_request_counter (thang TEXT PRIMARY KEY, so_cuoi INTEGER NOT NULL CHECK(so_cuoi BETWEEN 1 AND 9999));
            CREATE TABLE rental_request_schema (version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL);
            INSERT INTO rental_request_schema VALUES(1,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        cmd.ExecuteNonQuery();
        tx.Commit();
        DatabaseUpdates.Check(path);
        Console.WriteLine("Rental request module v1 installed. Backup: " + backupPath);
    }
}
