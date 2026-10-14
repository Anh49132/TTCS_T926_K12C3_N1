using Microsoft.Data.Sqlite;
using System.IO;

namespace QL_PhongTro.Data;

public static class DichVuSchemaInitializer
{
    public static void InitializeInvoices(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true }.ToString());
        connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT id, phong_id, trang_thai, ngay_tra_phong FROM hop_dong LIMIT 0; SELECT id, hop_dong_id, gia_thue, ngay_bat_dau, ngay_ket_thuc FROM ky_hop_dong LIMIT 0; SELECT id, dich_vu_id, da_chot_gia FROM cau_hinh_dich_vu LIMIT 0;";
        using (var reader = check.ExecuteReader()) { while (reader.NextResult()) { } }
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('hoa_don','chi_tiet_hoa_don','hop_dong_dich_vu')";
        if (Convert.ToInt32(check.ExecuteScalar()) != 0) throw new InvalidOperationException("Schema hóa đơn/hợp đồng dịch vụ đã tồn tại; cần xử lý thủ công, không tạo lại.");
        check.CommandText = "PRAGMA integrity_check";
        if (check.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("CSDL chưa đạt integrity_check.");
        var backupPath = databasePath + ".before-s109-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
        { backup.Open(); connection.BackupDatabase(backup); }
        using var stream = typeof(DichVuSchemaInitializer).Assembly.GetManifestResourceStream("S1-09-hoa-don.sql")!;
        using var readerScript = new StreamReader(stream);
        using var tx = connection.BeginTransaction();
        check.Transaction = tx; check.CommandText = readerScript.ReadToEnd(); check.ExecuteNonQuery();
        InvoiceIssueDateSchema.Ensure(connection, tx);
        check.CommandText = "SELECT MAX(version) FROM app_schema_version";
        if (Convert.ToInt32(check.ExecuteScalar()) >= 22) InvoiceCancellationSchema.Ensure(connection, tx);
        InvoicePaymentSchema.Ensure(connection, tx);
        check.CommandText = "SELECT MAX(version) FROM app_schema_version";
        if (Convert.ToInt32(check.ExecuteScalar()) >= 26) InvoiceCollectionSchema.Ensure(connection, tx);
        check.CommandText = "SELECT MAX(version) FROM app_schema_version";
        if (Convert.ToInt32(check.ExecuteScalar()) >= 27) InvoiceCollectionCancellationSchema.Ensure(connection, tx);
        check.CommandText = "SELECT MAX(version) FROM app_schema_version";
        if(Convert.ToInt32(check.ExecuteScalar())>=31)TatToanSchema.EnsureInvoiceGuard(connection,tx);
        check.CommandText = "PRAGMA foreign_key_check";
        using (var reader = check.ExecuteReader()) if (reader.Read()) throw new InvalidOperationException("Khóa ngoại không hợp lệ; hãy thay đổi.");
        tx.Commit(); Console.WriteLine("Đã thêm schema hóa đơn/dịch vụ. Bản sao lưu: " + backupPath);
    }
    // Explicit operator command only. Do not call this from web startup.
    public static void Initialize(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true }.ToString());
        connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT id, vai_tro, dang_hoat_dong FROM tai_khoan LIMIT 0; SELECT id, chu_nha_id, dang_hoat_dong FROM toa_nha LIMIT 0; SELECT id, toa_nha_id FROM phong_tro LIMIT 0;";
        using (var reader = check.ExecuteReader()) { while (reader.NextResult()) { } }
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('dich_vu','cau_hinh_dich_vu')";
        if (Convert.ToInt32(check.ExecuteScalar()) != 0)
            throw new InvalidOperationException("Bảng dịch vụ đã tồn tại. Cần xử lý thủ công schema; không tạo lại bảng.");
        check.CommandText = "PRAGMA integrity_check";
        if (!string.Equals(check.ExecuteScalar()?.ToString(), "ok", StringComparison.Ordinal))
            throw new InvalidOperationException("CSDL chưa đạt kiểm tra integrity_check.");
        var backupPath = databasePath + ".before-s109-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = backupPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString()))
        { backup.Open(); connection.BackupDatabase(backup); }
        using var stream = typeof(DichVuSchemaInitializer).Assembly.GetManifestResourceStream("S1-09-dich-vu.sql")
            ?? throw new InvalidOperationException("Không tìm thấy script dịch vụ.");
        using var script = new StreamReader(stream);
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = script.ReadToEnd();
        command.ExecuteNonQuery();
        command.CommandText = "PRAGMA foreign_key_check";
        using (var reader = command.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("Kiểm tra khóa ngoại thất bại; hãy thay đổi.");
        transaction.Commit();
        Console.WriteLine("Đã thêm schema dịch vụ. Bản sao lưu: " + backupPath);
    }
}
