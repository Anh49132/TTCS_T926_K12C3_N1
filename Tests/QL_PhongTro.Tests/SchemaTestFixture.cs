using Microsoft.Data.Sqlite;
using Xunit;

namespace QL_PhongTro.Tests;

internal static class SchemaTestFixture
{
    // Synthetic fixtures start at the current schema. Remove the later, empty
    // room extensions before lowering version markers to emulate an older DB.
    internal static void RemoveRoomExtensions(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ghi_chu_phong'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 1)
        {
            command.CommandText = "SELECT COUNT(*) FROM ghi_chu_phong";
            Assert.Equal(0L, command.ExecuteScalar());
            command.CommandText = "DROP TABLE ghi_chu_phong";
            command.ExecuteNonQuery();
        }
        foreach (var column in new[] { "ghi_chu_noi_bo", "tien_nghi" })
        {
            command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('phong_tro') WHERE name='{column}'";
            if (Convert.ToInt32(command.ExecuteScalar()) == 0) continue;
            command.CommandText = $"SELECT COUNT(*) FROM phong_tro WHERE {column} IS NOT NULL";
            Assert.Equal(0L, command.ExecuteScalar());
            command.CommandText = $"ALTER TABLE phong_tro DROP COLUMN {column}";
            command.ExecuteNonQuery();
        }
    }
}
