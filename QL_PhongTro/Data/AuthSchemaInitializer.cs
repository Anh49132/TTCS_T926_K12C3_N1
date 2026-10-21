using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class AuthSchemaInitializer
{
    public static void Initialize(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, Mode = SqliteOpenMode.ReadWrite
        }.ToString());
        connection.Open();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(tai_khoan)";
            using var reader = command.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(1));
        }
        if (columns.Count == 0) throw new InvalidOperationException("Missing tai_khoan table.");
        var required = new Dictionary<string, string>
        {
            ["failed_login_count"] = "INTEGER DEFAULT 0",
            ["locked_until"] = "TEXT NULL",
            ["refresh_token_hash"] = "TEXT NULL",
            ["refresh_token_expiry"] = "TEXT NULL"
        };
        var missing = required.Where(column => !columns.Contains(column.Key)).ToArray();
        if (missing.Length == 0) return;

        var backupPath = databasePath + ".before-auth-" + Guid.NewGuid().ToString("N") + ".bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
        {
            backup.Open();
            connection.BackupDatabase(backup);
        }
        using var transaction = connection.BeginTransaction();
        foreach (var column in missing)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"ALTER TABLE tai_khoan ADD COLUMN {column.Key} {column.Value}";
            command.ExecuteNonQuery();
        }
        transaction.Commit();
        Console.WriteLine("Authentication schema ready. Backup: " + backupPath);
    }
}
