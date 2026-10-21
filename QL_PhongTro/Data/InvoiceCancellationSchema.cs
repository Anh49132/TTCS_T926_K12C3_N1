using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;

// v22 changes guards only. Existing invoices, lines and reserved cancellation columns stay untouched.
public static class InvoiceCancellationSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        Ensure(connection, tx);
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(22,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery(); tx.Commit();
    }

    public static void Ensure(SqliteConnection connection, SqliteTransaction tx)
    {
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "PRAGMA table_info(hoa_don)";
        var columns = new List<string>();
        using (var reader = cmd.ExecuteReader()) while (reader.Read()) columns.Add(reader.GetString(1));
        if (columns.Count == 0) return; // Optional invoice module is installed separately.
        foreach (var name in new[] { "thay_the_hoa_don_id", "nguoi_huy_id", "ngay_huy", "ly_do_huy", "phien_ban" })
            if (!columns.Contains(name)) throw new InvalidOperationException("Schema hóa đơn chưa có cột hủy/thay thế; cần rà soát, không tự thêm cột.");
        var mutable = new HashSet<string> { "trang_thai", "nguoi_huy_id", "ngay_huy", "ly_do_huy", "phien_ban" };
        var unchanged = string.Join(" AND ", columns.Where(x => !mutable.Contains(x)).Select(x => {
            var quoted = "\"" + x.Replace("\"", "\"\"") + "\"";
            return $"NEW.{quoted} IS OLD.{quoted}";
        }));
        cmd.CommandText = $"""
            DROP TRIGGER IF EXISTS khoa_hoa_don_update;
            CREATE TRIGGER khoa_hoa_don_update BEFORE UPDATE ON hoa_don
            WHEN OLD.trang_thai<>'NHAP' AND NOT (
              OLD.trang_thai='DA_PHAT_HANH' AND NEW.trang_thai='DA_HUY'
              AND OLD.ngay_huy IS NULL AND OLD.nguoi_huy_id IS NULL AND OLD.ly_do_huy IS NULL
              AND NEW.ngay_huy IS NOT NULL AND NEW.nguoi_huy_id IS NOT NULL
              AND NEW.ly_do_huy IS NOT NULL AND length(trim(NEW.ly_do_huy)) BETWEEN 1 AND 1000
              AND NEW.phien_ban=OLD.phien_ban+1 AND {unchanged})
            BEGIN SELECT RAISE(ABORT,'Issued/cancelled invoice content is immutable'); END;
            CREATE TRIGGER IF NOT EXISTS hoa_don_thay_the_insert BEFORE INSERT ON hoa_don
            WHEN NEW.thay_the_hoa_don_id IS NOT NULL AND NOT EXISTS (
              SELECT 1 FROM hoa_don old WHERE old.id=NEW.thay_the_hoa_don_id AND old.trang_thai='DA_HUY'
              AND old.hop_dong_id=NEW.hop_dong_id AND old.nam=NEW.nam AND old.thang=NEW.thang
              AND old.tu_ngay=NEW.tu_ngay AND old.den_ngay=NEW.den_ngay AND NEW.trang_thai='NHAP')
            BEGIN SELECT RAISE(ABORT,'Replacement must be a draft of the cancelled invoice period'); END;
            CREATE TRIGGER IF NOT EXISTS hoa_don_thay_the_update BEFORE UPDATE ON hoa_don
            WHEN NEW.thay_the_hoa_don_id IS NOT OLD.thay_the_hoa_don_id OR (OLD.thay_the_hoa_don_id IS NOT NULL
              AND (NEW.hop_dong_id IS NOT OLD.hop_dong_id OR NEW.nam IS NOT OLD.nam OR NEW.thang IS NOT OLD.thang
              OR NEW.tu_ngay IS NOT OLD.tu_ngay OR NEW.den_ngay IS NOT OLD.den_ngay))
            BEGIN SELECT RAISE(ABORT,'Replacement origin and period are immutable'); END;
            """;
        cmd.ExecuteNonQuery();
    }
}
