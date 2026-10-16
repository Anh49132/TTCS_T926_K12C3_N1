using Microsoft.Data.Sqlite;
using QL_PhongTro.Data;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace QL_PhongTro.Tests;

// S2-08 lát 1: migration v10 only has to add its two tables, and it has to survive a
// database that was already on v9, which is the state every other team leaves behind.
public class LichHenMigrationTests : IDisposable
{
    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
    }

    private readonly string _seedPath = Path.Combine(AppPath(), "Data", "permissions.seed.json");
    private readonly string _folder;
    private readonly string _dbPath;

    public LichHenMigrationTests()
    {
        _folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(_folder);
        _dbPath = Path.Combine(_folder, $"s2_08_mig_{Guid.NewGuid():N}.sqlite");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in Directory.GetFiles(_folder, Path.GetFileName(_dbPath) + "*")) File.Delete(file);
    }

    private void Chay(string sql)
    {
        using var c = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
        c.Open();
        using var command = c.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private string Scalar(string sql)
    {
        using var c = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
        c.Open();
        using var command = c.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
    }

    private void HaVeBanV9()
    {
        SchemaTestFixture.RemoveRoomExtensions(_dbPath);
        Chay("DROP TABLE yeu_cau_thue_lich_su; DROP TABLE yeu_cau_thue_thong_bao; DROP TABLE yeu_cau;"
            + " DELETE FROM app_schema_version WHERE version >= 10;");
        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public void CSDLTrong_UpdateLenV10_TaoDuHaiBang()
    {
        LocalDatabaseInitializer.Create(_dbPath, _seedPath);

        Assert.Equal("40", Scalar("SELECT COALESCE(MAX(version),0) FROM app_schema_version"));
        Assert.Equal("40", Scalar("SELECT COALESCE(MAX(version),0) FROM app_schema_version"));
        Assert.Equal("2", Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN"
            + " ('yeu_cau_thue_lich_su','yeu_cau_thue_thong_bao')"));
        DatabaseUpdates.Check(_dbPath);
    }

    [Fact]
    public void CSLDaCoV9_UpdateLenV10_KhongDungLaiBangCu()
    {
        LocalDatabaseInitializer.Create(_dbPath, _seedPath);
        HaVeBanV9();

        DatabaseUpdates.Update(_dbPath, _seedPath);

        Assert.Equal("40", Scalar("SELECT COALESCE(MAX(version),0) FROM app_schema_version"));
        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name='yeu_cau_thue_lich_su'"));
        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name='yeu_cau_thue_thong_bao'"));
        DatabaseUpdates.Check(_dbPath);
    }

    [Fact]
    public void ChayUpdateLanThuHai_KhongThayDoiGi()
    {
        LocalDatabaseInitializer.Create(_dbPath, _seedPath);
        DatabaseUpdates.Update(_dbPath, _seedPath);
        var truoc = Scalar("SELECT version || '|' || applied_at FROM app_schema_version WHERE version=12");

        DatabaseUpdates.Update(_dbPath, _seedPath);

        Assert.Equal(truoc, Scalar("SELECT version || '|' || applied_at FROM app_schema_version WHERE version=12"));
        DatabaseUpdates.Check(_dbPath);
    }

    [Fact]
    public void BangCuaS208_KhongKhoaYeuCauThueCuaS206()
    {
        LocalDatabaseInitializer.Create(_dbPath, _seedPath);
        RentalRequestSchema.Initialize(_dbPath);

        // S2-06 owns yeu_cau_thue; S2-08 must not add a foreign key into it.
        var khoa = Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='yeu_cau_thue'");
        Assert.Equal("1", khoa);
        using var c = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
        c.Open();
        foreach (var table in new[] { "yeu_cau_thue_lich_su", "yeu_cau_thue_thong_bao" })
        {
            using var command = c.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM pragma_foreign_key_list('{table}') WHERE \"table\"='yeu_cau_thue'";
            Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
        }
    }
}
