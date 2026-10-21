using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class RequestDemoSeeder
{
    public const string OwnerEmail = "owner.demo@demo.local";
    public const string TenantEmail = "tenant1.demo@demo.local";
    public const string Tenant2Email = "tenant2.demo@demo.local";

    public static void Seed(string databasePath, string? password)
    {
        if (string.IsNullOrWhiteSpace(password)
            || password.Length < 8
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            throw new InvalidOperationException(
                "Set RequestDemo:Password to at least 8 characters containing a letter and a digit.");
        }

        DatabaseUpdates.Check(databasePath);
        RentalRequestSchema.Initialize(databasePath);

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            ForeignKeys = true
        }.ToString());
        connection.Open();

        using (var dataCheck = connection.CreateCommand())
        {
            dataCheck.CommandText = """
                SELECT (SELECT COUNT(*) FROM tai_khoan)
                     + (SELECT COUNT(*) FROM khach_thue)
                     + (SELECT COUNT(*) FROM toa_nha)
                     + (SELECT COUNT(*) FROM phong_tro)
                     + (SELECT COUNT(*) FROM tin_dang)
                     + (SELECT COUNT(*) FROM yeu_cau_thue);
                """;
            if (Convert.ToInt32(dataCheck.ExecuteScalar()) != 0)
            {
                throw new InvalidOperationException(
                    "Request demo seeding requires a newly initialized database with no business data.");
            }
        }

        var backupPath = databasePath + ".before-request-demo-" + Guid.NewGuid().ToString("N") + ".bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
        {
            backup.Open();
            connection.BackupDatabase(backup);
        }

        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "PRAGMA foreign_keys=ON";
        command.ExecuteNonQuery();

        var utcNow = DateTime.UtcNow;
        var now = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0, DateTimeKind.Utc);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var ownerId = InsertAccount(connection, tx, "Chủ nhà Demo", OwnerEmail, "0900000101", "CHU_NHA", passwordHash, now);
        var tenantAccountId = InsertAccount(connection, tx, "Khách thuê 1", TenantEmail, "0900000102", "KHACH_THUE", passwordHash, now);
        var conflictTenantAccountId = InsertAccount(connection, tx, "Khách thuê 2", Tenant2Email, "0900000103", "KHACH_THUE", passwordHash, now);
        var tenantId = Scalar(connection, tx, """
            INSERT INTO khach_thue(tai_khoan_id,ho_ten,so_dien_thoai,ngay_tao)
            VALUES ($account,'Khách thuê 1','0912345678',$now)
            RETURNING id;
            """, ("$account", tenantAccountId), ("$now", now));
        var conflictTenantId = Scalar(connection, tx, """
            INSERT INTO khach_thue(tai_khoan_id,ho_ten,so_dien_thoai,ngay_tao)
            VALUES ($account,'Khách thuê 2','0912345679',$now)
            RETURNING id;
            """, ("$account", conflictTenantAccountId), ("$now", now));

        var alphaId = Scalar(connection, tx, """
            INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,phuong_xa,quan_huyen,tinh_thanh,so_tang,dang_hoat_dong)
            VALUES ($owner,'Demo Tòa Alpha','1 Đường Demo A','Phường Demo','Quận 1','TP.HCM',5,1)
            RETURNING id;
            """, ("$owner", ownerId));
        var betaId = Scalar(connection, tx, """
            INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,phuong_xa,quan_huyen,tinh_thanh,so_tang,dang_hoat_dong)
            VALUES ($owner,'Demo Tòa Beta','2 Đường Demo B','Phường Demo','Quận 3','TP.HCM',4,1)
            RETURNING id;
            """, ("$owner", ownerId));

        var roomAlpha = InsertRoom(connection, tx, alphaId, "A101-DEMO", 1, now);
        var roomAlphaViewing = InsertRoom(connection, tx, alphaId, "A102-DEMO", 1, now);
        var roomBeta = InsertRoom(connection, tx, betaId, "B202-DEMO", 2, now);
        var listingAlpha = InsertListing(connection, tx, roomAlpha, ownerId, "Demo yêu cầu thuê - Phòng Alpha", now);
        var listingAlphaViewing = InsertListing(connection, tx, roomAlphaViewing, ownerId, "Demo lịch xem - Phòng Alpha A102", now);
        var listingBeta = InsertListing(connection, tx, roomBeta, ownerId, "Demo yêu cầu thuê - Phòng Beta", now);

        var conflictAppointment = now.AddDays(3);
        var instantRequestId = InsertRequest(connection, tx, "DEMO-YC-001", listingAlpha, tenantId, "THUE_NGAY", "MOI", now.AddHours(-1), now.AddDays(1), null, null);
        var confirmRequestId = InsertRequest(connection, tx, "DEMO-YC-002", listingAlphaViewing, tenantId, "XEM_PHONG", "DA_HEN_LICH", now.AddHours(-26), conflictAppointment.AddMinutes(15), ownerId, now.AddMinutes(-10));
        var conflictRequestId = InsertRequest(connection, tx, "DEMO-YC-003", listingAlphaViewing, conflictTenantId, "XEM_PHONG", "DA_HEN_LICH", now.AddHours(-30), conflictAppointment, ownerId, now.AddHours(-20));
        var rejectRequestId = InsertRequest(connection, tx, "DEMO-YC-004", listingBeta, tenantId, "XEM_PHONG", "MOI", now.AddHours(-2), now.AddDays(4), null, null);

        tx.Commit();
        Console.WriteLine("Request demo data seeded.");
        Console.WriteLine("Backup: " + backupPath);
        Console.WriteLine("Owner: " + OwnerEmail + " / " + password);
        Console.WriteLine("Tenant: " + TenantEmail + " / " + password);
        Console.WriteLine($"Confirm and clash warning: /LichHen/ChiTiet/{confirmRequestId}");
        Console.WriteLine($"Existing conflicting appointment: /LichHen/ChiTiet/{conflictRequestId} at {conflictAppointment.AddHours(7):dd/MM/yyyy HH:mm} Vietnam time");
        Console.WriteLine($"Reject with required reason: /LichHen/ChiTiet/{rejectRequestId}");
        Console.WriteLine($"Approve instant rental: /LichHen/ChiTiet/{instantRequestId}");
    }

    private static int InsertAccount(SqliteConnection connection, SqliteTransaction tx, string name, string email, string phone, string role, string passwordHash, DateTime now) =>
        Scalar(connection, tx, """
            INSERT INTO tai_khoan
                (ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,failed_login_count,must_change_password,email_confirmed,is_deleted,ngay_tao,ngay_cap_nhat)
            VALUES
                ($name,$email,$phone,$hash,$role,1,0,0,0,0,1,0,$now,$now)
            RETURNING id;
            """,
            ("$name", name), ("$email", email), ("$phone", phone), ("$hash", passwordHash), ("$role", role), ("$now", now));

    private static int InsertRoom(SqliteConnection connection, SqliteTransaction tx, int buildingId, string code, int floor, DateTime now) =>
        Scalar(connection, tx, """
            INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,loai_phong,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,mo_ta,ngay_tao)
            VALUES ($building,$code,$floor,'Studio',25,3500000,3500000,3,'TRONG','Phòng demo để kiểm thử yêu cầu thuê',$now)
            RETURNING id;
            """, ("$building", buildingId), ("$code", code), ("$floor", floor), ("$now", now));

    private static int InsertListing(SqliteConnection connection, SqliteTransaction tx, int roomId, int ownerId, string title, DateTime now) =>
        Scalar(connection, tx, """
            INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,ngay_dang,ngay_het_han,trang_thai,ngay_tao)
            VALUES ($room,$owner,$title,'Tin demo dùng để kiểm thử danh sách yêu cầu thuê',$now,$expires,'DANG_HIEN_THI',$now)
            RETURNING id;
            """, ("$room", roomId), ("$owner", ownerId), ("$title", title), ("$now", now), ("$expires", now.AddDays(30)));

    private static int InsertRequest(
        SqliteConnection connection,
        SqliteTransaction tx,
        string code,
        int listingId,
        int tenantId,
        string type,
        string status,
        DateTime createdAt,
        DateTime desiredAt,
        int? handlerId,
        DateTime? handledAt)
    {
        return Scalar(connection, tx, """
            INSERT INTO yeu_cau_thue
                (ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,loi_nhan,lich_hen,trang_thai,ly_do_tu_choi,nguoi_xu_ly_id,ngay_xu_ly,ngay_tao)
            VALUES
                ($code,$listing,$tenant,$type,$desired,2,'Dữ liệu demo kiểm thử tiêu chí xử lý lịch hẹn',$appointment,$status,$rejectReason,$handler,$handled,$created)
            RETURNING id;
            """,
            ("$code", code),
            ("$listing", listingId),
            ("$tenant", tenantId),
            ("$type", type),
            ("$desired", DateOnly.FromDateTime(desiredAt).ToString("yyyy-MM-dd")),
            ("$appointment", status == "DA_HEN_LICH" ? desiredAt : DBNull.Value),
            ("$status", status),
            ("$rejectReason", status == "TU_CHOI" ? "Không phù hợp điều kiện thuê" : DBNull.Value),
            ("$handler", handlerId.HasValue ? handlerId.Value : DBNull.Value),
            ("$handled", handledAt.HasValue ? handledAt.Value : DBNull.Value),
            ("$created", createdAt));
    }

    private static int Scalar(SqliteConnection connection, SqliteTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = CreateCommand(connection, tx, sql, parameters);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = CreateCommand(connection, tx, sql, parameters);
        command.ExecuteNonQuery();
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return command;
    }
}
