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

if(args.Length is <2 or >3)throw new ArgumentException("Usage: S404Demo <application-directory> <new-demo-directory>");
var task5=args.Length==3 && args[2]=="task5";
var task3=args.Length==3 && args[2] is "task3" or "task4" or "task5" or "s4052";
var app=Path.GetFullPath(args[0]);var folder=Path.GetFullPath(args[1]);
if(Directory.Exists(folder))throw new IOException("Refusing to overwrite an existing demo directory.");
Directory.CreateDirectory(folder);var path=Path.Combine(folder,"s404.sqlite");
LocalDatabaseInitializer.Create(path,Path.Combine(app,"Data","permissions.seed.json"));
RentalRequestSchema.Initialize(path);DichVuSchemaInitializer.InitializeInvoices(path);
var password=Convert.ToHexString(RandomNumberGenerator.GetBytes(12))+"a1!";
using(var c=new SqliteConnection($"Data Source={path};Foreign Keys=True;Pooling=False"))
{
    c.Open();using var cmd=c.CreateCommand();cmd.Parameters.AddWithValue("$hash",BCrypt.Net.BCrypt.HashPassword(password));
    cmd.CommandText="""
        INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
        VALUES(1,'Chủ nhà demo S4-04','owner.s404@demo.local','0900404001',$hash,'CHU_NHA',1,1,'2026-01-01','2026-01-01'),
        (2,'Chủ nhà khác S4-04','other.s404@demo.local','0900404002',$hash,'CHU_NHA',1,1,'2026-01-01','2026-01-01'),
        (3,'Khách thuê demo S4-04','tenant.s404@demo.local','0900404003',$hash,'KHACH_THUE',1,1,'2026-01-01','2026-01-01'),
        (4,'Khách thuê khác S4-04','foreign.s404@demo.local','0900404004',$hash,'KHACH_THUE',1,1,'2026-01-01','2026-01-01');
        INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(1,1,'Demo Tòa Alpha','1 Đường Demo A'),(2,2,'Demo Tòa Beta','2 Đường Demo B');
        INSERT INTO khach_thue(id,tai_khoan_id,ho_ten,ngay_tao) VALUES(1,3,'Khách thuê demo S4-04','2026-01-01'),(2,4,'Khách thuê khác S4-04','2026-01-01');
        """;cmd.ExecuteNonQuery();
}
var context=new HttpContextAccessor { HttpContext=new DefaultHttpContext { User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"1"),new Claim(ClaimTypes.Role,"CHU_NHA")],"demo")) } };
using(var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True;Pooling=False").Options,context))
{
    var room=new PhongTro {ToaNhaId=1,MaPhong="A103",Tang=1,DienTich=25,GiaThue=3500000,SoNguoiToiDa=3,TrangThai="DANG_THUE",NgayTao=DateTime.UtcNow};
    db.PhongTros.Add(room);await db.SaveChangesAsync();
    var h=new HopDongThamChieu {MaHopDong="HD-2026-0024",PhongId=room.Id,KhachDungTenId=1,TienCoc=7000000,TrangThai="DANG_HIEU_LUC",NgayChot=1,NguoiLapId=1,NgayTao=DateTime.UtcNow};
    db.HopDongs.Add(h);await db.SaveChangesAsync();
    db.KyHopDongs.Add(new() {HopDongId=h.Id,NgayBatDau=new(2026,1,1),NgayKetThuc=new(2027,12,31),GiaThue=3500000,SoThang=24,NguoiLapId=1,NgayTao=DateTime.UtcNow});await db.SaveChangesAsync();
    foreach(var (name,price,unit,code) in new[] {("Điện",4000L,"kWh","DIEN"),("Nước",20000L,"m³","NUOC")})
    {
        await new DichVuService(db).ThemAsync(1,new TaoDichVuViewModel {ToaNhaId=1,TenDichVu=name,DonGia=price,CachTinh=CachTinhDichVu.TheoChiSo,DonViTinh=unit,ApDungMacDinh=false});
        var catalog=await db.DichVuToaNhas.AsNoTracking().Include(x=>x.DichVu).SingleAsync(x=>x.DichVu.TenDichVu==name);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dich_vu SET ma_dich_vu={code} WHERE id={catalog.DichVuId}");
        await new DichVuPhongService(db,new DichVuService(db)).DatDichVuAsync(1,room.Id,catalog.Id,true);
    }
    await db.Database.ExecuteSqlRawAsync("UPDATE cau_hinh_dich_vu SET tu_ngay='2026-01-01'");db.ChangeTracker.Clear();
    db.HopDongChiSoDauKys.Add(new() {HopDongId=h.Id,NgayBanGiao=new(2026,9,30),ChiSoDien=1256,ChiSoNuoc=86,NguoiNhapId=1,NgayNhap=DateTime.UtcNow});
    var old=new HoaDon {MaHoaDon="HD-DEMO-202609",HopDongId=h.Id,Nam=2026,Thang=9,TuNgay=new(2026,9,1),DenNgay=new(2026,9,30),NgayChot=new(2026,9,30),SoNguoiTinhPhi=1,TongTien=1700000,HanThanhToan=new(2026,10,7),NguoiLapId=1,NgayLap=DateTime.UtcNow,ChiTiet=[new() {SoThuTu=1,LoaiKhoan="TIEN_PHONG",TenKhoan="Tiền phòng",SoLuong=1,DonGia=1700000,ThanhTien=1700000}]};
    db.HoaDons.Add(old);await db.SaveChangesAsync();old.TrangThai="DA_PHAT_HANH";old.NgayPhatHanh=DateTime.UtcNow;await db.SaveChangesAsync();
    db.ThanhToans.Add(new() {HoaDonId=old.Id,SoTien=500000,NgayThanhToan=new(2026,10,1),TrangThai="DA_XAC_NHAN"});
    db.GiaoDichCocs.Add(new() {Ma="COC-DEMO-01",KhoaChongTrung=Guid.NewGuid().ToString("N"),HopDongId=h.Id,SoTien=7000000,HinhThuc="CHUYEN_KHOAN",NguoiThucHienId=1,NgayGiaoDich=DateTime.UtcNow});await db.SaveChangesAsync();
    var clock=new DemoClock();var handover=new TraPhongService(db,clock);var input=new TraPhongInput {ApDungPhat=task3,TienPhat=task3?3500000:0,LyDo="KHACH_CHUYEN_DI",HopDongId=h.Id,NgayTra=new(2026,10,13),ChiSoDien=1330,ChiSoNuoc=91};
    input.DauVanTay=TraPhongService.DauVanTay(await handover.NhapAsync(1,input,true));await handover.LuuAsync(1,input);
    var settlement=new TatToanService(db,clock);var draft=await settlement.NhapAsync(1,h.Id);
    draft.Input.KhauTru=[new() {TenKhoan="Thay khóa cửa",SoTien=250000,GhiChu="Mất chìa khóa"},new() {TenKhoan="Sơn lại tường",SoTien=200000,GhiChu="Tường bị bẩn"}];
    await settlement.LuuAsync(1,draft.Input);
    if(args.Length==3 && args[2]=="s4052")
    {
        db.TinDangs.Add(new() {PhongId=room.Id,NguoiDangId=1,TieuDe="Phòng A103 có ban công — tin cũ",NoiDung="Mô tả được giữ nguyên từ tin cũ.\nPhòng thoáng, có ban công.\nNội dung thử nghiệm: giá cũ 3.000.000 đ/tháng; hãy kiểm tra giá ở trường giá phòng.",TrangThai="DA_CHO_THUE",NgayTao=clock.UtcNow.AddDays(-60),NgayDang=clock.UtcNow.AddDays(-59),NgayHetHan=clock.UtcNow.AddDays(-29)});
        for(var i=1;i<=8;i++)db.AnhPhongs.Add(new() {PhongId=room.Id,ThuTu=i,DuongDan="/images/hero-clean.jpg",DuongDanAnhNho="/images/hero-clean.jpg",MoTa=$"Ảnh minh họa demo {i}",NgayTao=clock.UtcNow});
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET gia_thue=4500000 WHERE id={room.Id}");
    }
    if(task5)
    {
        var ready=await settlement.KiemTraChotAsync(1,h.Id);
        ready.Confirm.DaKiemTra=true;ready.Confirm.DongY=true;
        await settlement.HoanTatAsync(1,ready.Confirm);
        var pdf=await settlement.PdfAsync(3,h.Id);
        await File.WriteAllBytesAsync(Path.Combine(folder,pdf.FileName),pdf.Content);
    }
    if(task3)
    {
        var extraRoom=new PhongTro {ToaNhaId=1,MaPhong="A104",Tang=1,DienTich=25,GiaThue=3500000,SoNguoiToiDa=3,TrangThai="DANG_THUE",NgayTao=DateTime.UtcNow};
        db.PhongTros.Add(extraRoom);await db.SaveChangesAsync();
        var extraContract=new HopDongThamChieu {MaHopDong="HD-2026-0025",PhongId=extraRoom.Id,KhachDungTenId=1,TienCoc=7000000,TrangThai="DANG_HIEU_LUC",NgayChot=1,NguoiLapId=1,NgayTao=DateTime.UtcNow};
        db.HopDongs.Add(extraContract);await db.SaveChangesAsync();
        db.KyHopDongs.Add(new() {HopDongId=extraContract.Id,NgayBatDau=new(2026,1,1),NgayKetThuc=new(2027,12,31),GiaThue=3500000,SoThang=24,NguoiLapId=1,NgayTao=DateTime.UtcNow});
        db.HopDongChiSoDauKys.Add(new() {HopDongId=extraContract.Id,NgayBanGiao=new(2026,9,30),ChiSoDien=1256,ChiSoNuoc=86,NguoiNhapId=1,NgayNhap=DateTime.UtcNow});await db.SaveChangesAsync();
        foreach(var service in await db.DichVuToaNhas.AsNoTracking().Where(x=>x.ToaNhaId==1).ToListAsync())
            await new DichVuPhongService(db,new DichVuService(db)).DatDichVuAsync(1,extraRoom.Id,service.Id,true);
    }

}
DatabaseUpdates.Check(path);
await File.WriteAllTextAsync(Path.Combine(folder,"access.json"),JsonSerializer.Serialize(new {DatabasePath=path,Password=password,Owner="owner.s404@demo.local",Tenant="tenant.s404@demo.local",ForeignOwner="other.s404@demo.local",ForeignTenant="foreign.s404@demo.local"},new JsonSerializerOptions {WriteIndented=true}));
await File.WriteAllTextAsync(Path.Combine(folder,"README.txt"),"Demo S4-04 tasks 1-4. Login information is stored privately in access.json; do not commit. Owner: /TatToan/Nhap/1; confirm: /TatToan/XacNhan/1; final snapshot: /TatToan/ChiTiet/1. With a 3.500.000 VND penalty, extra due is 13.742 VND; without penalty, refund is 3.486.258 VND. Once confirmed, use /TatToan/KetQua/1. Contract 2 (task3/task4 mode) is available at /TraPhong/Nhap/2 for a new checkout. No PDF. Browser verification performs real confirmation: use a fresh demo for each full run.\n");
if(task5)await File.WriteAllTextAsync(Path.Combine(folder,"README.txt"),"Demo S4-04 task 5. Credentials: access.json (private). Contract 1 is confirmed; open /TatToan/ChiTiet/1 as owner or tenant and download /TatToan/TaiPdf/1. Extra due at confirmation: 13.742 VND. The PDF is also generated in this folder. Contract 2 remains active for a fresh checkout. Do not commit data or credentials.\n");
Console.WriteLine("S4-04 demo ready: "+folder+"; credentials saved privately in access.json.");
sealed class DemoClock:ITimeProvider { public DateTime UtcNow=>new(2026,10,14,2,0,0,DateTimeKind.Utc); }
