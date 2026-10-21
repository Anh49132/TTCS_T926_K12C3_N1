using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

internal static class RoomServicesSchema
{
    // Called only by the versioned updater, after its read-only checks and backup.
    internal static void Upgrade(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('dich_vu','cau_hinh_dich_vu','khoi_tao_dich_vu')";
        var count = Convert.ToInt32(command.ExecuteScalar());
        if (count == 0) Run("S1-09-dich-vu.sql");
        else if (count != 3) throw new InvalidOperationException("Incomplete service schema; update cancelled.");
        Run("S2-01-dich-vu-phong.sql");
        command.CommandText = "PRAGMA foreign_key_check";
        using (var reader = command.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("Foreign key check failed.");
        command.CommandText = "PRAGMA integrity_check";
        if (command.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Integrity check failed.");
        tx.Commit();

        void Run(string resource)
        {
            using var stream = typeof(RoomServicesSchema).Assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            command.CommandText = reader.ReadToEnd();
            command.ExecuteNonQuery();
        }
    }
}
