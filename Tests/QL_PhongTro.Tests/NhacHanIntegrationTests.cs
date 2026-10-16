using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using System.IO;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class NhacHanIntegrationTests : IDisposable
{
    private readonly string _dbPath;

    public NhacHanIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"nhachan-{Guid.NewGuid():N}.sqlite");
        var seedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "QL_PhongTro", "Data", "permissions.seed.json"));
        LocalDatabaseInitializer.Create(_dbPath, seedPath);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={_dbPath};Foreign Keys=True;Pooling=False").Options);

    private static void Exec(string dbPath, string sql)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Foreign Keys=True;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static int CountThongBao(string dbPath, int hoaDonId, string loai)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Foreign Keys=True;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM thong_bao WHERE hoa_don_id={hoaDonId} AND loai_thong_bao='{loai}'";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void SeedInvoice(DateOnly hanThanhToan, string trangThai = "DA_PHAT_HANH", long tongTien = 2_000_000)
    {
        var nam = hanThanhToan.Year;
        var thang = hanThanhToan.Month;
        Exec(_dbPath, """
            INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat,email_confirmed)
            VALUES(1,'Admin','admin@x.local','0900000000','x','ADMIN',1,0,0,'2026-01-01','2026-01-01',1);
            INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat,email_confirmed)
            VALUES(2,'Khach A','a@x.local','0900000001','x','KHACH_THUE',1,0,0,'2026-01-01','2026-01-01',1);
            INSERT INTO khach_thue(id,tai_khoan_id,ho_ten,ngay_tao) VALUES(1,2,'Khach A','2026-01-01');
            INSERT INTO toa_nha(id,ten_toa_nha,chu_nha_id,quan_ly_id,ngay_chot_hang_thang) VALUES(1,'Toa A',1,1,5);
            INSERT INTO phong_tro(id,toa_nha_id,ma_phong,gia_thue,trang_thai,phien_ban) VALUES(1,1,'101',2000000,'DANG_THUE',1);
            INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao)
            VALUES(1,'HD-0001',1,1,'DANG_HIEU_LUC',1,'2026-01-01');
            INSERT INTO ky_hop_dong(id,hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao)
            VALUES(1,1,1,'2026-01-01','2026-12-31',12,2000000,1,'2026-01-01');
            """);
        Exec(_dbPath, $"""
            INSERT INTO hoa_don(id,ma_hoa_don,hop_dong_id,thang,nam,tu_ngay,den_ngay,trang_thai,tong_tien,han_thanh_toan,nguoi_lap_id,ngay_tao,ngay_cap_nhat)
            VALUES(1,'HD-{nam}-{thang:00}',1,{thang},{nam},'2026-01-01','2026-01-31','{trangThai}',{tongTien},'{hanThanhToan:yyyy-MM-dd}',1,'2026-01-01','2026-01-01');
            """);
    }

    [Fact]
    public async Task ChayTuCommandLine_CreatesSapDenHan_WhenDueIn2Days()
    {
        SeedInvoice(new DateOnly(2026, 10, 10));
        var result = await QL_PhongTro.Services.NhacHanService.ChayTuCommandLine(_dbPath, new DateOnly(2026, 10, 8));
        Assert.Equal(1, result.SapDenHan);
        Assert.Equal(0, result.QuaHan);
        Assert.Equal(1, CountThongBao(_dbPath, 1, QL_PhongTro.Models.LoaiThongBaoHoaDon.SapDenHan));
    }

    [Fact]
    public async Task ChayTuCommandLine_DoesNotCreate_WhenDueIn1Day()
    {
        SeedInvoice(new DateOnly(2026, 10, 10));
        var result = await QL_PhongTro.Services.NhacHanService.ChayTuCommandLine(_dbPath, new DateOnly(2026, 10, 9));
        Assert.Equal(0, result.SapDenHan);
        Assert.Equal(0, result.QuaHan);
    }

    [Fact]
    public async Task ChayTuCommandLine_DoesNotCreate_WhenDueIn3Days()
    {
        SeedInvoice(new DateOnly(2026, 10, 10));
        var result = await QL_PhongTro.Services.NhacHanService.ChayTuCommandLine(_dbPath, new DateOnly(2026, 10, 7));
        Assert.Equal(0, result.SapDenHan);
        Assert.Equal(0, result.QuaHan);
    }

    [Fact]
    public async Task ChayTuCommandLine_CreatesQuaHan_WhenDue1DayAgo()
    {
        SeedInvoice(new DateOnly(2026, 10, 10));
        var result = await QL_PhongTro.Services.NhacHanService.ChayTuCommandLine(_dbPath, new DateOnly(2026, 10, 11));
        Assert.Equal(0, result.SapDenHan);
        Assert.Equal(1, result.QuaHan);
        Assert.Equal(1, CountThongBao(_dbPath, 1, QL_PhongTro.Models.LoaiThongBaoHoaDon.QuaHan));
    }

    [Fact]
    public async Task ChayTuCommandLine_RunningTwice_DoesNotDuplicate()
    {
        SeedInvoice(new DateOnly(2026, 10, 10));
        await QL_PhongTro.Services.NhacHanService.ChayTuCommandLine(_dbPath, new DateOnly(2026, 10, 8));
        var result = await QL_PhongTro.Services.NhacHanService.ChayTuCommandLine(_dbPath, new DateOnly(2026, 10, 8));
        Assert.Equal(0, result.SapDenHan);
        Assert.Equal(1, CountThongBao(_dbPath, 1, QL_PhongTro.Models.LoaiThongBaoHoaDon.SapDenHan));
    }
}
