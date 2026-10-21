using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

public static class InvoiceCollectionCancellationSchema
{
    public static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var cmd = connection.CreateCommand(); cmd.Transaction = transaction;
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='thanh_toan'";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) return;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS huy_lan_thu_hoa_don (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                thanh_toan_id INTEGER NOT NULL UNIQUE REFERENCES thanh_toan(id) ON DELETE RESTRICT,
                ly_do_huy TEXT NOT NULL CHECK(length(trim(ly_do_huy)) BETWEEN 5 AND 500),
                nguoi_huy_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ten_nguoi_huy TEXT NOT NULL,
                thoi_diem_huy TEXT NOT NULL,
                phien_ban INTEGER NOT NULL DEFAULT 1 CHECK(phien_ban=1)
            );
            CREATE TRIGGER IF NOT EXISTS huy_lan_thu_no_update BEFORE UPDATE ON huy_lan_thu_hoa_don
            BEGIN SELECT RAISE(ABORT,'Cancellation history is immutable'); END;
            CREATE TRIGGER IF NOT EXISTS huy_lan_thu_no_delete BEFORE DELETE ON huy_lan_thu_hoa_don
            BEGIN SELECT RAISE(ABORT,'Cancellation history cannot be deleted'); END;
            CREATE TRIGGER IF NOT EXISTS huy_lan_thu_state_guard BEFORE INSERT ON huy_lan_thu_hoa_don
            WHEN (SELECT trang_thai FROM thanh_toan WHERE id=NEW.thanh_toan_id)<>'DA_HUY'
            BEGIN SELECT RAISE(ABORT,'Receipt must be canceled before recording cancellation'); END;
            CREATE TRIGGER IF NOT EXISTS canceled_receipt_no_update BEFORE UPDATE ON thanh_toan
            WHEN OLD.trang_thai='DA_HUY' AND EXISTS(SELECT 1 FROM huy_lan_thu_hoa_don WHERE thanh_toan_id=OLD.id)
            BEGIN SELECT RAISE(ABORT,'Canceled receipt cannot be changed or restored'); END;
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT id,thanh_toan_id,ly_do_huy,nguoi_huy_id,ten_nguoi_huy,thoi_diem_huy,phien_ban FROM huy_lan_thu_hoa_don LIMIT 0";
        using (cmd.ExecuteReader()) { }
        Validate(connection, transaction);
    }

    public static void Validate(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var cmd = connection.CreateCommand(); cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT COUNT(*) FROM pragma_foreign_key_list('huy_lan_thu_hoa_don') WHERE on_delete='RESTRICT'
            AND (("table"='thanh_toan' AND "from"='thanh_toan_id' AND "to"='id')
                OR ("table"='tai_khoan' AND "from"='nguoi_huy_id' AND "to"='id'))
            """;
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 2) throw new InvalidOperationException("Lịch sử hủy thu thiếu khóa ngoại RESTRICT.");
        cmd.CommandText = """
            SELECT COUNT(*) FROM pragma_index_list('huy_lan_thu_hoa_don') i WHERE i."unique"=1 AND i.partial=0
            AND (SELECT group_concat(name,',') FROM pragma_index_info(i.name))='thanh_toan_id'
            """;
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) throw new InvalidOperationException("Lịch sử hủy thu thiếu khóa unique lần thu.");
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name IN ('huy_lan_thu_no_update','huy_lan_thu_no_delete','huy_lan_thu_state_guard','canceled_receipt_no_update')";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 4) throw new InvalidOperationException("Thiếu trigger bảo vệ lịch sử hủy thu.");
    }

    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction(); Ensure(connection, tx);
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(27,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery(); tx.Commit();
    }
}
