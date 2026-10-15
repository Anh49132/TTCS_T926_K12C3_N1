using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed class BaoHongChiPhiOptionalInvoiceTests
{
    private sealed class Clock : ITimeProvider { public DateTime UtcNow => new(2026,10,14,12,0,0,DateTimeKind.Utc); }
    private static string CreateDatabase()
    {
        var root=Directory.GetCurrentDirectory();
        while(!File.Exists(Path.Combine(root,"AGENTS.md"))) root=Directory.GetParent(root)!.FullName;
        var dir=Path.Combine(root,"data","s407-cost","optional-invoice-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);var path=Path.Combine(dir,"test.sqlite");
        LocalDatabaseInitializer.Create(path,Path.Combine(root,"QL_PhongTro","Data","permissions.seed.json"));
        RentalRequestSchema.Initialize(path);
        using var c=new SqliteConnection($"Data Source={path};Foreign Keys=True;Pooling=False");c.Open();
        using var cmd=c.CreateCommand();cmd.CommandText="""
INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat) VALUES
 (1,'Owner','owner@audit.local','0900000001','hash','CHU_NHA',1,1,'2026-01-01','2026-01-01'),
 (2,'Tenant','tenant@audit.local','0900000002','hash','KHACH_THUE',1,1,'2026-01-01','2026-01-01'),
 (3,'Manager','manager@audit.local','0900000003','hash','QUAN_LY',1,1,'2026-01-01','2026-01-01');
 INSERT INTO khach_thue(id,tai_khoan_id,ho_ten,ngay_tao) VALUES(1,2,'Tenant','2026-01-01');
 INSERT INTO toa_nha(id,chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES(1,1,3,'Audit','Audit');
 INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(1,1,'A101',1,25,3000000,3,'DANG_THUE','2026-01-01');
 INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES(1,'HD-AUDIT',1,1,'DANG_HIEU_LUC');
 INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES(1,'2026-10-01','2026-12-31',3000000);
 INSERT INTO bao_hong(id,ma_bao_hong,nguoi_bao_id,khach_thue_id,phong_id,hop_dong_id,loai_thiet_bi,mo_ta,muc_do,trang_thai,ngay_tao,khoa_chong_trung) VALUES(1,'BH-202610-0001',2,1,1,1,'Tap','Audit','THUONG','HOAN_THANH','2026-10-14 05:00:00','11111111111111111111111111111111');
""";cmd.ExecuteNonQuery();return path;
    }
    private static AppDbContext Context(string path)=>new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True;Pooling=False").Options);
    private static BaoHongChiPhiInput Input(string side,int version=0)=>new(){SoTien="350000",BenChiuPhi=side,PhienBan=version};

    [Theory][InlineData("CHU_NHA")][InlineData("KHACH_THUE")]
    public async Task SaveEditReplayAndReadWithoutInvoiceModule(string side)
    {
        var path=CreateDatabase(); DatabaseUpdates.Check(path);
        using(var db=Context(path)) {
            var service=new BaoHongChiPhiService(db,new Clock());var input=Input(side);
            var cost=await service.LuuAsync(3,1,input);
            Assert.Equal(350000,cost.SoTien);Assert.Equal(1,cost.HopDongId);
            Assert.Equal(side=="KHACH_THUE",cost.ChoThuKhach);
            Assert.Equal(side=="KHACH_THUE"?new DateOnly(2026,11,1):(DateOnly?)null,cost.KyDuKien);
            Assert.Equal(cost.Id,(await service.LuuAsync(3,1,input)).Id);
            await Assert.ThrowsAsync<ChiPhiConflictException>(()=>service.LuuAsync(3,1,Input(side)));
            var edit=Input(side=="CHU_NHA"?"KHACH_THUE":"CHU_NHA",1);edit.SoTien="250000";
            await service.LuuAsync(3,1,edit);
        }
        using(var db=Context(path)) {
            var cost=await db.ChiPhiBaoHongs.SingleAsync();Assert.Equal(250000,cost.SoTien);Assert.Equal(2,cost.PhienBan);
            Assert.Equal(2,await db.LichSuChiPhiBaoHongs.CountAsync());
            Assert.Equal(2,await db.NhatKyHoatDongs.CountAsync(x=>x.LoaiDoiTuong=="chi_phi_bao_hong"));
            var tenant=new BaoHongService(db,new Clock());
            Assert.NotNull((await tenant.ChiTietAsync(2,1))!.ChiPhi);
            Assert.NotNull((await new ChuNhaBaoHongService(db,tenant).ChiTietAsync(1,1))!.ChiPhi);
            Assert.NotNull((await new QuanLyBaoHongService(db,new Clock(),tenant).ChiTietAsync(3,1))!.ChiPhi);
            Assert.Equal(0,await db.Database.SqlQuery<int>($"SELECT count(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','chi_tiet_hoa_don')").SingleAsync());
        }
        DatabaseUpdates.Check(path);
    }

    [Fact]
    public async Task InstallingInvoicesAfterSavedCostPreservesHistoryAndLocksLinkedSource()
    {
        var path=CreateDatabase();
        using(var db=Context(path))
            await new BaoHongChiPhiService(db,new Clock()).LuuAsync(3,1,Input("KHACH_THUE"));
        DichVuSchemaInitializer.InitializeInvoices(path);
        DatabaseUpdates.Check(path);
        using(var connection=new SqliteConnection($"Data Source={path};Foreign Keys=True;Pooling=False"))
        {
            connection.Open();using var command=connection.CreateCommand();
            command.CommandText="""
                INSERT INTO hoa_don(id,ma_hoa_don,hop_dong_id,thang,nam,tu_ngay,den_ngay,ngay_chot,so_nguoi_tinh_phi,ngay_lap,han_thanh_toan,tong_tien,trang_thai,nguoi_lap_id)
                VALUES(1,'HD-LATE-COST',1,11,2026,'2026-11-01','2026-11-30','2026-11-01',1,'2026-10-14 12:00:00','2026-11-08',350000,'NHAP',1);
                INSERT INTO chi_tiet_hoa_don(hoa_don_id,so_thu_tu,bao_hong_id,loai_khoan,ten_khoan,so_luong,don_gia,thanh_tien,ghi_chu)
                VALUES(1,1,1,'PHAT_SINH','Sửa chữa','1',350000,350000,'Chi phí báo hỏng');
                """;
            command.ExecuteNonQuery();
            command.CommandText="UPDATE chi_phi_bao_hong SET so_tien=250000,phien_ban=2";
            Assert.Equal(19,Assert.Throws<SqliteException>(()=>command.ExecuteNonQuery()).SqliteErrorCode);
        }
        using(var db=Context(path))
        {
            Assert.Equal(350000,(await db.ChiPhiBaoHongs.SingleAsync()).SoTien);
            Assert.Equal(1,await db.LichSuChiPhiBaoHongs.CountAsync());
            Assert.Equal("TRONG_HOA_DON_NHAP",await db.Database.SqlQuery<string>($"SELECT trang_thai_phan_bo AS Value FROM chi_phi_bao_hong_trang_thai WHERE bao_hong_id=1").SingleAsync());
            await Assert.ThrowsAsync<ChiPhiConflictException>(()=>new BaoHongChiPhiService(db,new Clock()).LuuAsync(3,1,Input("KHACH_THUE",1)));
        }
    }
}
