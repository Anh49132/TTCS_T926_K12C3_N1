using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using QL_PhongTro.Data;
using Xunit;

namespace QL_PhongTro.Tests;

public class LoginAttemptSchemaTests
{
    [Fact]
    public void UpgradePreservesEveryLegacyValueAndIsIdempotent()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "QL_PhongTro", "Data"))) root = root.Parent;
        var seed = Path.Combine(root!.FullName, "QL_PhongTro", "Data", "permissions.seed.json");
        var folder = Path.Combine(Path.GetTempPath(), "nhatot-v42-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var fake = Path.Combine(folder, "legacy.sqlite");
            LocalDatabaseInitializer.Create(fake, seed);
            using (var c = Open(fake))
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                    DROP TABLE login_failed_attempt;
                    DELETE FROM app_schema_version WHERE version=42;
                    INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,ngay_tao,ngay_cap_nhat,failed_login_count,locked_until)
                    VALUES('Fixture','fixture@v42.test','0900000042','legacy-hash','KHACH_THUE','2026-01-01','2026-01-02',5,'2026-10-20 10:00:00');
                    """;
                cmd.ExecuteNonQuery();
            }
            Verify(fake, seed);
            // Optional, explicitly selected source. Only a read-only SQLite backup is upgraded.
            var source = Environment.GetEnvironmentVariable("NHATOT_UPGRADE_SOURCE");
            if (!string.IsNullOrWhiteSpace(source))
            {
                var before = SHA256.HashData(File.ReadAllBytes(source));
                var copy = Path.Combine(folder, "source-copy.sqlite");
                using (var from = Open(source, true))
                using (var to = Open(copy)) from.BackupDatabase(to);
                Verify(copy, seed);
                Assert.True(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Source DB bytes changed.");
            }
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }
    }

    private static SqliteConnection Open(string path, bool readOnly = false)
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate, Pooling = false, ForeignKeys = true }.ToString());
        c.Open(); return c;
    }

    private static Dictionary<string, string> Fingerprints(string path)
    {
        using var c = Open(path, true);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT IN ('app_schema_version','login_failed_attempt') ORDER BY name";
        var tables = new List<string>();
        using (var r = cmd.ExecuteReader()) while (r.Read()) tables.Add(r.GetString(0));
        var hashes = new Dictionary<string, string>();
        foreach (var table in tables)
        {
            cmd.CommandText = "SELECT * FROM \"" + table.Replace("\"", "\"\"") + "\"";
            var rows = new List<string>();
            using (var r = cmd.ExecuteReader())
                while (r.Read()) rows.Add(JsonSerializer.Serialize(Enumerable.Range(0, r.FieldCount).Select(r.GetValue).ToArray()));
            rows.Sort(StringComparer.Ordinal);
            hashes[table] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))));
        }
        return hashes;
    }

    private static void Verify(string path, string seed)
    {
        var before = Fingerprints(path);
        DatabaseUpdates.Update(path, seed);
        DatabaseUpdates.Check(path);
        var after = Fingerprints(path);
        Assert.True(before.Count == after.Count && before.All(x => after.GetValueOrDefault(x.Key) == x.Value), "Legacy values changed.");
        using (var c = Open(path, true))
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check"; Assert.Equal("ok", cmd.ExecuteScalar());
            cmd.CommandText = "PRAGMA foreign_key_check"; using var r = cmd.ExecuteReader(); Assert.False(r.Read());
        }
        SqliteConnection.ClearAllPools();
        var bytes = SHA256.HashData(File.ReadAllBytes(path));
        DatabaseUpdates.Update(path, seed);
        SqliteConnection.ClearAllPools();
        Assert.True(bytes.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Second update changed DB bytes.");
    }
}
