using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace QL_PhongTro.Tests;

// S2-08 lát 1: xác nhận lịch hẹn. Each test builds a throwaway database in the temp
// folder, so a failed assertion never leaks into another and the repository database
// is never touched.
public class LichHenXacNhanLichTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà S2-08";
    private const string ChuNhaKhacHoTen = "Chủ nhà khác S2-08";
    private const string KhachHoTen = "Khách S2-08";

    private readonly string _dbPath;
    private readonly AppDbContext _db;
    private readonly MockTimeProvider _clock = new();
    private readonly LichHenService _service;

    private int _chuNhaId;
    private int _chuNhaKhacId;
    private int _khachAccountId;
    private int _khachId;
    private int _phongId;
    private int _tinDangId;
    private int _toaNhaId;
    private DateTime _hienTai;

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
    }

    public LichHenXacNhanLichTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"s2_08_l1_{Guid.NewGuid():N}.sqlite");

        // Same order a real machine follows: base schema, then the optional rental module
        // that S2-06 owns, then S2-08's own v10.
        LocalDatabaseInitializer.Create(_dbPath, seedPath);
        RentalRequestSchema.Initialize(_dbPath);
        DatabaseUpdates.Update(_dbPath, seedPath);

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + _dbPath).Options);
        _hienTai = new DateTime(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc); // 10:00 Vietnam
        _clock.UtcNow = _hienTai;
        _service = new LichHenService(_db, _clock);
        TaoDuLieu();
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private long Insert(string sql, params (string Name, object? Value)[] parameters)
    {
        var connection = _db.Database.GetDbConnection();
        using var command = ((SqliteConnection)connection).CreateCommand();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
        using var idCommand = ((SqliteConnection)connection).CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt64(idCommand.ExecuteScalar());
    }

    private string Now() => _hienTai.ToString("yyyy-MM-dd HH:mm:ss");

    private int TaiKhoanMoi(string hoTen, string vaiTro, string email) => (int)Insert(
        "INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,"
            + "ngay_tao,ngay_cap_nhat,must_change_password,email_confirmed,is_deleted) "
            + "VALUES ($ten,$email,$sdt,'x',$vai_tro,1,0,0,$now,$now,0,1,0)",
        ("$ten", hoTen), ("$email", email), ("$sdt", "09" + Random.Shared.Next(10000000, 99999999)),
        ("$vai_tro", vaiTro), ("$now", Now()));

    private void TaoDuLieu()
    {
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "s2_08_l1_chu_nha@test.local");
        _chuNhaKhacId = TaiKhoanMoi(ChuNhaKhacHoTen, "CHU_NHA", "s2_08_l1_chu_nha_khac@test.local");
        _khachAccountId = TaiKhoanMoi(KhachHoTen, "KHACH_THUE", "s2_08_l1_khach@test.local");

        _toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà S2-08','1 Đường S2-08',1,1)", ("$chu", _chuNhaId));
        Insert("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà khác S2-08','2 Đường S2-08',1,1)", ("$chu", _chuNhaKhacId));

        _phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'P-S2-08',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", _toaNhaId), ("$now", Now()));

        _tinDangId = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
                + "VALUES ($phong,$chu,'Tin S2-08','Nội dung','DANG_HIEN_THI',$now)",
            ("$phong", _phongId), ("$chu", _chuNhaId), ("$now", Now()));

        _khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES ($tk,$ten,$now)",
            ("$tk", _khachAccountId), ("$ten", KhachHoTen), ("$now", Now()));
    }

    private int TaoYeuCau(string trangThai = LichHenTrangThai.Moi, DateTime? lichHenUtc = null, int? khachId = null) =>
        (int)Insert(
            "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
                + "so_nguoi_du_kien,lich_hen,trang_thai,ngay_tao,phien_ban) "
                + "VALUES ($ma,$tin,$khach,$loai,'2026-04-01',2,$lich,$trangThai,$now,0)",
            ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
            ("$tin", _tinDangId), ("$khach", khachId ?? _khachId),
            ("$loai", LichHenTrangThai.LoaiXemPhong), ("$lich", lichHenUtc is { } l ? l.ToString("yyyy-MM-dd HH:mm:ss") : null),
            ("$trangThai", trangThai), ("$now", Now()));

    private string Scalar(string sql, params (string Name, object? Value)[] parameters)
    {
        var connection = _db.Database.GetDbConnection();
        using var command = ((SqliteConnection)connection).CreateCommand();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
    }

    [Fact]
    public async Task XacNhanLich_ChuyenSangDaHenLich_GhiLichSuVaThongBao()
    {
        var id = TaoYeuCau();
        var lichHen = _hienTai.AddDays(1);

        await _service.XacNhanLichAsync(id, _chuNhaId, lichHen, default);

        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(lichHen.ToString("yyyy-MM-dd HH:mm:ss"), Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(_chuNhaId.ToString(), Scalar("SELECT nguoi_xu_ly_id FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(Now(), Scalar("SELECT ngay_xu_ly FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("1", Scalar("SELECT phien_ban FROM yeu_cau_thue WHERE id=$id", ("$id", id)));

        Assert.Equal(HanhDongYeuCau.XacNhan, Scalar("SELECT hanh_dong FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai_cu FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai_moi FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal("CHU_NHA", Scalar("SELECT vai_tro_luc_thuc_hien FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));

        Assert.Equal(LoaiThongBaoYeuCau.XacNhanLich, Scalar("SELECT loai FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(_khachAccountId.ToString(), Scalar("SELECT nguoi_nhan_id FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal($"/LichHen/ChiTiet/{id}", Scalar("SELECT duong_dan FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task XacNhanLich_GhiDungMotBanGhi()
    {
        var id = TaoYeuCau();
        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);
        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task XacNhanLich_LichHenTrongQuaKhong()
    {
        var id = TaoYeuCau();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddMinutes(-5), default));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task XacNhanLich_KhongPhaiChuNhaPhong_ThenTuChoi()
    {
        // KHACH_THUE holds WRITE on YEU_CAU_THUE, so ownership has to be checked in the service.
        var id = TaoYeuCau();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.XacNhanLichAsync(id, _khachAccountId, _hienTai.AddDays(1), default));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task XacNhanLich_ChuNhaCuaToaNhaKhac_ThenTuChoi()
    {
        var id = TaoYeuCau();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.XacNhanLichAsync(id, _chuNhaKhacId, _hienTai.AddDays(1), default));
    }

    [Fact]
    public async Task XacNhanLich_YeuCauDaCoLich_ThenTuChoi()
    {
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, _hienTai.AddDays(1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(2), default));
    }

    [Fact]
    public async Task XacNhanLich_LanThuHai_ThenTuChoi()
    {
        var id = TaoYeuCau();
        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(2), default));
    }

    [Fact]
    public async Task XacNhanLich_YeuCauKhongTonTai_ThenBaoKhongTimThay()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.XacNhanLichAsync(999999, _chuNhaId, _hienTai.AddDays(1), default));
    }

    [Fact]
    public async Task XacNhanLich_KhachChuaLienKetTaiKhoan_VanXacNhanDuoc_ChiBoThongBao()
    {
        var khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES (NULL,'Khách vô danh',$now)", ("$now", Now()));
        var id = TaoYeuCau(LichHenTrangThai.Moi, null, khachId);

        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);

        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DocYeuCau_DocQuaTinDangDenPhongVaChuNha()
    {
        var id = TaoYeuCau();
        var doc = await _service.DocAsync(id, default);
        Assert.NotNull(doc);
        Assert.Equal(_phongId, doc!.PhongId);
        Assert.Equal("P-S2-08", doc.MaPhong);
        Assert.Equal(_chuNhaId, doc.ChuNhaId);
        Assert.Equal(_khachAccountId, doc.TaiKhoanKhachId);
    }

    [Fact]
    public async Task QuyenXem_ChuNhaVaKhachDuocXem_ChuNhaKhacKhong()
    {
        var id = TaoYeuCau();
        Assert.True(await _service.ChuNhaCuaYeuCauAsync(_chuNhaId, id, default));
        Assert.True(await _service.KhachChuYeuCauAsync(_khachAccountId, id, default));
        Assert.False(await _service.ChuNhaCuaYeuCauAsync(_chuNhaKhacId, id, default));
        Assert.False(await _service.KhachChuYeuCauAsync(_chuNhaKhacId, id, default));
    }
}
