using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

// Additive reconciliation of the independent v35/v36 branch histories.
public static class Sprint4IntegrationSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        InvoicePaymentReportSchema.Ensure(connection, transaction);
        InvoicePaymentProofSchema.Ensure(connection, transaction);
        ReportPerformanceSchema.Ensure(connection, transaction);
        EmailGuiLogSchema.Ensure(connection, transaction);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        ReportPerformanceSchema.Validate(connection, transaction);
        command.CommandText = "PRAGMA foreign_key_check";
        using (var reader = command.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("Khóa ngoại không hợp lệ; bước v37 đã được hoàn tác.");
        command.CommandText = "PRAGMA integrity_check";
        if (command.ExecuteScalar()?.ToString() != "ok")
            throw new InvalidOperationException("Integrity check không đạt; bước v37 đã được hoàn tác.");
        command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(37,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
