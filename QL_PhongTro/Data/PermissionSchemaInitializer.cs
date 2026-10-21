using System.Text.Json;
using Microsoft.Data.Sqlite;
using QL_PhongTro.Models;

namespace QL_PhongTro.Data;

public static class PermissionSchemaInitializer
{
    public sealed class Seed
    {
        public List<AppRole> Roles { get; set; } = [];
        public List<AppModule> Modules { get; set; } = [];
        public List<RolePermission> Permissions { get; set; } = [];
    }

    // Explicit administrative command only; never runs during normal web startup.
    public static void Initialize(string databasePath, string seedPath)
    {
        var seed = JsonSerializer.Deserialize<Seed>(File.ReadAllText(seedPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Missing permission seed.");
        var roles = seed.Roles.Select(r => r.Code).ToHashSet();
        var modules = seed.Modules.Select(m => m.Code).ToHashSet();
        if (roles.Count != seed.Roles.Count || modules.Count != seed.Modules.Count ||
            seed.Permissions.Count != roles.Count * modules.Count ||
            seed.Permissions.Select(p => (p.RoleCode, p.ModuleCode)).Distinct().Count() != seed.Permissions.Count ||
            seed.Permissions.Any(p => !roles.Contains(p.RoleCode) || !modules.Contains(p.ModuleCode) ||
                p.AccessLevel is not ("NONE" or "READ" or "WRITE" or "FULL")))
            throw new InvalidOperationException("Invalid or incomplete permission matrix.");

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true
        }.ToString());
        connection.Open();
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT id, vai_tro, dang_hoat_dong FROM tai_khoan LIMIT 0";
            using var reader = check.ExecuteReader();
        }
        var backupPath = databasePath + ".before-s1-04-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
        {
            backup.Open();
            connection.BackupDatabase(backup);
        }
        using var transaction = connection.BeginTransaction();
        void Execute(string sql, params (string, object)[] values)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
            command.ExecuteNonQuery();
        }
        Execute("""
            CREATE TABLE IF NOT EXISTS app_role (
                Code TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS app_module (
                Code TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                GroupName TEXT NOT NULL,
                SortOrder INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS role_permission (
                RoleCode TEXT NOT NULL REFERENCES app_role(Code) ON DELETE RESTRICT,
                ModuleCode TEXT NOT NULL REFERENCES app_module(Code) ON DELETE RESTRICT,
                AccessLevel TEXT NOT NULL CHECK (AccessLevel IN ('NONE','READ','WRITE','FULL')),
                OwnDataOnly INTEGER NOT NULL CHECK (OwnDataOnly IN (0,1)),
                PRIMARY KEY (RoleCode, ModuleCode)
            );
            """);
        using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = "SELECT (SELECT COUNT(*) FROM app_role) + (SELECT COUNT(*) FROM app_module) + (SELECT COUNT(*) FROM role_permission)";
        if (Convert.ToInt64(countCommand.ExecuteScalar()) == 0)
        {
            foreach (var r in seed.Roles)
                Execute("INSERT INTO app_role VALUES ($code,$name,$sort)", ("$code", r.Code), ("$name", r.Name), ("$sort", r.SortOrder));
            foreach (var m in seed.Modules)
                Execute("INSERT INTO app_module VALUES ($code,$name,$group,$sort)", ("$code", m.Code), ("$name", m.Name), ("$group", m.GroupName), ("$sort", m.SortOrder));
            foreach (var p in seed.Permissions)
                Execute("INSERT INTO role_permission VALUES ($role,$module,$level,$own)",
                    ("$role", p.RoleCode), ("$module", p.ModuleCode), ("$level", p.AccessLevel), ("$own", p.OwnDataOnly));
        }
        else
        {
            // Never re-grant revoked permissions or overwrite live configuration on rerun.
            Console.WriteLine("Permission data already exists; seed not reapplied.");
        }
        transaction.Commit();
        Console.WriteLine("Permission schema ready. Backup: " + backupPath);
    }
}

