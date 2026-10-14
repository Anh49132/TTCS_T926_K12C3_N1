using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace QL_PhongTro.Data;

// Append new versions; never rewrite an update already shared with the team.
public static class DatabaseUpdates
{
    internal const int CurrentVersion = 32;
    private static SqliteConnection Open(string path, bool readOnly)
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            ForeignKeys = true
        }.ToString());
        c.Open();
        return c;
    }

    private static void Execute(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static bool HasTable(SqliteConnection c, string name)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
        cmd.Parameters.AddWithValue("$name", name);
        return Convert.ToInt64(cmd.ExecuteScalar()) != 0;
    }

    public static void Check(string path) => Check(path, true, true, true, true);

    private static void Check(string path, bool requireAudit, bool requireAccountSecurity = true, bool requireRoomServices = false,
        bool requirePublicListings = false, bool requireCurrentVersion = true)
    {
        using var c = Open(path, true);
        var problems = new List<string>();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(c).Options);
        var schemaVersion = 0;
        if (HasTable(c, "app_schema_version"))
        {
            using var versionCommand = c.CreateCommand();
            versionCommand.CommandText = "SELECT COALESCE(MAX(version),0) FROM app_schema_version";
            schemaVersion = Convert.ToInt32(versionCommand.ExecuteScalar());
        }
        if (requireCurrentVersion && schemaVersion != CurrentVersion)
            problems.Add($"app_schema_version: expected {CurrentVersion}, actual {schemaVersion}");
        var contracts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "hop_dong", "ky_hop_dong" };
        var services = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dich_vu", "cau_hinh_dich_vu", "khoi_tao_dich_vu" };
        var invoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "hoa_don", "chi_tiet_hoa_don", "hop_dong_dich_vu" };
        var invoicePayments = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "thanh_toan" };
        var requiredOptional = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rental = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tin_dang", "yeu_cau_thue" };
        if (HasTable(c, "yeu_cau_thue")) requiredOptional.UnionWith(rental);
        if (contracts.Any(table => HasTable(c, table))) requiredOptional.UnionWith(contracts);
        if (services.Any(table => HasTable(c, table))) requiredOptional.UnionWith(services);
        if (invoices.Any(table => HasTable(c, table)))
        {
            requiredOptional.UnionWith(contracts);
            requiredOptional.UnionWith(services);
            requiredOptional.UnionWith(invoices);
            requiredOptional.UnionWith(invoicePayments);
        }
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()!;
            if(schemaVersion<32 && table=="cham_dut_hop_dong")continue;
            if (schemaVersion < 31 && table is "giao_dich_coc" or "thanh_ly_hop_dong" or "chi_tiet_khau_tru") continue;
            if (table == "bao_hong_trao_doi" && schemaVersion < 30) continue;
            if (table == "anh_bao_hong" && schemaVersion < 29) continue;
            if (table == "bao_hong" && schemaVersion < 28) continue;
            if (table == "huy_lan_thu_hoa_don" && (schemaVersion < 27 || !HasTable(c, "thanh_toan"))) continue;
            if (table == "lan_thu_hoa_don" && (schemaVersion < 26 || !HasTable(c, "thanh_toan"))) continue;
            if (table == "ghi_chu_phong" && schemaVersion < 25) continue;
            if (table == "thong_bao" && schemaVersion < 21) continue;
            if (table == "thanh_toan" && schemaVersion < 23 && !HasTable(c, table)) continue;
            if (table == "chi_so_dien_nuoc" && schemaVersion < 19 && !HasTable(c, table)) continue;
            if (table == "nguoi_o_ghep" && schemaVersion < 16) continue;
            if (table == "hop_dong_chi_so_dau_ky" && schemaVersion < 15) continue;
            if (!requireRoomServices && table is "dich_vu_toa_nha" or "dich_vu_phong" or "ngung_dich_vu_phong") continue;
            if (table == "nhat_ky_hoat_dong" && !requireAudit) continue;
            if (!requirePublicListings && (table is "tin_dang" or "anh_phong") && !HasTable(c, table)) continue;
            // S2-08 tables are produced by migration v10, so they are not required until it has run.
            if (table is "yeu_cau_thue_lich_su" or "yeu_cau_thue_thong_bao" && !HasTable(c, table)) continue;
            var isOptional = contracts.Contains(table) || services.Contains(table) || invoices.Contains(table)
                || invoicePayments.Contains(table) || table == "yeu_cau_thue";
            if (isOptional && !requiredOptional.Contains(table)) continue;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var columns = entity.GetProperties().Select(p => p.GetColumnName(store)!)
                .Where(column => requireAccountSecurity || table != "tai_khoan" || column is not ("email_confirmed" or "is_deleted"));
            if(schemaVersion<32 && table=="thanh_ly_hop_dong")columns=columns.Where(column=>column!="tien_phat");
            if(schemaVersion<32 && table=="chi_tiet_khau_tru")columns=columns.Where(column=>column!="loai_khoan");
            if (schemaVersion < 30 && table == "bao_hong") columns = columns.Where(column => column != "phien_ban");
            if (schemaVersion < 14 && table == "hop_dong")
                columns = columns.Where(column => column is not ("yeu_cau_thue_id" or "khach_dung_ten_id" or "tien_coc_thoa_thuan" or "ngay_chot_hang_thang" or "nguoi_lap_id" or "ngay_tao"));
            if (schemaVersion < 14 && table == "ky_hop_dong")
                columns = columns.Where(column => column is not ("so_thu_tu" or "so_thang" or "nguoi_lap_id" or "ngay_tao"));
            if (table == "anh_phong" && schemaVersion < 12)
                columns = columns.Where(column => column is not ("dang_cho_xoa" or "loi_xoa_gan_nhat" or "lan_thu_xoa_gan_nhat"));
            if (schemaVersion < 19 && table == "toa_nha")
                columns = columns.Where(column => column is not ("dien_tich_dat" or "thang_may" or "bai_do_xe" or "camera_an_ninh" or "bao_ve_24h" or "khu_giat_say" or "san_thuong"));
            if (table == "chi_so_dien_nuoc" && schemaVersion < 18)
                columns = columns.Where(column => column != "da_xac_nhan_bat_thuong");
            if (table == "phong_tro" && schemaVersion < 24)
                columns = columns.Where(column => column is not ("ghi_chu_noi_bo" or "tien_nghi"));
            if (table == "hoa_don" && schemaVersion < 20)
                columns = columns.Where(column => column != "ngay_phat_hanh_nghiep_vu");
            Probe(table, columns);
        }
        if (schemaVersion >= 15) Probe("hop_dong_so_ma", ["nam", "so_cuoi"]);
        if (schemaVersion >= 28) Probe("bao_hong_so_ma", ["thang", "so_cuoi"]);
        if (schemaVersion >= 28) BaoHongSchema.Validate(c);
        if (schemaVersion >= 29) AnhBaoHongSchema.Validate(c);
        if (schemaVersion >= 30) BaoHongTraoDoiSchema.Validate(c);
        Probe("password_reset_token", ["token_hash", "account_id", "expires_at", "used_at"]);
        Probe("password_reset_request", ["id", "email_key", "requested_at"]);
        Probe("account_session_version", ["account_id", "version"]);
        if (requireAccountSecurity) Probe("email_confirmation", ["account_id", "token_hash", "expires_at", "requested_at"]);
        if (HasTable(c, "app_module") && HasTable(c, "app_role") && HasTable(c, "role_permission"))
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT (SELECT COUNT(*) FROM app_module) * (SELECT COUNT(*) FROM app_role) * (SELECT COUNT(*) FROM role_permission)";
            if (Convert.ToInt64(cmd.ExecuteScalar()) == 0) problems.Add("Permission seed is missing; inspect permissions.seed.json and existing permission data.");
        }
        if (requireAudit)
            AuditSchema.Validate(c);
        if (schemaVersion >= 26 && HasTable(c, "thanh_toan") && HasTable(c, "lan_thu_hoa_don")) InvoiceCollectionSchema.Validate(c);
        if (schemaVersion >= 27 && HasTable(c, "thanh_toan") && HasTable(c, "huy_lan_thu_hoa_don")) InvoiceCollectionCancellationSchema.Validate(c);
        if(schemaVersion>=31)TatToanSchema.Validate(c);
        if(schemaVersion>=32)ChamDutHopDongSchema.Validate(c);
        if (problems.Count != 0)
            throw new InvalidOperationException("Database schema is not ready: " + string.Join("; ", problems) +
                "\nStop the app, then run: dotnet run --project QL_PhongTro -- --update-database" +
                "\nUse the same DatabasePath for update and startup. Existing incompatible columns require manual review. No schema was changed by this check.");
        if (!services.Any(table => HasTable(c, table)))
            Console.WriteLine("Optional S1-09 service schema is not installed; service and service-invoice pages are unavailable for this database.");

        void Probe(string table, IEnumerable<string> columns)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var reader = cmd.ExecuteReader();
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read()) actual.Add(reader.GetString(1));
            var missing = columns.Where(column => !actual.Contains(column)).ToArray();
            if (missing.Length > 0) problems.Add(table + ": missing " + string.Join(", ", missing));
        }
    }

    public static void Update(string path, string seedPath)
    {
        // Prevent two updater processes from interleaving. Stop web instances before updating.
        using var updateLock = new FileStream(path + ".update.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using (var readOnly = Open(path, true))
        {
            using var probe = readOnly.CreateCommand();
            probe.CommandText = "PRAGMA integrity_check";
            if (probe.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Integrity check failed.");
            probe.CommandText = "PRAGMA foreign_key_check";
            using var reader = probe.ExecuteReader();
            if (reader.Read()) throw new InvalidOperationException("Foreign key check failed.");
        }
        using var c = Open(path, false);
        if (!HasTable(c, "tai_khoan")) throw new InvalidOperationException("Missing existing tai_khoan table; refusing to create a replacement database.");
        var version = 0;
        if (HasTable(c, "app_schema_version"))
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(MAX(version),0) FROM app_schema_version";
            version = Convert.ToInt32(cmd.ExecuteScalar());
        }
        if (version > CurrentVersion) throw new InvalidOperationException("Database is newer than this code. Pull the matching branch before updating.");
        if (version == CurrentVersion)
        {
            Check(path);
            AccountReuseSchema.Ensure(path);
            Console.WriteLine($"Database already up to date (version {CurrentVersion}). No changes.");
            return;
        }
        if (version >= 5) Check(path, true, true, version >= 8, false, false);
        var backupPath = path + ".before-update-" + Guid.NewGuid().ToString("N") + ".bak";
        using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
        {
            backup.Open();
            c.BackupDatabase(backup);
        }
        Console.WriteLine("Database backup: " + backupPath);
        // Legacy initializers are additive and rerunnable. A failed update is not marked complete.
        // The backup covers the whole update; individual initializers also keep their own backups.
        if (version < 1)
        {
        AuthSchemaInitializer.Initialize(path);
        using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(c).Options))
            RoomSchemaInitializer.EnsureSchema(db);
        PermissionSchemaInitializer.Initialize(path, seedPath);
        PasswordSchemaInitializer.Initialize(path);
        Execute(c, """
            CREATE TABLE IF NOT EXISTS khach_thue (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                tai_khoan_id INTEGER UNIQUE REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ho_ten TEXT NOT NULL, ngay_sinh TEXT, so_giay_to TEXT,
                giay_to_bon_so_cuoi TEXT, anh_giay_to_truoc TEXT, anh_giay_to_sau TEXT,
                dia_chi_thuong_tru TEXT, nghe_nghiep TEXT, so_dien_thoai TEXT,
                email TEXT, lien_he_khan_cap TEXT, ngay_tao TEXT NOT NULL
            );
            """);
        Execute(c, """
            CREATE TABLE IF NOT EXISTS app_schema_version (
                version INTEGER NOT NULL PRIMARY KEY, applied_at TEXT NOT NULL
            );
            INSERT INTO app_schema_version(version, applied_at) VALUES (1, strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """);
        }
        if (version < 2)
        {
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "PRAGMA table_info(tai_khoan)";
            bool hasFlag = false;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) hasFlag |= reader.GetString(1) == "must_change_password";
            if (!hasFlag)
            {
                command.CommandText = "ALTER TABLE tai_khoan ADD COLUMN must_change_password INTEGER NOT NULL DEFAULT 0 CHECK(must_change_password IN (0,1))";
                command.ExecuteNonQuery();
            }
            // Duplicate legacy data causes rollback, never automatic deletion/merging.
            command.CommandText = """
                CREATE UNIQUE INDEX IF NOT EXISTS ux_account_email_normalized ON tai_khoan(lower(trim(email)));
                CREATE UNIQUE INDEX IF NOT EXISTS ux_account_phone ON tai_khoan(so_dien_thoai);
                INSERT INTO app_schema_version(version,applied_at) VALUES(2,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.ExecuteNonQuery();
            tx.Commit();
        }
        if (version < 3)
        {
            Check(path, false, false, false, false, false);
            AuditSchema.Upgrade(c);
        }
        if (version < 4)
        {
            Check(path, true, false, false, false, false);
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            command.CommandText = """
                ALTER TABLE tai_khoan ADD COLUMN email_confirmed INTEGER NOT NULL DEFAULT 1 CHECK(email_confirmed IN (0,1));
                ALTER TABLE tai_khoan ADD COLUMN is_deleted INTEGER NOT NULL DEFAULT 0 CHECK(is_deleted IN (0,1));
                CREATE TABLE email_confirmation (
                    account_id INTEGER NOT NULL PRIMARY KEY REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                    token_hash TEXT NOT NULL UNIQUE,
                    expires_at INTEGER NOT NULL,
                    requested_at INTEGER NOT NULL
                );
                INSERT INTO app_schema_version(version,applied_at) VALUES(4,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.ExecuteNonQuery();
            tx.Commit();
        }
        AccountReuseSchema.Ensure(path);
        if (version < 6) RoomServicesSchema.Upgrade(c);
        if (version < 7) RoomServicePriceSchema.Upgrade(c);
        if (version < 8) RoomServiceRemovalSchema.Upgrade(c);
    if (version < 9)
        {
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS tin_dang (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    phong_id INTEGER NOT NULL REFERENCES phong_tro(id) ON DELETE RESTRICT,
                    nguoi_dang_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                    tin_goc_id INTEGER REFERENCES tin_dang(id) ON DELETE RESTRICT,
                    tieu_de TEXT NOT NULL CHECK(length(tieu_de) <= 200),
                    noi_dung TEXT,
                    ngay_dang TEXT,
                    ngay_het_han TEXT,
                    trang_thai TEXT NOT NULL DEFAULT 'NHAP' CHECK(length(trang_thai) <= 25),
                    ngay_tao TEXT NOT NULL
                );
                CREATE INDEX ix_tin_dang_phong_id_trang_thai ON tin_dang(phong_id, trang_thai);
                CREATE INDEX ix_tin_dang_trang_thai_ngay_het_han ON tin_dang(trang_thai, ngay_het_han);
                CREATE UNIQUE INDEX ux_tin_dang_phong_dang_hien_thi ON tin_dang(phong_id) WHERE trang_thai = 'DANG_HIEN_THI';
                CREATE TABLE IF NOT EXISTS anh_phong (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    phong_id INTEGER NOT NULL REFERENCES phong_tro(id) ON DELETE RESTRICT,
                    duong_dan TEXT NOT NULL CHECK(length(duong_dan) <= 500),
                    duong_dan_anh_nho TEXT CHECK(duong_dan_anh_nho IS NULL OR length(duong_dan_anh_nho) <= 500),
                    thu_tu INTEGER NOT NULL CHECK(thu_tu BETWEEN 1 AND 8),
                    mo_ta TEXT CHECK(mo_ta IS NULL OR length(mo_ta) <= 255),
                    ngay_tao TEXT NOT NULL,
                    UNIQUE(phong_id, thu_tu)
                );
                INSERT INTO app_schema_version(version,applied_at) VALUES(9,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.ExecuteNonQuery();
            tx.Commit();
        }
        if (version < 10)
        {
            Check(path, true, true, true, true, false);
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            // S2-08 only adds its own two tables. yeu_cau_thue belongs to S2-06 and is
            // installed separately by RentalRequestSchema, so nothing here touches it and
            // neither table declares a foreign key into it.
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS yeu_cau_thue_lich_su (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    yeu_cau_thue_id INTEGER NULL,
                    trang_thai_cu TEXT CHECK(length(trang_thai_cu) <= 25),
                    trang_thai_moi TEXT NOT NULL CHECK(length(trang_thai_moi) <= 25),
                    hanh_dong TEXT NOT NULL CHECK(length(hanh_dong) <= 40),
                    nguoi_thuc_hien_id INTEGER NULL,
                    ten_nguoi_thuc_hien TEXT CHECK(length(ten_nguoi_thuc_hien) <= 100),
                    vai_tro_luc_thuc_hien TEXT CHECK(length(vai_tro_luc_thuc_hien) <= 20),
                    lich_hen_cu TEXT NULL,
                    lich_hen_moi TEXT NULL,
                    ly_do_tu_choi TEXT CHECK(length(ly_do_tu_choi) <= 30),
                    ghi_chu_tu_choi TEXT CHECK(length(ghi_chu_tu_choi) <= 500),
                    thoi_diem TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_yeu_cau_thue_lich_su ON yeu_cau_thue_lich_su(yeu_cau_thue_id, thoi_diem);
                CREATE TABLE IF NOT EXISTS yeu_cau_thue_thong_bao (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    yeu_cau_thue_id INTEGER NULL,
                    nguoi_nhan_id INTEGER NOT NULL,
                    loai TEXT NOT NULL CHECK(length(loai) <= 40),
                    tieu_de TEXT NOT NULL CHECK(length(tieu_de) <= 200),
                    noi_dung TEXT NOT NULL CHECK(length(noi_dung) <= 500),
                    duong_dan TEXT CHECK(length(duong_dan) <= 200),
                    da_doc INTEGER NOT NULL DEFAULT 0 CHECK(da_doc IN (0,1)),
                    ngay_tao TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_yeu_cau_thue_thong_bao_nguoi ON yeu_cau_thue_thong_bao(nguoi_nhan_id, da_doc, ngay_tao);
                INSERT INTO app_schema_version(version,applied_at) VALUES(10,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.ExecuteNonQuery();
            tx.Commit();
        }
        if (version < 11)
        {
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS yeu_cau (id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ma_yeu_cau TEXT NOT NULL UNIQUE, khach_thue_id INTEGER NOT NULL REFERENCES khach_thue(id) ON DELETE RESTRICT, phong_id INTEGER NOT NULL REFERENCES phong_tro(id) ON DELETE RESTRICT, toa_nha_id INTEGER NOT NULL REFERENCES toa_nha(id) ON DELETE RESTRICT, loai_yeu_cau TEXT NOT NULL, ngay_mong_muon TEXT, trang_thai TEXT NOT NULL DEFAULT 'MOI', ngay_tao TEXT NOT NULL);
                CREATE INDEX IF NOT EXISTS ix_yeu_cau_toa_nha_ngay_tao ON yeu_cau(toa_nha_id, ngay_tao);
                INSERT INTO app_schema_version(version,applied_at) VALUES(11,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.ExecuteNonQuery();
            tx.Commit();
        }
        if (version < 12)
        {
            Check(path, true, true, true, true, false);
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            AddColumnIfMissing("dang_cho_xoa", "ALTER TABLE anh_phong ADD COLUMN dang_cho_xoa INTEGER NOT NULL DEFAULT 0 CHECK(dang_cho_xoa IN (0,1))");
            AddColumnIfMissing("loi_xoa_gan_nhat", "ALTER TABLE anh_phong ADD COLUMN loi_xoa_gan_nhat TEXT CHECK(loi_xoa_gan_nhat IS NULL OR length(loi_xoa_gan_nhat) <= 1000)");
            AddColumnIfMissing("lan_thu_xoa_gan_nhat", "ALTER TABLE anh_phong ADD COLUMN lan_thu_xoa_gan_nhat TEXT");
            command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(12,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
            command.ExecuteNonQuery();
            tx.Commit();

            void AddColumnIfMissing(string column, string sql)
            {
                command.Parameters.Clear();
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('anh_phong') WHERE name=$column";
                command.Parameters.AddWithValue("$column", column);
                if (Convert.ToInt64(command.ExecuteScalar()) != 0)
                    return;
                command.Parameters.Clear();
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
        if (version < 13)
        {
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            command.CommandText = """
                UPDATE role_permission
                SET AccessLevel='WRITE'
                WHERE RoleCode='QUAN_LY' AND ModuleCode='TIN_DANG' AND AccessLevel='READ';
                INSERT INTO app_schema_version(version,applied_at) VALUES(13,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.ExecuteNonQuery();
            tx.Commit();
        }
        if (version < 14) ContractSchema.Upgrade(c);
        if (version < 15) ContractCompletionSchema.Upgrade(c);
        if (version < 16) RoommateSchema.Upgrade(c);
        if (version < 17 && HasTable(c, "chi_so_dien_nuoc")) MeterReadingSchema.Upgrade(c, recordVersion: false);
        if (version < 17)
        {
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            var additions = new Dictionary<string, string>
            {
                ["dien_tich_dat"] = "TEXT NULL CHECK(dien_tich_dat IS NULL OR CAST(dien_tich_dat AS REAL) > 0)",
                ["thang_may"] = "INTEGER NOT NULL DEFAULT 0 CHECK(thang_may IN (0,1))",
                ["bai_do_xe"] = "INTEGER NOT NULL DEFAULT 0 CHECK(bai_do_xe IN (0,1))",
                ["camera_an_ninh"] = "INTEGER NOT NULL DEFAULT 0 CHECK(camera_an_ninh IN (0,1))",
                ["bao_ve_24h"] = "INTEGER NOT NULL DEFAULT 0 CHECK(bao_ve_24h IN (0,1))",
                ["khu_giat_say"] = "INTEGER NOT NULL DEFAULT 0 CHECK(khu_giat_say IN (0,1))",
                ["san_thuong"] = "INTEGER NOT NULL DEFAULT 0 CHECK(san_thuong IN (0,1))",
            };
            foreach (var addition in additions)
            {
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('toa_nha') WHERE name=$name";
                command.Parameters.AddWithValue("$name", addition.Key);
                var exists = Convert.ToInt64(command.ExecuteScalar()) != 0;
                command.Parameters.Clear();
                if (exists) continue;
                command.CommandText = $"ALTER TABLE toa_nha ADD COLUMN {addition.Key} {addition.Value}";
                command.ExecuteNonQuery();
            }
            command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(17,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
            command.ExecuteNonQuery();
            tx.Commit();
        }
        if (version < 19)
        {
            MeterReadingSchema.Upgrade(c, recordVersion: false);
            MeterAnomalySchema.Upgrade(c, recordVersion: false);
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            var additions = new Dictionary<string, string>
            {
                ["dien_tich_dat"] = "TEXT NULL CHECK(dien_tich_dat IS NULL OR CAST(dien_tich_dat AS REAL) > 0)",
                ["thang_may"] = "INTEGER NOT NULL DEFAULT 0 CHECK(thang_may IN (0,1))",
                ["bai_do_xe"] = "INTEGER NOT NULL DEFAULT 0 CHECK(bai_do_xe IN (0,1))",
                ["camera_an_ninh"] = "INTEGER NOT NULL DEFAULT 0 CHECK(camera_an_ninh IN (0,1))",
                ["bao_ve_24h"] = "INTEGER NOT NULL DEFAULT 0 CHECK(bao_ve_24h IN (0,1))",
                ["khu_giat_say"] = "INTEGER NOT NULL DEFAULT 0 CHECK(khu_giat_say IN (0,1))",
                ["san_thuong"] = "INTEGER NOT NULL DEFAULT 0 CHECK(san_thuong IN (0,1))",
            };
            foreach (var addition in additions)
            {
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('toa_nha') WHERE name=$name";
                command.Parameters.AddWithValue("$name", addition.Key);
                var exists = Convert.ToInt64(command.ExecuteScalar()) != 0;
                command.Parameters.Clear();
                if (exists) continue;
                command.CommandText = $"ALTER TABLE toa_nha ADD COLUMN {addition.Key} {addition.Value}";
                command.ExecuteNonQuery();
            }
            command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(19,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
            command.ExecuteNonQuery();
            tx.Commit();
        }
        if (version < 20) InvoiceIssueDateSchema.Upgrade(c);
        if (version < 21) InvoiceNotificationSchema.Upgrade(c);
        if (version < 22) InvoiceCancellationSchema.Upgrade(c);
        if (version < 23) InvoicePaymentSchema.Upgrade(c);
        if (version < 24) RoomDetailsSchema.Upgrade(c);
        if (version < 25) RoomNotesSchema.Upgrade(c);
        if (version < 26) InvoiceCollectionSchema.Upgrade(c);
        if (version < 27) InvoiceCollectionCancellationSchema.Upgrade(c);
        if (version < 28) BaoHongSchema.Upgrade(c);
        if (version < 29) AnhBaoHongSchema.Upgrade(c);
        if (version < 30) BaoHongTraoDoiSchema.Upgrade(c);
        if (version < 31) TatToanSchema.Upgrade(c);
        if (version < 32) ChamDutHopDongSchema.Upgrade(c);
        Check(path);
        Console.WriteLine($"Database updated to version {CurrentVersion}. Existing business rows preserved.");
    }
}
