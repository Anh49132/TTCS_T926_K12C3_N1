using System.Text.Json;
using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace QL_PhongTro.Data;

// Explicit, disposable demo creation. Never opens a user's existing database.
public static class Sprint2DemoSeeder
{
    public static async Task CreateAsync(string folder, string seedPath, string password, string url)
    {
        if (Directory.Exists(folder) || File.Exists(folder))
            throw new IOException("Demo directory already exists; select a new directory: " + folder);
        if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ArgumentException("Demo password needs at least 8 characters, letters and digits.");
        Directory.CreateDirectory(folder);
        var database = Path.Combine(folder, "sprint2.sqlite");
        LocalDatabaseInitializer.Create(database, seedPath);
        RequestDemoSeeder.Seed(database, password);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = database, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true }.ToString());
        connection.Open();
        object? Run(string sql, params (string Key, object? Value)[] values)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (key, value) in values) command.Parameters.AddWithValue(key, value ?? DBNull.Value);
            return command.ExecuteScalar();
        }
        int Id(string sql, params (string Key, object? Value)[] values) => Convert.ToInt32(Run(sql, values));
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        var month = new DateOnly(today.Year, today.Month, 1);
        var start = month.AddMonths(-2).ToString("yyyy-MM-dd");
        var stamp = now.ToString("O");
        var owner = Id("SELECT id FROM tai_khoan WHERE email=$email", ("$email", RequestDemoSeeder.OwnerEmail));
        var tenant = Id("SELECT id FROM khach_thue WHERE tai_khoan_id=(SELECT id FROM tai_khoan WHERE email=$email)", ("$email", RequestDemoSeeder.TenantEmail));
        var tenant2 = Id("SELECT id FROM khach_thue WHERE tai_khoan_id=(SELECT id FROM tai_khoan WHERE email=$email)", ("$email", RequestDemoSeeder.Tenant2Email));
        var alpha = Id("SELECT id FROM toa_nha WHERE ten_toa_nha='Demo Tòa Alpha'");
        var beta = Id("SELECT id FROM toa_nha WHERE ten_toa_nha='Demo Tòa Beta'");
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        int Account(string email, string name, string phone, string role) => Id("""
            INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,
            must_change_password,email_confirmed,is_deleted,ngay_tao,ngay_cap_nhat)
            VALUES($name,$email,$phone,$hash,$role,1,0,0,0,1,0,$now,$now) RETURNING id
            """, ("$name", name), ("$email", email), ("$phone", phone), ("$hash", hash), ("$role", role), ("$now", stamp));
        Account("admin.demo@demo.local", "Admin Sprint 2", "0900000104", "ADMIN");
        var otherOwner = Account("owner.b.demo@demo.local", "Chủ nhà B", "0900000105", "CHU_NHA");
        var manager = Account("manager.demo@demo.local", "Quản lý A1", "0900000106", "QUAN_LY");
        var tenant3Account = Account("tenant3.demo@demo.local", "Khách thuê 3", "0900000107", "KHACH_THUE");
        Run("INSERT INTO khach_thue(tai_khoan_id,ho_ten,so_dien_thoai,ngay_tao) VALUES($account,'Khách thuê 3','0912345680',$now)", ("$account", tenant3Account), ("$now", stamp));
        Run("UPDATE toa_nha SET quan_ly_id=$manager WHERE id=$building", ("$manager", manager), ("$building", alpha));
        var otherBuilding = Id("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,phuong_xa,quan_huyen,tinh_thanh,so_tang,dang_hoat_dong) VALUES($owner,'Tòa B1','99 Đường Demo','Phường Tân Quy','Quận 7','TP.HCM',4,1) RETURNING id", ("$owner", otherOwner));
        var otherBuilding2 = Id("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,phuong_xa,quan_huyen,tinh_thanh,so_tang,dang_hoat_dong) VALUES($owner,'Tòa B2','88 Đường Demo','Phường 25','Bình Thạnh','TP.HCM',3,1) RETURNING id", ("$owner", otherOwner));
        int Room(int building, string code, int floor = 1, string state = "TRONG", long rent = 3500000, int capacity = 3) => Id("""
            INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,mo_ta,ngay_tao)
            VALUES($building,$code,$floor,25,$rent,$rent,$capacity,$state,'Phòng mẫu sáng thoáng, có cửa sổ và khu bếp riêng.',$now) RETURNING id
            """, ("$building", building), ("$code", code), ("$floor", floor), ("$rent", rent), ("$capacity", capacity), ("$state", state), ("$now", stamp));
        int Listing(int room, string title, string state = "DANG_HIEN_THI", bool expired = false, int? account = null) => Id("""
            INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,ngay_dang,ngay_het_han,trang_thai,ngay_tao)
            VALUES($room,$owner,$title,'Tin mẫu để kiểm thử Sprint 2. Chi phí được lấy từ dịch vụ thực sự gán cho phòng.',$now,$expiry,$state,$now) RETURNING id
            """, ("$room", room), ("$owner", account ?? owner), ("$title", title), ("$now", stamp), ("$expiry", now.AddDays(expired ? -1 : 30).ToString("O")), ("$state", state));
        var a101 = Id("SELECT id FROM phong_tro WHERE ma_phong='A101-DEMO'");
        var a102 = Id("SELECT id FROM phong_tro WHERE ma_phong='A102-DEMO'");
        var b202 = Id("SELECT id FROM phong_tro WHERE ma_phong='B202-DEMO'");
        Run("UPDATE phong_tro SET tang=0 WHERE id=$id", ("$id", a101));
        var blank = Room(alpha, "A103-TAO-TIN");
        var upload = Room(alpha, "A104-UPLOAD");
        var invoiceRoom = Room(alpha, "C001-HOA-DON", 0, "DANG_THUE");
        var invoiceRoomB = Room(alpha, "C002-KHONG-GUI-XE", 2, "DANG_THUE");
        var draftRoom = Room(alpha, "A105-NHAP");
        var draftListing = Listing(draftRoom, "Bản nháp phòng A105", "NHAP");
        var expiredRoom = Room(alpha, "A106-HET-HAN");
        var expiredListing = Listing(expiredRoom, "Tin đã hết hạn phòng A106", expired: true);
        var hiddenRoom = Room(alpha, "A107-TAM-AN");
        var hiddenListing = Listing(hiddenRoom, "Tin tạm ẩn phòng A107", "TAM_AN");
        var otherRoom = Room(otherBuilding, "OTHER-01");
        var otherListing = Listing(otherRoom, "Tin của chủ nhà khác", account: otherOwner);
        var availableRoom = Room(alpha, "A108-GUI-YEU-CAU", capacity: 3, rent: 2500000);
        var availableListing = Listing(availableRoom, "Phòng A108 — sức chứa 3 người, thử gửi yêu cầu");
        // 35 public listings: three pages at 12 items/page, with 11 on the last page.
        // Together with six excluded listings this keeps the acceptance fixture small and auditable.
        for (var i = 1; i <= 33; i++)
        {
            var building = (i % 4) switch { 0 => alpha, 1 => beta, 2 => otherBuilding, _ => otherBuilding2 };
            var room = Room(building, $"SEARCH-{i:000}", 1 + i % 4, rent: 1500000 + i % 12 * 250000, capacity: 1 + i % 4);
            Run("UPDATE phong_tro SET dien_tich=$area WHERE id=$room", ("$area", 15 + i % 20), ("$room", room));
            var state = i == 4 ? "TAM_AN" : i == 5 ? "NHAP" : "DANG_HIEN_THI";
            var publisher = building == alpha || building == beta ? owner : otherOwner;
            var id = Listing(room, $"SEARCH-{i:000} — phòng mẫu", state, account: publisher);
            Run("UPDATE tin_dang SET ngay_dang=$date WHERE id=$id", ("$date", now.AddMinutes(-i).ToString("O")), ("$id", id));
        }
        int Service(string code, string name) => Id("INSERT INTO dich_vu(ma_dich_vu,ten_dich_vu,dang_hoat_dong) VALUES($code,$name,1) RETURNING id", ("$code", code), ("$name", name));
        var electricity = Service("DIEN", "Điện");
        var water = Service("NUOC", "Nước");
        var trash = Service("RAC", "Rác");
        var internet = Service("INTERNET", "Internet");
        var parking = Service("GUI_XE", "Gửi xe");
        var catalogs = new Dictionary<(int, int), int>();
        foreach (var building in new[] { alpha })
        {
            foreach (var service in new[] { electricity, water, trash, parking, internet })
            {
                catalogs[(building, service)] = Id("INSERT INTO dich_vu_toa_nha(toa_nha_id,dich_vu_id,ap_dung_mac_dinh) VALUES($building,$service,1) RETURNING id", ("$building", building), ("$service", service));
                var method = service == electricity ? "THEO_CHI_SO" : service == water ? "THEO_NGUOI" : "CO_DINH";
                var unit = service == electricity ? "kWh" : service == water ? "người/tháng" : "phòng/tháng";
                var rate = service == electricity ? 4000 : service == water ? 80000 : service == trash ? 50000 : service == internet ? 150000 : 100000;
                Run("""
                    INSERT INTO cau_hinh_dich_vu(toa_nha_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,dang_ap_dung,da_chot_gia,nguoi_tao_id,ngay_tao)
                    VALUES($building,$service,$method,$unit,$rate,$start,1,1,$owner,$now)
                    """, ("$building", building), ("$service", service), ("$method", method), ("$unit", unit), ("$rate", rate), ("$start", start), ("$owner", owner), ("$now", stamp));
            }
            Run("INSERT INTO khoi_tao_dich_vu(toa_nha_id,ngay_tao) VALUES($building,$now)", ("$building", building), ("$now", stamp));
        }
        foreach (var room in new[] { a101, a102, b202, blank, upload, invoiceRoom, invoiceRoomB, draftRoom, expiredRoom, hiddenRoom, availableRoom })
        {
            if (room == b202) continue; // A2 deliberately has no service configuration for S2-10.
            foreach (var service in new[] { electricity, water, trash, internet })
            {
                if (service == water && (room == invoiceRoom || room == invoiceRoomB))
                    continue;
                Run("INSERT INTO dich_vu_phong(phong_id,dich_vu_toa_nha_id) VALUES($room,$catalog)", ("$room", room), ("$catalog", catalogs[(alpha, service)]));
            }
            if (room == a101 || room == invoiceRoom)
                Run("INSERT INTO dich_vu_phong(phong_id,dich_vu_toa_nha_id,don_gia_rieng) VALUES($room,$catalog,70000)", ("$room", room), ("$catalog", catalogs[(alpha, parking)]));
        }
        Run("UPDATE dich_vu_phong SET don_gia_rieng=4200 WHERE phong_id=$room AND dich_vu_toa_nha_id=$catalog", ("$room", a101), ("$catalog", catalogs[(alpha, electricity)]));
        Run("UPDATE cau_hinh_dich_vu SET den_ngay=$end WHERE toa_nha_id=$building AND dich_vu_id=$service", ("$end", month.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd")), ("$building", alpha), ("$service", water));
        Run("INSERT INTO cau_hinh_dich_vu(toa_nha_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,dang_ap_dung,da_chot_gia,nguoi_tao_id,ngay_tao) VALUES($building,$service,'THEO_NGUOI','người/tháng',90000,$from,1,1,$owner,$now)",
            ("$building", alpha), ("$service", water), ("$from", month.AddMonths(1).ToString("yyyy-MM-dd")), ("$owner", owner), ("$now", stamp));
        var prefix = $"YC-{today:yyyyMM}-";
        for (var i = 1; i <= 4; i++) Run("UPDATE yeu_cau_thue SET ma_yeu_cau=$code WHERE id=$id", ("$code", prefix + i.ToString("0000")), ("$id", i));
        for (var i = 5; i <= 7; i++)
        {
            var listing = i + 4;
            var state = i == 5 ? "TU_CHOI" : i == 6 ? "DA_HUY" : "DA_DUYET";
            Run("""
                INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,
                loi_nhan,trang_thai,ly_do_tu_choi,nguoi_xu_ly_id,ngay_xu_ly,ngay_tao)
                VALUES($code,$listing,$tenant,'THUE_NGAY',$desired,1,'Yêu cầu mẫu đã xử lý',$state,$reason,$owner,$now,$now)
                """, ("$code", prefix + i.ToString("0000")), ("$listing", listing), ("$tenant", tenant),
                ("$desired", today.AddDays(2).ToString("yyyy-MM-dd")), ("$state", state),
                ("$reason", i == 5 ? "KHONG_PHU_HOP_SO_NGUOI" : null), ("$owner", owner), ("$now", stamp));
            if (i == 7)
            {
                Run("UPDATE phong_tro SET trang_thai='DA_DAT_COC' WHERE id=(SELECT phong_id FROM tin_dang WHERE id=$id)", ("$id", listing));
                Run("UPDATE tin_dang SET trang_thai='DA_CHO_THUE' WHERE id=$id", ("$id", listing));
            }
        }
        Run("""
            INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,
            loi_nhan,trang_thai,ngay_tao)
            VALUES($code,$listing,$tenant,'XEM_PHONG',$desired,2,'Yêu cầu mở sẵn để thử gửi trùng','MOI',$now)
            """, ("$code", prefix + "0008"), ("$listing", availableListing), ("$tenant", tenant2),
            ("$desired", today.AddDays(2).ToString("yyyy-MM-dd")), ("$now", stamp));
        Run("INSERT INTO rental_request_counter(thang,so_cuoi) VALUES($month,8)", ("$month", today.ToString("yyyyMM")));
        // Add optional invoice schema using the same isolated fixture path as tests.
        if (Id("SELECT COUNT(*) FROM sqlite_master WHERE name='hop_dong'") == 0)
        {
            var script = File.ReadAllText(Path.Combine(Path.GetDirectoryName(seedPath)!, "..", "..", "docs", "sql", "S1-06-quan-he-thue.sql"));
            Run(script[script.IndexOf("CREATE TABLE hop_dong", StringComparison.Ordinal)..script.IndexOf("CREATE TABLE nguoi_o_ghep", StringComparison.Ordinal)]);
        }
        if (Id("SELECT COUNT(*) FROM sqlite_master WHERE name='hoa_don'") == 0) DichVuSchemaInitializer.InitializeInvoices(database);
        foreach (var room in new[] { invoiceRoom, invoiceRoomB })
        {
            var contract = Id("INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES($code,$room,$tenant,'DANG_HIEU_LUC',$owner,$now) RETURNING id", ("$code", room == invoiceRoom ? "HD-DEMO-C001" : "HD-DEMO-C002"), ("$room", room), ("$tenant", tenant), ("$owner", owner), ("$now", stamp));
            Run("INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES($contract,1,$from,$to,24,3500000,$owner,$now)", ("$contract", contract), ("$from", start), ("$to", month.AddMonths(21).AddDays(-1).ToString("yyyy-MM-dd")), ("$owner", owner), ("$now", stamp));
        }
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, owner.ToString()), new Claim(ClaimTypes.Role, "CHU_NHA")], "demo")) };
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + database).Options,
            new HttpContextAccessor { HttpContext = http });
        var prices = new DichVuService(db);
        var rooms = new DichVuPhongService(db, prices);
        var invoices = new List<int>();
        foreach (var room in new[] { invoiceRoom, invoiceRoomB })
        {
            var contract = Id("SELECT id FROM hop_dong WHERE phong_id=$room", ("$room", room));
            var input = new LapHoaDonDichVuViewModel { ToaNhaId = alpha, HopDongId = contract, NgayApDung = month.AddMonths(-1), SoNguoi = 2 };
            foreach (var service in new[] { internet, parking })
            {
                var price = await rooms.LayGiaHoaDonAsync(owner, room, service, input.NgayApDung.Value);
                if (price is not null) input.Dong.Add(new DongDichVuInput { Chon = true, DichVuId = service, CauHinhId = price.CauHinhId, DonGiaDaXem = price.DonGia });
            }
            invoices.Add(await new HoaDonDichVuService(db, prices).PhatHanhAsync(owner, input));
        }
        var samples = Path.Combine(folder, "sample-images");
        Directory.CreateDirectory(samples);
        for (var i = 1; i <= 10; i++)
        {
            using var image = new Image<Rgba32>(1200, 800, new Rgba32((byte)(25 + i * 20), (byte)(220 - i * 15), (byte)(60 + i * 12)));
            var path = Path.Combine(samples, $"room-{i:00}.png");
            await image.SaveAsPngAsync(path);
            if (i == 1) await image.SaveAsJpegAsync(Path.Combine(samples, "room-jpg.jpg"));
            if (i == 9) File.Copy(path, Path.Combine(samples, "09-ninth-image.png"));
        }
        File.Copy(Path.Combine(samples, "room-01.png"), Path.Combine(samples, "valid-small.png"));
        File.Copy(Path.Combine(samples, "room-jpg.jpg"), Path.Combine(samples, "valid-small.jpg"));
        await using (var oversized = new FileStream(Path.Combine(samples, "over-5mb.png"), FileMode.CreateNew, FileAccess.Write))
        {
            var valid = await File.ReadAllBytesAsync(Path.Combine(samples, "room-01.png"));
            await oversized.WriteAsync(valid);
            oversized.SetLength(5L * 1024 * 1024 + 1);
        }
        await File.WriteAllTextAsync(Path.Combine(samples, "not-an-image.txt"), "Tệp mẫu không phải ảnh.");
        await File.WriteAllTextAsync(Path.Combine(samples, "fake-content.jpg"), "Nội dung giả, phải bị từ chối dù có đuôi JPG.");
        if (Run("PRAGMA integrity_check")?.ToString() != "ok") throw new InvalidOperationException("Demo integrity check failed.");
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "PRAGMA foreign_key_check";
            using var reader = check.ExecuteReader();
            if (reader.Read()) throw new InvalidOperationException("Demo foreign key check failed.");
        }
        var manifest = new
        {
            createdAt = now, database, directory = folder, url, password,
            roomImagesPath = Path.Combine(folder, "uploads", "rooms"), sampleImages = samples,
            accounts = new[] { new { email = "admin.demo@demo.local", role = "ADMIN", label = "ADMIN" },
                new { email = RequestDemoSeeder.OwnerEmail, role = "CHU_NHA", label = "CHU_NHA A" },
                new { email = "owner.b.demo@demo.local", role = "CHU_NHA", label = "CHU_NHA B" },
                new { email = "manager.demo@demo.local", role = "QUAN_LY", label = "QUAN_LY" },
                new { email = RequestDemoSeeder.TenantEmail, role = "KHACH_THUE", label = "KHACH_THUE 1" },
                new { email = RequestDemoSeeder.Tenant2Email, role = "KHACH_THUE", label = "KHACH_THUE 2" },
                new { email = "tenant3.demo@demo.local", role = "KHACH_THUE", label = "KHACH_THUE 3" } },
            buildings = new { a1 = alpha, a2 = beta, b1 = otherBuilding, b2 = otherBuilding2 },
            rooms = new { a101, a102, b202, blank, upload, invoiceRoom, invoiceRoomB, draftRoom, expiredRoom, hiddenRoom, availableRoom },
            listings = new { draftListing, expiredListing, hiddenListing, otherListing, availableListing },
            requests = new { instant = 1, confirm = 2, clash = 3, reject = 4, rejected = 5, cancelled = 6, approved = 7 },
            clashVietnam = DateTime.Parse(Run("SELECT lich_hen FROM yeu_cau_thue WHERE id=3")!.ToString()!,
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal)
                .AddHours(7).ToString("dd/MM/yyyy HH:mm"),
            currentPeriod = month.ToString("MM/yyyy"), nextPeriod = month.AddMonths(1).ToString("MM/yyyy"),
            invoicePeriod = month.AddMonths(-1).ToString("MM/yyyy"), invoices,
            listingCount = Id("SELECT COUNT(*) FROM tin_dang"), publicListingCount = 35, excludedListingCount = 6,
            imageUploadRoom = a101, emptyImageRoom = upload, duplicateRequestListing = availableListing,
            integrity = "ok", foreignKeys = "ok"
        };
        await File.WriteAllTextAsync(Path.Combine(folder, "access.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        var accountRows = string.Join(Environment.NewLine, manifest.accounts.Select(x => $"| {x.label} | `{x.email}` | `{password}` | {x.role} |"));
        await File.WriteAllTextAsync(Path.Combine(folder, "report.md"), $"""
            # Báo cáo dữ liệu mẫu Sprint 2

            Database tách biệt: `{database}`
            URL: `{url}`
            Thư mục ảnh: `{Path.Combine(folder, "uploads", "rooms")}`

            | Tài khoản | Email | Mật khẩu chung | Role |
            | --- | --- | --- | --- |
            {accountRows}

            - S2-01: A1 là tòa `{alpha}`; phòng tầng trệt `{a101}`, phòng tầng trên `{a102}`, hóa đơn cũ `{invoices[0]}`.
            - S2-02: phòng upload `{a101}` (được nạp 5 ảnh qua HTTP sau khi seed); phòng không ảnh `{upload}`; file thử ở `{samples}`.
            - S2-03: phòng tạo tin `{blank}`, phòng đang thuê `{invoiceRoom}`; tin nháp `{draftListing}`, hết hạn `{expiredListing}`, tạm ẩn `{hiddenListing}`.
            - S2-04: 35 tin công khai hợp lệ ở 4 quận/huyện, 6 tin bị loại; 12 tin/trang cho kết quả 12/12/11.
            - S2-05: tin chi tiết `1`, có giá riêng điện, các khoản cố định và tiền cọc.
            - S2-06: phòng `{availableRoom}` sức chứa 3; tin `{availableListing}` đã có yêu cầu mở của KHACH_THUE 2, KHACH_THUE 1 chưa có yêu cầu trên tin này.
            - S2-07/08/09: yêu cầu 1–8 đủ 5 trạng thái; yêu cầu 2 và 3 cùng phòng, lịch cách 15 phút.
            - S2-10: A1 có điện theo chỉ số, nước theo người và cấu hình nước chờ kỳ `{month.AddMonths(1):MM/yyyy}`; A2 `{beta}` chưa cấu hình.

            File này và `access.json` nằm dưới `data/`, đã được Git ignore. Không chia sẻ mật khẩu này cho môi trường thật.
            """);
        Console.WriteLine("Sprint 2 demo ready: " + folder);
        Console.WriteLine("Accounts and password: " + Path.Combine(folder, "access.json"));
    }
}
