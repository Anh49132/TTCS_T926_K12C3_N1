using Microsoft.Data.Sqlite;
using Xunit;

namespace QL_PhongTro.Tests;

internal static class SchemaTestFixture
{
    internal static void RemoveChiPhiExtensions(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open(); using var command = connection.CreateCommand();
        // Synthetic databases only: remove the empty v31 extension before
        // simulating v28-v30. Never called on the demo databases.
        command.CommandText = "SELECT COUNT(*) FROM chi_phi_bao_hong";
        Assert.Equal(0L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM lich_su_chi_phi_bao_hong";
        Assert.Equal(0L, command.ExecuteScalar());
        command.CommandText = """
            DROP VIEW chi_phi_bao_hong_trang_thai;
            DROP TRIGGER IF EXISTS chi_phi_invoice_insert;
            DROP TRIGGER IF EXISTS chi_phi_invoice_update;
            DROP TRIGGER IF EXISTS chi_phi_invoice_delete;
            DROP TRIGGER IF EXISTS chi_phi_invoice_parent;
            DROP TRIGGER IF EXISTS chi_phi_invoice_cost_lock;
            DROP TABLE lich_su_chi_phi_bao_hong;
            DROP TABLE chi_phi_bao_hong;
            DROP INDEX IF EXISTS ux_hoa_don_bao_hong;
            """;
        command.ExecuteNonQuery();
    }
    internal static void RemoveBaoHongExtensions(string path)
    {
        RemoveChiPhiExtensions(path);
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM bao_hong_trao_doi";
        Assert.Equal(0L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM anh_bao_hong";
        Assert.Equal(0L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM bao_hong";
        Assert.Equal(0L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM bao_hong_so_ma";
        Assert.Equal(0L, command.ExecuteScalar());
        command.CommandText = "DROP TABLE bao_hong_trao_doi; DROP TABLE anh_bao_hong; DROP TABLE bao_hong; DROP TABLE bao_hong_so_ma";
        command.ExecuteNonQuery();
    }
    // Synthetic fixtures start at the current schema. Remove the later, empty
    // room extensions before lowering version markers to emulate an older DB.
    internal static void RemoveRoomExtensions(string path)
    {
        RemoveBaoHongExtensions(path);
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
