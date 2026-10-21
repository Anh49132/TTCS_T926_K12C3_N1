using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

// Only create a fresh synthetic database. Never open/copy the user's local database.
var root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
var app = Path.Combine(root, "QL_PhongTro");
var folder = Path.Combine(root, "data", "service-removal-demo", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
Directory.CreateDirectory(folder);
var path = Path.Combine(folder, "demo.sqlite");
LocalDatabaseInitializer.Create(path, Path.Combine(app, "Data", "permissions.seed.json"));
var password = "Demo-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) + "a!";
using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true }.ToString());
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = """
    INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
    VALUES(1,'Demo Owner','owner@example.test','0900000001',$hash,'CHU_NHA',1,1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP);
    INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(1,1,'Demo ngừng dịch vụ','Dữ liệu giả để kiểm thử');
    INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(1,'Khách demo',CURRENT_TIMESTAMP);
    """;
command.Parameters.AddWithValue("$hash", BCrypt.Net.BCrypt.HashPassword(password));
command.ExecuteNonQuery();
command.Parameters.Clear();
var rentals = File.ReadAllText(Path.Combine(root, "docs", "sql", "S1-06-quan-he-thue.sql"));
command.CommandText = rentals[rentals.IndexOf("CREATE TABLE hop_dong", StringComparison.Ordinal)..rentals.IndexOf("CREATE TABLE nguoi_o_ghep", StringComparison.Ordinal)];
command.ExecuteNonQuery();
DichVuSchemaInitializer.InitializeInvoices(path);
var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "CHU_NHA")], "demo")) } };
using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options, accessor);
var prices = new DichVuService(db);
await prices.ThemAsync(1, new TaoDichVuViewModel
{ ToaNhaId = 1, TenDichVu = "Gửi xe", CachTinh = CachTinhDichVu.CoDinh, DonViTinh = "phòng/tháng", DonGia = 100000, ApDungMacDinh = true });
var today = DichVuService.HomNay();
var current = new DateOnly(today.Year, today.Month, 1);
var previous = current.AddMonths(-1);
var next = current.AddMonths(1);
await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE cau_hinh_dich_vu SET tu_ngay={previous.ToString("yyyy-MM-dd")}");
var roomServices = new DichVuPhongService(db, prices);
var rooms = new[] { "DEMO-A", "DEMO-B" }.Select(code => new PhongTro
{ ToaNhaId = 1, MaPhong = code, Tang = 1, DienTich = 20, GiaThue = 2000000, SoNguoiToiDa = 2, TrangThai = "DANG_THUE", NgayTao = DateTime.UtcNow }).ToArray();
db.PhongTros.AddRange(rooms);
await roomServices.GanMacDinhChoPhongMoiAsync(rooms);
await db.SaveChangesAsync();
var invoices = new HoaDonDichVuService(db, prices);
var oldInvoices = new List<int>();
var serviceId = await db.DichVus.Select(x => x.Id).SingleAsync();
foreach (var room in rooms)
{
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES({room.Id},{"HD-" + room.MaPhong},{room.Id},1,'DANG_HIEU_LUC',1,{DateTime.UtcNow})");
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES({room.Id},1,{previous.ToString("yyyy-MM-dd")},{current.AddYears(2).AddDays(-1).ToString("yyyy-MM-dd")},25,2000000,1,{DateTime.UtcNow})");
    var price = (await roomServices.LayGiaHoaDonAsync(1, room.Id, serviceId, previous))!;
    oldInvoices.Add(await invoices.PhatHanhAsync(1, new LapHoaDonDichVuViewModel
    {
        ToaNhaId = 1, HopDongId = room.Id, NgayApDung = previous, SoNguoi = 1,
        Dong = [new DongDichVuInput { Chon = true, DichVuId = serviceId, CauHinhId = price.CauHinhId, DonGiaDaXem = price.DonGia }]
    }));
}
DatabaseUpdates.Check(path);
if (await db.HoaDons.CountAsync(x => x.TongTien == 2100000 && x.TrangThai == "DA_PHAT_HANH") != 2)
    throw new InvalidOperationException("Demo invoice verification failed.");
var access = Path.Combine(folder, "access.json");
File.WriteAllText(access, JsonSerializer.Serialize(new
{
    database = path, email = "owner@example.test", password, building = 1,
    roomA = rooms[0].Id, roomB = rooms[1].Id, previousInvoiceA = oldInvoices[0], previousInvoiceB = oldInvoices[1],
    previousMonth = previous.ToString("yyyy-MM-dd"), currentMonth = current.ToString("yyyy-MM-dd"), nextMonth = next.ToString("yyyy-MM-dd")
}, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.Combine(root, "data", "service-removal-demo", "latest.txt"), access);
Console.WriteLine("Demo ready. Private credentials and dates: " + access);
Console.WriteLine("Expected: old/current A = 2,100,000 VND; next A after removal = 2,000,000; unchanged B = 2,100,000.");
