using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

internal static class RoomServiceRemovalSchema
{
    internal static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT id,phong_id,dich_vu_toa_nha_id,don_gia_rieng FROM dich_vu_phong LIMIT 0";
        using (var reader = command.ExecuteReader()) { }
        using var stream = typeof(RoomServiceRemovalSchema).Assembly.GetManifestResourceStream("S2-01-ngung-dich-vu-phong.sql")!;
        using var script = new StreamReader(stream);
        command.CommandText = script.ReadToEnd();
        command.ExecuteNonQuery();
        command.CommandText = "PRAGMA foreign_key_check";
        using (var reader = command.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("Foreign key check failed.");
        command.CommandText = "PRAGMA integrity_check";
        if (command.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Integrity check failed.");
        tx.Commit();
    }
}
