using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;

internal static class DemoPreparation
{
    public static async Task PrepareAsync(AppDbContext db, string root, string database, string runDir, TaiKhoan owner, ToaNha building, string password)
    {
        await db.Database.ExecuteSqlRawAsync(File.ReadAllText(Path.Combine(root, "docs/sql/S1-06-khach-thue.sql")));
        var rentals = File.ReadAllText(Path.Combine(root, "docs/sql/S1-06-quan-he-thue.sql"));
        rentals = rentals[rentals.IndexOf("CREATE TABLE hop_dong", StringComparison.Ordinal)..].Replace("COMMIT;", "");
        await db.Database.ExecuteSqlRawAsync(rentals);
        DichVuSchemaInitializer.InitializeInvoices(database);
        var profile = new KhachThue { HoTen = "Khách demo S1-09", NgayTao = DateTime.UtcNow };
        var room = new PhongTro { ToaNhaId = building.Id, MaPhong = "DEMO-101", Tang = 1, DienTich = 25,
            GiaThue = 2000000, SoNguoiToiDa = 4, TrangThai = "DANG_THUE", NgayTao = DateTime.UtcNow };
        db.KhachThues.Add(profile); db.PhongTros.Add(room); await db.SaveChangesAsync();
        var contractCode = "HD-DEMO-" + Guid.NewGuid().ToString("N")[..8];
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES({contractCode},{room.Id},{profile.Id},{"DANG_HIEU_LUC"},{owner.Id},{DateTime.UtcNow})");
        var contract = await db.HopDongs.SingleAsync(x => x.MaHopDong == contractCode);
        var today = DichVuService.HomNay(); var from = new DateOnly(today.Year, today.Month, 1); var end = from.AddYears(2).AddDays(-1);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES({contract.Id},1,{from},{end},24,2000000,{owner.Id},{DateTime.UtcNow})");
        // Leave defaults unseeded so first opening the list demonstrates automatic initialization.
        var instructions = $"""
            DB kiểm thử: {database}
            Email chủ nhà: {owner.Email}
            Mật khẩu demo: {password}
            Tòa nhà: {building.TenToaNha} (ID {building.Id})
            Phòng: DEMO-101; giá phòng 2.000.000 VND/tháng; tối đa 4 người.
            Hợp đồng: {contractCode}; thuê trọn tháng trong 24 tháng.
            Chỉ sử dụng tài khoản này trên DB kiểm thử. Không có dữ liệu demo ghi vào DB gốc.
            Lệnh chạy từ thư mục gốc repo:
            dotnet run --project QL_PhongTro/QL_PhongTro.csproj --no-build -- --DatabasePath="{database}" --urls=http://localhost:5247
            Mở http://localhost:5247/Account/Login, đăng nhập rồi mở /DichVu.
            """;
        await File.WriteAllTextAsync(Path.Combine(runDir, "demo-access.txt"), instructions);
        Console.WriteLine("DEMO PREPARED (not a test PASS):");
        Console.WriteLine(instructions);
        Console.WriteLine("Thông tin đã lưu tại: " + Path.Combine(runDir, "demo-access.txt"));
    }
}
