using Microsoft.Data.Sqlite;
using Xunit;

namespace QL_PhongTro.Tests;

internal static class SchemaTestFixture
{
    internal static void RemoveXacNhanExtensions(string path)
    {
        using var c=new SqliteConnection($"Data Source={path};Pooling=False");c.Open();using var cmd=c.CreateCommand();
        foreach(var table in new[] {"xac_nhan_tat_toan","lich_su_trang_thai_phong"})
        {
            cmd.CommandText=$"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
            if(Convert.ToInt32(cmd.ExecuteScalar())==0)continue;
            cmd.CommandText=$"SELECT COUNT(*) FROM {table}";Assert.Equal(0L,cmd.ExecuteScalar());
            cmd.CommandText=$"DROP TABLE {table}";cmd.ExecuteNonQuery();
        }
        cmd.CommandText="DROP TRIGGER IF EXISTS hop_dong_ket_thuc_no_invoice; DROP TRIGGER IF EXISTS hop_dong_ket_thuc_no_publish; DROP TRIGGER IF EXISTS tat_toan_no_cancel_invoice; DELETE FROM app_schema_version WHERE version>=33";
        cmd.ExecuteNonQuery();
    }
    internal static void RemoveTatToanExtensions(string path)
    {
        RemoveXacNhanExtensions(path);
        using var connection=new SqliteConnection($"Data Source={path};Pooling=False");connection.Open();using var cmd=connection.CreateCommand();
        foreach(var table in new[] {"cham_dut_hop_dong","chi_tiet_khau_tru","thanh_ly_hop_dong","giao_dich_coc"})
        {
            cmd.CommandText=$"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
            if(Convert.ToInt32(cmd.ExecuteScalar())==0)continue;
            cmd.CommandText=$"SELECT COUNT(*) FROM {table}";Assert.Equal(0L,cmd.ExecuteScalar());
            cmd.CommandText=$"DROP TABLE {table}";cmd.ExecuteNonQuery();
        }
    }
    internal static void RemoveBaoHongExtensions(string path)
    {
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
        RemoveTatToanExtensions(path);
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
