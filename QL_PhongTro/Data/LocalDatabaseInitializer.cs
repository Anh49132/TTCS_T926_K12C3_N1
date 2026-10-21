using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class LocalDatabaseInitializer
{
    public static void Create(string path, string seedPath)
    {
        if (File.Exists(path)) throw new IOException("Database already exists; refusing to overwrite: " + path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staging = path + ".init-" + Guid.NewGuid().ToString("N") + ".sqlite";
        // Minimal legacy base; the versioned updater installs the current schema.
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = staging, ForeignKeys = true }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE tai_khoan (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ho_ten TEXT NOT NULL, email TEXT NOT NULL, so_dien_thoai TEXT NOT NULL,
                    mat_khau TEXT NOT NULL, vai_tro TEXT DEFAULT 'KHACH_THUE',
                    dang_hoat_dong INTEGER NOT NULL DEFAULT 1,
                    is_staff INTEGER NOT NULL DEFAULT 0, is_superuser INTEGER NOT NULL DEFAULT 0,
                    last_login TEXT, ngay_tao TEXT NOT NULL, ngay_cap_nhat TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }
        DatabaseUpdates.Update(staging, seedPath);
        DatabaseUpdates.Check(staging);
        SqliteConnection.ClearAllPools();
        File.Move(staging, path, overwrite: false);
        Console.WriteLine("Local database created, no demo accounts or credentials: " + path);
    }
}
