using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

internal static class RoomServicePriceSchema
{
    internal static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "PRAGMA table_info(\"dich_vu_phong\")";
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                if (reader.GetString(1) == "don_gia_rieng")
                    throw new InvalidOperationException("Room service price column exists without the matching schema version; inspect the database before updating.");

        using var stream = typeof(RoomServicePriceSchema).Assembly.GetManifestResourceStream("S2-01-gia-rieng-dich-vu-phong.sql")
            ?? throw new InvalidOperationException("Missing room service price migration resource.");
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