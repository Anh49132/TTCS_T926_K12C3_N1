using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;
public static class RoommateSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS nguoi_o_ghep (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id) ON DELETE RESTRICT,
                khach_thue_id INTEGER NOT NULL REFERENCES khach_thue(id) ON DELETE RESTRICT,
                ngay_vao TEXT NOT NULL, ngay_ra TEXT,
                CHECK(ngay_ra IS NULL OR ngay_ra >= ngay_vao),
                UNIQUE(hop_dong_id,khach_thue_id,ngay_vao));
            CREATE TRIGGER IF NOT EXISTS contract_signer_required_insert BEFORE INSERT ON hop_dong
            WHEN NEW.trang_thai IN ('CHO_HIEU_LUC','DANG_HIEU_LUC') AND NEW.khach_dung_ten_id IS NULL
            BEGIN SELECT RAISE(ABORT,'contract signer required'); END;
            CREATE TRIGGER IF NOT EXISTS contract_signer_required_update BEFORE UPDATE ON hop_dong
            WHEN NEW.trang_thai IN ('CHO_HIEU_LUC','DANG_HIEU_LUC') AND NEW.khach_dung_ten_id IS NULL
            BEGIN SELECT RAISE(ABORT,'contract signer required'); END;
            CREATE TRIGGER IF NOT EXISTS roommate_not_signer BEFORE INSERT ON nguoi_o_ghep
            WHEN NEW.khach_thue_id = (SELECT khach_dung_ten_id FROM hop_dong WHERE id=NEW.hop_dong_id)
            BEGIN SELECT RAISE(ABORT,'roommate cannot be signer'); END;
            CREATE TRIGGER IF NOT EXISTS contract_signer_immutable BEFORE UPDATE OF khach_dung_ten_id ON hop_dong
            WHEN OLD.trang_thai <> 'NHAP' AND NEW.khach_dung_ten_id IS NOT OLD.khach_dung_ten_id
            BEGIN SELECT RAISE(ABORT,'contract signer immutable'); END;
            CREATE TRIGGER IF NOT EXISTS roommate_not_signer_update BEFORE UPDATE ON nguoi_o_ghep
            WHEN NEW.khach_thue_id = (SELECT khach_dung_ten_id FROM hop_dong WHERE id=NEW.hop_dong_id)
            BEGIN SELECT RAISE(ABORT,'roommate cannot be signer'); END;
            INSERT INTO app_schema_version(version,applied_at) VALUES(16,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
