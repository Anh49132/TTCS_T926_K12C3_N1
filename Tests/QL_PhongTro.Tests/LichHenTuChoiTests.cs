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

// S2-08 lát 3: chủ nhà từ chối yêu cầu kèm lý do và ghi chú.
// Dùng lại đúng harness của lát 1, mọi câu lệnh SQL thuần để không phụ thuộc entity S2-06.
public class LichHenTuChoiTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà S2-08 l3";
    private const string ChuNhaKhacHoTen = "Chủ nhà khác S2-08 l3";
    private const string KhachHoTen = "Khách S2-08 l3";

    private readonly string _dbPath;
    private readonly AppDbContext _db;
    private readonly MockTimeProvider _clock = new();
    private readonly LichHenService _service;

    private int _chuNhaId;
    private int _chuNhaKhacId;
    private int _khachAccountId;
    private int _khachId;
    private int _tinDangId;
    private DateTime _hienTai;

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
    }

    public LichHenTuChoiTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"s2_08_l3_{Guid.NewGuid():N}.sqlite");

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

    private string Scalar(string sql, params (string Name, object? Value)[] parameters)
    {
        var connection = _db.Database.GetDbConnection();
        using var command = ((SqliteConnection)connection).CreateCommand();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var result = command.ExecuteScalar();
        return result is null || result is DBNull ? "" : Convert.ToString(result)!;
    }

    private int TaiKhoanMoi(string hoTen, string vaiTro, string email) => (int)Insert(
        "INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,"
            + "ngay_tao,ngay_cap_nhat,must_change_password,email_confirmed,is_deleted) "
            + "VALUES ($ten,$email,$sdt,'x',$vai_tro,1,0,0,$now,$now,0,1,0)",
        ("$ten", hoTen), ("$email", email), ("$sdt", "09" + Random.Shared.Next(10000000, 99999999)),
        ("$vai_tro", vaiTro), ("$now", Now()));

    private void TaoDuLieu()
    {
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "s2_08_l3_chu_nha@test.local");
        _chuNhaKhacId = TaiKhoanMoi(ChuNhaKhacHoTen, "CHU_NHA", "s2_08_l3_chu_nha_khac@test.local");
        _khachAccountId = TaiKhoanMoi(KhachHoTen, "KHACH_THUE", "s2_08_l3_khach@test.local");

        var toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà S2-08 l3','1 Đường S2-08',1,1)", ("$chu", _chuNhaId));
        Insert("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà khác S2-08 l3','2 Đường S2-08',1,1)", ("$chu", _chuNhaKhacId));

        var phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L3-A101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));

        _tinDangId = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
                + "VALUES ($phong,$chu,'Tin L3-A101','Nội dung','DANG_HIEN_THI',$now)",
            ("$phong", phongId), ("$chu", _chuNhaId), ("$now", Now()));

        _khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES ($tk,$ten,$now)",
            ("$tk", _khachAccountId), ("$ten", KhachHoTen), ("$now", Now()));
    }

    private int TaoYeuCau(string trangThai = LichHenTrangThai.Moi, DateTime? lichHenUtc = null) => (int)Insert(
        "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
            + "so_nguoi_du_kien,lich_hen,trang_thai,ngay_tao,phien_ban) "
            + "VALUES ($ma,$tin,$khach,$loai,'2026-04-01',2,$lich,$trangThai,$now,0)",
        ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
        ("$tin", _tinDangId), ("$khach", _khachId),
        ("$loai", LichHenTrangThai.LoaiXemPhong),
        ("$lich", lichHenUtc is { } l ? l.ToString("yyyy-MM-dd HH:mm:ss") : null),
        ("$trangThai", trangThai), ("$now", Now()));

    [Fact]
    public async Task TuChoi_YeuCauMoi_ChuyenSangTuChoiVaGhiLyDo()
    {
        var id = TaoYeuCau();

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.DaCoKhachThue, "đã có người hỏi trước", default);

        Assert.Equal(LichHenTrangThai.TuChoi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(LyDoTuChoi.DaCoKhachThue, Scalar("SELECT ly_do_tu_choi FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(_chuNhaId.ToString(), Scalar("SELECT nguoi_xu_ly_id FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(Now(), Scalar("SELECT ngay_xu_ly FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("1", Scalar("SELECT phien_ban FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task TuChoi_GhiLichSuVaThongBaoChoKhach()
    {
        var id = TaoYeuCau();

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.KhachKhongLienLacDuoc, null, default);

        Assert.Equal(HanhDongYeuCau.TuChoi, Scalar("SELECT hanh_dong FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai_cu FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.TuChoi, Scalar("SELECT trang_thai_moi FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LyDoTuChoi.KhachKhongLienLacDuoc, Scalar("SELECT ly_do_tu_choi FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal("CHU_NHA", Scalar("SELECT vai_tro_luc_thuc_hien FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));

        Assert.Equal(LoaiThongBaoYeuCau.YeuCauTuChoi, Scalar("SELECT loai FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(_khachAccountId.ToString(), Scalar("SELECT nguoi_nhan_id FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal($"Khách không liên lạc được", LichHenService.LyDoLabel(LyDoTuChoi.KhachKhongLienLacDuoc));
        Assert.Contains(LichHenService.LyDoLabel(LyDoTuChoi.KhachKhongLienLacDuoc),
            Scalar("SELECT noi_dung FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task TuChoi_GhiChuNamTrongLichSuKhongPhaiTrenYeuCauThue()
    {
        // yeu_cau_thue không có cột ghi chú và thuộc S2-06, nên ghi chú chỉ nằm ở lịch sử.
        var id = TaoYeuCau();

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, "abcde", default);

        Assert.Equal("abcde", Scalar("SELECT ghi_chu_tu_choi FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        var doc = await _service.DocAsync(id, default);
        Assert.Equal("abcde", doc!.GhiChuTuChoi);
    }

    [Fact]
    public async Task TuChoi_KhongGhiChuThiDeTrongVaDocVeRong()
    {
        var id = TaoYeuCau();

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.DaCoKhachThue, "   ", default);

        Assert.Equal("", Scalar("SELECT ghi_chu_tu_choi FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Null((await _service.DocAsync(id, default))!.GhiChuTuChoi);
    }

    [Fact]
    public async Task TuChoi_YeuCauDaHenLich_VanTuChoiDuoc()
    {
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, _hienTai.AddDays(1));

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.DaCoKhachThue, null, default);

        Assert.Equal(LichHenTrangThai.TuChoi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task Huy_KhachHuyYeuCauMoi_GhiLichSuVaThongBaoChuNha()
    {
        var id = TaoYeuCau();

        await _service.HuyAsync(id, _khachAccountId, default);

        Assert.Equal(LichHenTrangThai.DaHuy, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(HanhDongYeuCau.Huy, Scalar("SELECT hanh_dong FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal("KHACH_THUE", Scalar("SELECT vai_tro_luc_thuc_hien FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(_chuNhaId.ToString(), Scalar("SELECT nguoi_nhan_id FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LoaiThongBaoYeuCau.YeuCauDaHuy, Scalar("SELECT loai FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task Huy_ChuNhaHuyYeuCauDaHen_GhiLichSuVaThongBaoKhach()
    {
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, _hienTai.AddDays(1));

        await _service.HuyAsync(id, _chuNhaId, default);

        Assert.Equal(LichHenTrangThai.DaHuy, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("CHU_NHA", Scalar("SELECT vai_tro_luc_thuc_hien FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(_khachAccountId.ToString(), Scalar("SELECT nguoi_nhan_id FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task Huy_TrangThaiDaDongHoacNguoiKhac_ThenBiTuChoi()
    {
        var daDuyet = TaoYeuCau(LichHenTrangThai.DaDuyet);
        var cuaNguoiKhac = TaoYeuCau();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.HuyAsync(daDuyet, _chuNhaId, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.HuyAsync(cuaNguoiKhac, _chuNhaKhacId, default));

        Assert.Equal(LichHenTrangThai.DaDuyet, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", daDuyet)));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", cuaNguoiKhac)));
    }

    [Fact]
    public async Task TuChoi_LyDoKhac_PhaiCoGhiChu()
    {
        var id = TaoYeuCau();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, null, default));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, "  ", default));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, "abc", default));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, "abcde", default);
        Assert.Equal(LichHenTrangThai.TuChoi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task TuChoi_LyDoKhongHopLe_ThenTuChoi()
    {
        var id = TaoYeuCau();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TuChoiAsync(id, _chuNhaId, "TUONG_TUONG", "ghi chú", default));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task TuChoi_GhiChuQuaDai_ThenTuChoi()
    {
        var id = TaoYeuCau();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.DaCoKhachThue, new string('a', 501), default));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task TuChoi_KhongPhaiChuNhaPhong_ThenTuChoi()
    {
        // KHACH_THUE và cả chủ nhà khác đều phải bị chặn.
        var id = TaoYeuCau();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.TuChoiAsync(id, _khachAccountId, LyDoTuChoi.DaCoKhachThue, null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.TuChoiAsync(id, _chuNhaKhacId, LyDoTuChoi.DaCoKhachThue, null, default));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task TuChoi_YeuCauDaTuChoi_ThenTuChoi()
    {
        var id = TaoYeuCau(LichHenTrangThai.TuChoi);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.DaCoKhachThue, null, default));
    }

    [Fact]
    public async Task TuChoi_YeuCauDaDuyet_ThenTuChoi()
    {
        var id = TaoYeuCau(LichHenTrangThai.DaDuyet);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.DaCoKhachThue, null, default));
    }

    [Fact]
    public async Task TuChoi_YeuCauKhongTonTai_ThenBaoKhongTimThay()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.TuChoiAsync(999999, _chuNhaId, LyDoTuChoi.DaCoKhachThue, null, default));
    }

    [Fact]
    public async Task TuChoi_KhongGuiThongBaoChoChinhNguoiThaoTac()
    {
        var id = TaoYeuCau();
        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.DaCoKhachThue, null, default);
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_thong_bao WHERE nguoi_nhan_id=$id", ("$id", _chuNhaId)));
    }

    [Fact]
    public async Task TuChoi_KhachChuaLienKetTaiKhoan_VanTuChoiDuoc_ChiBoThongBao()
    {
        var khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES (NULL,'Khách vô danh',$now)", ("$now", Now()));
        var id = (int)Insert(
            "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
                + "so_nguoi_du_kien,trang_thai,ngay_tao,phien_ban) "
                + "VALUES ($ma,$tin,$khach,'XEM_PHONG','2026-04-01',2,'MOI',$now,0)",
            ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
            ("$tin", _tinDangId), ("$khach", khachId), ("$now", Now()));

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.DaCoKhachThue, null, default);

        Assert.Equal(LichHenTrangThai.TuChoi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task TuChoi_GiuNguyenLichHenDaXacNhan()
    {
        // Từ chối sau khi đã hẹn không xoá lịch: S2-09 còn cần thấy mốc đã hẹn.
        var lichHen = _hienTai.AddDays(1);
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, lichHen);

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.KhachKhongLienLacDuoc, null, default);

        Assert.Equal(lichHen.ToString("yyyy-MM-dd HH:mm:ss"), Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(lichHen.ToString("yyyy-MM-dd HH:mm:ss"),
            Scalar("SELECT lich_hen_cu FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Theory]
    [InlineData(LyDoTuChoi.DaCoKhachThue, "Phòng đã có khách thuê")]
    [InlineData(LyDoTuChoi.KhongPhuHop, "Không phù hợp số người")]
    [InlineData(LyDoTuChoi.KhachKhongLienLacDuoc, "Khách không liên lạc được")]
    [InlineData(LyDoTuChoi.Khac, "Lý do khác")]
    public void LyDoLabel_HienThiDungTen(string lyDo, string nhan)
    {
        Assert.Equal(nhan, LichHenService.LyDoLabel(lyDo));
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("abcd")]
    public async Task TuChoi_LyDoKhacGhiChuNganHon5KyTu_ThenBiTuChoi(string ghiChu)
    {
        // AC2 quy định ghi chú từ 5 đến 500 ký tự. Giao diện cũng chặn nhưng server là nơi quyết định,
        // nên gửi form bằng tay vẫn không tạo được yêu cầu thiếu ghi chú.
        var id = TaoYeuCau();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, ghiChu, default));

        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task TuChoi_LyDoKhacGhiChuDung5KyTu_ThenDuoc()
    {
        var id = TaoYeuCau();

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, "abcde", default);

        Assert.Equal(LichHenTrangThai.TuChoi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task TuChoi_LyDoKhacGhiChuDung500KyTu_ThenDuoc()
    {
        var id = TaoYeuCau();

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, new string('a', 500), default);

        Assert.Equal("500", Scalar("SELECT length(ghi_chu_tu_choi) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Theory]
    [InlineData(LyDoTuChoi.DaCoKhachThue)]
    [InlineData(LyDoTuChoi.KhongPhuHop)]
    [InlineData(LyDoTuChoi.KhachKhongLienLacDuoc)]
    public async Task TuChoi_LyDoKhacKhongCanGhiChu(string lyDo)
    {
        // Chỉ "Lý do khác" mới bắt ghi chú, các lý do còn lại thì không.
        var id = TaoYeuCau();

        await _service.TuChoiAsync(id, _chuNhaId, lyDo, null, default);

        Assert.Equal(LichHenTrangThai.TuChoi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public void HangNhatGhiChuKhopVoiGiaoDienVaServer()
    {
        // Giao diện dùng 5/500 trong lich-hen.js và attribute maxlength, server dùng hai hằng này.
        Assert.Equal(5, LichHenService.GhiChuToiThieuLyDoKhac);
        Assert.Equal(500, LichHenService.GhiChuToiDa);
    }
}
