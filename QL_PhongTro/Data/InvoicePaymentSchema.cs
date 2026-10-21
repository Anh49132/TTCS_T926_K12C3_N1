using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class InvoicePaymentSchema
{
    public static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='hoa_don'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return;

        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='thanh_toan'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
        {
            command.CommandText = """
                CREATE TABLE thanh_toan (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    hoa_don_id INTEGER NOT NULL REFERENCES hoa_don(id) ON DELETE RESTRICT,
                    so_tien INTEGER NOT NULL CHECK(so_tien > 0),
                    ngay_thanh_toan TEXT NOT NULL CHECK(date(ngay_thanh_toan) IS NOT NULL AND ngay_thanh_toan=date(ngay_thanh_toan)),
                    trang_thai TEXT NOT NULL DEFAULT 'DA_XAC_NHAN'
                        CHECK(trang_thai IN ('CHO_XAC_NHAN','DA_XAC_NHAN','TU_CHOI','DA_HUY'))
                );
                CREATE INDEX ix_thanh_toan_hoa_don_trang_thai
                    ON thanh_toan(hoa_don_id,trang_thai);
                """;
            command.ExecuteNonQuery();
        }

        command.CommandText = "SELECT id,hoa_don_id,so_tien,ngay_thanh_toan,trang_thai FROM thanh_toan LIMIT 0";
        using (command.ExecuteReader()) { }
        command.CommandText = """
            SELECT COUNT(*) FROM pragma_foreign_key_list('thanh_toan')
            WHERE "table"='hoa_don' AND "from"='hoa_don_id' AND "to"='id' AND on_delete='RESTRICT'
            """;
        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
            throw new InvalidOperationException("Bảng thanh toán thiếu khóa ngoại hóa đơn ON DELETE RESTRICT; cần rà soát schema hiện có.");
        command.CommandText = """
            SELECT COUNT(*) FROM pragma_index_list('thanh_toan') i
            WHERE i.name='ix_thanh_toan_hoa_don_trang_thai'
              AND (SELECT group_concat(name,',') FROM pragma_index_info(i.name))='hoa_don_id,trang_thai'
            """;
        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
            throw new InvalidOperationException("Bảng thanh toán thiếu chỉ mục hóa đơn/trạng thái; cần rà soát schema hiện có.");
    }

    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        Ensure(connection, transaction);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(23,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
