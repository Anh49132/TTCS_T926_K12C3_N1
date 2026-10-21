using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class LocalAdminInitializer
{
    public static void Create(string databasePath, string? configuredEmail, string? configuredPassword, string? configuredPhone)
    {
        var email = configuredEmail?.Trim().ToLowerInvariant();
        var password = configuredPassword ?? "";
        var phone = configuredPhone?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new InvalidOperationException("Set LocalAdmin__Email to a valid development admin email.");
        if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new InvalidOperationException("Set LocalAdmin__Password to at least 8 characters with a letter and a digit.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^0[0-9]{9}$"))
            throw new InvalidOperationException("Set LocalAdmin__Phone to 10 digits beginning with 0.");

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            ForeignKeys = true
        }.ToString());
        connection.Open();

        using var check = connection.CreateCommand();
        check.CommandText = "SELECT vai_tro FROM tai_khoan WHERE lower(trim(email))=$email";
        check.Parameters.AddWithValue("$email", email);
        if (check.ExecuteScalar() is string role)
        {
            if (role != "ADMIN")
                throw new InvalidOperationException("The local admin email belongs to a non-admin account; refusing to overwrite it.");
            Console.WriteLine("Local admin already exists. No changes.");
            return;
        }

        var backupPath = databasePath + ".before-local-admin-" + Guid.NewGuid().ToString("N") + ".bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
        {
            backup.Open();
            connection.BackupDatabase(backup);
        }

        using var phoneCheck = connection.CreateCommand();
        phoneCheck.CommandText = "SELECT COUNT(*) FROM tai_khoan WHERE so_dien_thoai=$phone";
        phoneCheck.Parameters.AddWithValue("$phone", phone);
        if (Convert.ToInt64(phoneCheck.ExecuteScalar()) != 0)
            throw new InvalidOperationException("The configured phone already belongs to another account; no admin was created. Backup: " + backupPath);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tai_khoan
                (ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,
                 ngay_tao,ngay_cap_nhat,must_change_password)
            VALUES
                ('Admin local',$email,$phone,$password,'ADMIN',1,0,0,$now,$now,0)
            """;
        command.Parameters.AddWithValue("$email", email);
        command.Parameters.AddWithValue("$phone", phone);
        command.Parameters.AddWithValue("$password", BCrypt.Net.BCrypt.HashPassword(password));
        command.Parameters.AddWithValue("$now", DateTime.UtcNow);
        command.ExecuteNonQuery();
        Console.WriteLine("Local development admin created. Backup: " + backupPath);
    }
}
