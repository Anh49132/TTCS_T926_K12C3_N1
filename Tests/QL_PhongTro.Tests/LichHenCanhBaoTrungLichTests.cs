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

// S2-08 lát 2: cảnh báo trùng lịch hẹn trong khoảng ±30 phút.
// Dùng lại đúng hàm TimLichTrungAsync mà lát 1 đã đặt nền, trên cùng kiểu dự liệu.
public class LichHenCanhBaoTrungLichTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà S2-08 l2";
    private const string KhachHoTen = "Khách S2-08 l2";

    private readonly string _dbPath;
    private readonly AppDbContext _db;
    private readonly MockTimeProvider _clock = new();
    private readonly LichHenService _service;

    private int _chuNhaId;
    private int _khachAccountId;
    private int _khachId;
    private int _phongId;
    private int _tinDangId;
    private int _phongKhacId;
    private int _tinDangPhongKhacId;
    private DateTime _hienTai;

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
    }

    public LichHenCanhBaoTrungLichTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"s2_08_l2_{Guid.NewGuid():N}.sqlite");

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
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "s2_08_l2_chu_nha@test.local");
        _khachAccountId = TaiKhoanMoi(KhachHoTen, "KHACH_THUE", "s2_08_l2_khach@test.local");

        var toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà S2-08 l2','1 Đường S2-08',1,1)", ("$chu", _chuNhaId));

        _phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L2-A101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));
        _phongKhacId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L2-A201',2,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));

        _tinDangId = TinDangMoi(_phongId, "L2-A101");
        _tinDangPhongKhacId = TinDangMoi(_phongKhacId, "L2-A201");

        _khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES ($tk,$ten,$now)",
            ("$tk", _khachAccountId), ("$ten", KhachHoTen), ("$now", Now()));
    }

    private int TinDangMoi(int phongId, string maPhong) => (int)Insert(
        "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
            + "VALUES ($phong,$chu,$ten,'Nội dung','DANG_HIEN_THI',$now)",
        ("$phong", phongId), ("$chu", _chuNhaId), ("$ten", "Tin " + maPhong), ("$now", Now()));

    private int TaoYeuCau(string trangThai, DateTime? lichHenUtc, int? tinDangId = null) => (int)Insert(
        "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
            + "so_nguoi_du_kien,lich_hen,trang_thai,ngay_tao,phien_ban) "
            + "VALUES ($ma,$tin,$khach,$loai,'2026-04-01',2,$lich,$trangThai,$now,0)",
        ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
        ("$tin", tinDangId ?? _tinDangId), ("$khach", _khachId),
        ("$loai", LichHenTrangThai.LoaiXemPhong),
        ("$lich", lichHenUtc is { } l ? l.ToString("yyyy-MM-dd HH:mm:ss") : null),
        ("$trangThai", trangThai), ("$now", Now()));

    [Fact]
    public async Task LichTrung_Dung30PhutVanTinhLaTrung()
    {
        var goc = _hienTai.AddDays(2); // 10:00 Vietnam
        var id = TaoYeuCau(LichHenTrangThai.Moi, goc);
        var bang30 = TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddMinutes(30));
        var truoc30 = TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddMinutes(-30));

        var ketQua = await _service.TimLichTrungAsync(_phongId, goc, id, default);

        Assert.Equal(2, ketQua.Count);
        Assert.Contains(ketQua, x => x.YeuCauId == bang30);
        Assert.Contains(ketQua, x => x.YeuCauId == truoc30);
    }

    [Fact]
    public async Task LichTrung_Cach29PhutVanTinhLaTrung()
    {
        // Biên trong: 29 phút vẫn nằm trong ±30 nên phải cảnh báo, chỉ 31 phút mới ra ngoài.
        var goc = _hienTai.AddDays(2);
        var id = TaoYeuCau(LichHenTrangThai.Moi, goc);
        var truoc29 = TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddMinutes(-29));
        var sau29 = TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddMinutes(29));

        var ketQua = await _service.TimLichTrungAsync(_phongId, goc, id, default);

        Assert.Equal(2, ketQua.Count);
        Assert.Contains(ketQua, x => x.YeuCauId == truoc29);
        Assert.Contains(ketQua, x => x.YeuCauId == sau29);
    }

    [Fact]
    public async Task LichTrung_Cach31PhutKhongTinhLaTrung()
    {
        // Biên ngoài: 31 phút thì không cảnh báo, đã có test 30 phút ở lát trước.
        var goc = _hienTai.AddDays(2);
        var id = TaoYeuCau(LichHenTrangThai.Moi, goc);
        var truoc31 = TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddMinutes(-31));

        var ketQua = await _service.TimLichTrungAsync(_phongId, goc, id, default);

        Assert.Empty(ketQua);
        Assert.DoesNotContain(ketQua, x => x.YeuCauId == truoc31);
    }

    [Fact]
    public async Task LichTrung_Ngoai30PhutKhongTinh()
    {
        var goc = _hienTai.AddDays(2);
        var id = TaoYeuCau(LichHenTrangThai.Moi, goc);
        var ngoaiPhamVi = TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddMinutes(31));
        TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddHours(2));

        var ketQua = await _service.TimLichTrungAsync(_phongId, goc, id, default);

        Assert.Empty(ketQua);
        Assert.DoesNotContain(ketQua, x => x.YeuCauId == ngoaiPhamVi);
    }

    [Fact]
    public async Task LichTrung_ChiTinhDaHenLichVaDaDuyet()
    {
        var goc = _hienTai.AddDays(3);
        var id = TaoYeuCau(LichHenTrangThai.Moi, goc);
        TaoYeuCau(LichHenTrangThai.Moi, goc.AddMinutes(10));
        TaoYeuCau(LichHenTrangThai.TuChoi, goc.AddMinutes(10));
        TaoYeuCau(LichHenTrangThai.DaHuy, goc.AddMinutes(10));
        var daHen = TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddMinutes(10));
        var daDuyet = TaoYeuCau(LichHenTrangThai.DaDuyet, goc.AddMinutes(10));

        var ketQua = await _service.TimLichTrungAsync(_phongId, goc, id, default);

        Assert.Equal(2, ketQua.Count);
        Assert.Contains(ketQua, x => x.YeuCauId == daHen);
        Assert.Contains(ketQua, x => x.YeuCauId == daDuyet);
    }

    [Fact]
    public async Task LichTrung_KhongCanhBanhChinhYeuCauDangXet()
    {
        var goc = _hienTai.AddDays(4);
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, goc);
        Assert.Empty(await _service.TimLichTrungAsync(_phongId, goc, id, default));
    }

    [Fact]
    public async Task LichTrung_KhongCanhBanhLichOPhongKhac()
    {
        // S2-08 never sees yeu_cau_thue.phong_id; the room is reached through tin_dang.
        var goc = _hienTai.AddDays(5);
        var id = TaoYeuCau(LichHenTrangThai.Moi, goc);
        TaoYeuCau(LichHenTrangThai.DaHenLich, goc, _tinDangPhongKhacId);

        Assert.Empty(await _service.TimLichTrungAsync(_phongId, goc, id, default));
    }

    [Fact]
    public async Task LichTrung_BaoCaoTenKhachVaPhong()
    {
        var goc = _hienTai.AddDays(6);
        var id = TaoYeuCau(LichHenTrangThai.Moi, goc);
        TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddMinutes(15));

        var muc = Assert.Single(await _service.TimLichTrungAsync(_phongId, goc, id, default));

        Assert.Equal(KhachHoTen, muc.TenKhach);
        Assert.Equal("L2-A101", muc.TenPhong);
        Assert.Equal(LichHenService.HienThoiGio(goc.AddMinutes(15)), LichHenService.HienThoiGio(muc.LichHen));
    }

    [Fact]
    public async Task LichTrung_YeuCauKhongTonTai_ThenKhongBaoLoi()
    {
        Assert.Empty(await _service.TimLichTrungAsync(_phongId, _hienTai.AddDays(2), 999999, default));
    }

    [Fact]
    public async Task CanhBaoKhongChanXacNhan()
    {
        var goc = _hienTai.AddDays(7);
        var id = TaoYeuCau(LichHenTrangThai.Moi, goc);
        TaoYeuCau(LichHenTrangThai.DaHenLich, goc.AddMinutes(30));

        // Có cảnh báo trùng lịch, nhưng chủ nhà vẫn xác nhận được.
        Assert.NotEmpty(await _service.TimLichTrungAsync(_phongId, goc, id, default));

        var lichHen = goc.AddDays(30);
        await _service.XacNhanLichAsync(id, _chuNhaId, lichHen, default);

        var connection = _db.Database.GetDbConnection();
        using var command = ((SqliteConnection)connection).CreateCommand();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        command.CommandText = "SELECT trang_thai FROM yeu_cau_thue WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        Assert.Equal(LichHenTrangThai.DaHenLich, Convert.ToString(command.ExecuteScalar()));
    }
}
