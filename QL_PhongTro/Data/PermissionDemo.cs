using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class PermissionDemo
{
    public static void Create(string sourcePath, string destinationPath, string seedPath)
    {
        destinationPath = Path.GetFullPath(destinationPath);
        if (File.Exists(destinationPath) || string.Equals(Path.GetFullPath(sourcePath), destinationPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Demo destination must be a new file; existing databases are never overwritten.");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly }.ToString()))
        using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destinationPath }.ToString()))
        {
            source.Open(); target.Open(); source.BackupDatabase(target);
        }
        PermissionSchemaInitializer.Initialize(destinationPath, seedPath);
        using var connection = new SqliteConnection("Data Source=" + destinationPath);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(10)) + "a1!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var roles = new[] { "KHACH_THUE", "CHU_NHA", "QUAN_LY", "ADMIN" };
        var emails = new List<string>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < roles.Length; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            var email = $"demo-{roles[i].ToLowerInvariant()}-{suffix}@example.test";
            emails.Add(email);
            cmd.CommandText = """
                INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat)
                VALUES ($name,$email,$phone,$hash,$role,1,0,0,$now,$now);
                """;
            cmd.Parameters.AddWithValue("$name", "Demo " + roles[i]);
            cmd.Parameters.AddWithValue("$email", email);
            string phone;
            while (true)
            {
                phone = "09" + RandomNumberGenerator.GetInt32(10000000, 100000000);
                using var check = connection.CreateCommand();
                check.Transaction = transaction;
                check.CommandText = "SELECT COUNT(*) FROM tai_khoan WHERE so_dien_thoai=$phone";
                check.Parameters.AddWithValue("$phone", phone);
                if (Convert.ToInt64(check.ExecuteScalar()) == 0) break;
            }
            cmd.Parameters.AddWithValue("$phone", phone);
            cmd.Parameters.AddWithValue("$hash", hash);
            cmd.Parameters.AddWithValue("$role", roles[i]);
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow);
            cmd.ExecuteNonQuery();
        }
        transaction.Commit();
        Console.WriteLine("DEMO ONLY - SQLite copy: " + destinationPath);
        foreach (var email in emails) Console.WriteLine("Login: " + email);
        Console.WriteLine("Demo password (all four accounts): " + password);
    }
}

