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

// S2-08 lát 6: chủ nhà duyệt yêu cầu thuê ngay, phòng chuyển sang Đã đặt cọc.
// Dùng lại đúng harness của lát 1, mọi câu lệnh SQL thuần để không phụ thuộc entity S2-06.
public class LichHenDuyetThueNgayTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà S2-08 l6";
    private const string ChuNhaKhacHoTen = "Chủ nhà khác S2-08 l6";
    private const string KhachHoTen = "Khách S2-08 l6";

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
    private DateTime _hienTai;

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
    }

    public LichHenDuyetThueNgayTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"s2_08_l6_{Guid.NewGuid():N}.sqlite");

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
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "s2_08_l6_chu_nha@test.local");
        _chuNhaKhacId = TaiKhoanMoi(ChuNhaKhacHoTen, "CHU_NHA", "s2_08_l6_chu_nha_khac@test.local");
        _khachAccountId = TaiKhoanMoi(KhachHoTen, "KHACH_THUE", "s2_08_l6_khach@test.local");

        var toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà S2-08 l6','1 Đường S2-08',1,1)", ("$chu", _chuNhaId));
        Insert("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà khác S2-08 l6','2 Đường S2-08',1,1)", ("$chu", _chuNhaKhacId));

        _phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L6-A101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));

        _tinDangId = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
                + "VALUES ($phong,$chu,'Tin L6-A101','Nội dung','DANG_HIEN_THI',$now)",
            ("$phong", _phongId), ("$chu", _chuNhaId), ("$now", Now()));

        _khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES ($tk,$ten,$now)",
            ("$tk", _khachAccountId), ("$ten", KhachHoTen), ("$now", Now()));
    }

    private int TaoYeuCau(string loai = LichHenTrangThai.LoaiThueNgay,
        string trangThai = LichHenTrangThai.Moi, DateTime? lichHenUtc = null) => (int)Insert(
        "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
            + "so_nguoi_du_kien,lich_hen,trang_thai,ngay_tao,phien_ban) "
            + "VALUES ($ma,$tin,$khach,$loai,'2026-04-01',2,$lich,$trangThai,$now,0)",
        ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
        ("$tin", _tinDangId), ("$khach", _khachId), ("$loai", loai),
        ("$lich", lichHenUtc is { } l ? l.ToString("yyyy-MM-dd HH:mm:ss") : null),
        ("$trangThai", trangThai), ("$now", Now()));

    private string TrangThaiPhong() => Scalar("SELECT trang_thai FROM phong_tro WHERE id=$id", ("$id", _phongId));

    [Fact]
    public async Task DuyetSauKhiXem_GiuLichHenVaGiuPhong()
    {
        var appointment = _hienTai.AddDays(-1);
        var id = TaoYeuCau(LichHenTrangThai.LoaiXemPhong, LichHenTrangThai.DaHenLich, appointment);
        await _service.DuyetSauKhiXemAsync(id, _chuNhaId, default);
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("DA_DAT_COC", TrangThaiPhong());
        Assert.Equal(appointment.ToString("yyyy-MM-dd HH:mm:ss"), Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(HanhDongYeuCau.DuyetSauKhiXem, Scalar("SELECT hanh_dong FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DuyetSauKhiXemAsync(id, _chuNhaId, default));
    }

    [Theory]
    [InlineData("XEM_PHONG", "MOI")]
    [InlineData("THUE_NGAY", "DA_HEN_LICH")]
    [InlineData("XEM_PHONG", "DA_HUY")]
    public async Task DuyetSauKhiXem_ChanLoaiHoacTrangThaiSai(string type, string status)
    {
        var id = TaoYeuCau(type, status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DuyetSauKhiXemAsync(id, _chuNhaId, default));
        Assert.Equal("TRONG", TrangThaiPhong());
    }

    [Fact]
    public async Task DuyetSauKhiXem_ChanChuKhacVaSucChuaDaThayDoi()
    {
        var id = TaoYeuCau(LichHenTrangThai.LoaiXemPhong, LichHenTrangThai.DaHenLich);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DuyetSauKhiXemAsync(id, _chuNhaKhacId, default));
        Insert("UPDATE phong_tro SET so_nguoi_toi_da=1 WHERE id=$id", ("$id", _phongId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DuyetSauKhiXemAsync(id, _chuNhaId, default));
        Assert.Equal("TRONG", TrangThaiPhong());
        Assert.Equal("DA_HEN_LICH", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DuyetSauKhiXem_RollbackNeuGhiLichSuLoi()
    {
        var id = TaoYeuCau(LichHenTrangThai.LoaiXemPhong, LichHenTrangThai.DaHenLich);
        Insert("CREATE TRIGGER viewing_audit_fail BEFORE INSERT ON yeu_cau_thue_lich_su BEGIN SELECT RAISE(ABORT,'test failure'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => _service.DuyetSauKhiXemAsync(id, _chuNhaId, default));
        Assert.Equal("TRONG", TrangThaiPhong());
        Assert.Equal("DA_HEN_LICH", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", _tinDangId)));
    }

    [Fact]
    public async Task Duyet_DuyetYeuCauVaChuyenPhongSangDaDatCoc()
    {
        var id = TaoYeuCau();

        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);

        Assert.Equal(LichHenTrangThai.DaDuyet, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(_chuNhaId.ToString(), Scalar("SELECT nguoi_xu_ly_id FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(Now(), Scalar("SELECT ngay_xu_ly FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("1", Scalar("SELECT phien_ban FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.PhongDaDatCoc, TrangThaiPhong());
        Assert.Equal("1", Scalar("SELECT phien_ban FROM phong_tro WHERE id=$id", ("$id", _phongId)));
    }

    [Fact]
    public async Task Duyet_GhiLichSuVaThongBaoChoKhach()
    {
        var id = TaoYeuCau();

        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);

        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(HanhDongYeuCau.DuyetThueNgay, Scalar("SELECT hanh_dong FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai_cu FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.DaDuyet, Scalar("SELECT trang_thai_moi FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal("CHU_NHA", Scalar("SELECT vai_tro_luc_thuc_hien FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(Now(), Scalar("SELECT thoi_diem FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));

        Assert.Equal(LoaiThongBaoYeuCau.YeuCauDuyet, Scalar("SELECT loai FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(_khachAccountId.ToString(), Scalar("SELECT nguoi_nhan_id FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Contains("L6-A101", Scalar("SELECT noi_dung FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task Duyet_KhongGuiThongBaoChoChinhNguoiThaoTac()
    {
        var id = TaoYeuCau();
        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);
        Assert.Equal("DA_CHO_THUE", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", _tinDangId)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_thong_bao WHERE nguoi_nhan_id=$tk", ("$tk", _chuNhaId)));
    }

    [Fact]
    public async Task Duyet_GiuNguyenLichHenDaHen()
    {
        // Duyệt thuê ngay không xoá lịch: S2-09 còn cần thấy mốc đã hẹn.
        var lichHen = _hienTai.AddDays(1);
        var id = TaoYeuCau(LichHenTrangThai.LoaiThueNgay, LichHenTrangThai.DaHenLich, lichHen);

        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);

        Assert.Equal(LichHenTrangThai.DaDuyet, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(lichHen.ToString("yyyy-MM-dd HH:mm:ss"), Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(lichHen.ToString("yyyy-MM-dd HH:mm:ss"),
            Scalar("SELECT lich_hen_cu FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task Duyet_YeuCauDaHenLich_VanDuyetDuoc()
    {
        var id = TaoYeuCau(LichHenTrangThai.LoaiThueNgay, LichHenTrangThai.DaHenLich, _hienTai.AddDays(1));

        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);

        Assert.Equal(LichHenTrangThai.DaDuyet, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai_cu FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task Duyet_YeuCauXemPhong_ThenTuChoi()
    {
        // Yêu cầu xem phòng phải qua xác nhận lịch hoặc từ chối, không duyệt thẳng được.
        var id = TaoYeuCau(LichHenTrangThai.LoaiXemPhong);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DuyetThueNgayAsync(id, _chuNhaId, default));

        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.PhongTrong, TrangThaiPhong());
    }

    [Fact]
    public async Task Duyet_PhongDaCoNguoi_ThenTuChoiVaGiuNguyenTrangThaiPhong()
    {
        foreach (var trangThai in new[] { "DANG_THUE", "DA_DAT_COC", "NGUNG_CHO_THUE" })
        {
            DatTrangThaiPhong(trangThai);
            var id = TaoYeuCau();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.DuyetThueNgayAsync(id, _chuNhaId, default));

            Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
            Assert.Equal(trangThai, TrangThaiPhong());
            Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        }
    }

    [Theory]
    [InlineData(LichHenTrangThai.TuChoi)]
    [InlineData(LichHenTrangThai.DaHuy)]
    [InlineData(LichHenTrangThai.DaDuyet)]
    public async Task Duyet_YeuCauDaDong_ThenTuChoi(string trangThai)
    {
        var id = TaoYeuCau(LichHenTrangThai.LoaiThueNgay, trangThai);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DuyetThueNgayAsync(id, _chuNhaId, default));

        Assert.Equal(trangThai, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.PhongTrong, TrangThaiPhong());
    }

    [Fact]
    public async Task Duyet_LoiGiuaChung_KhongLamYeuCauVaPhongLechTrangThai()
    {
        // Cả hai UPDATE đã chạy thành công khi bản ghi lịch sử hỏng. Nếu không gộp chung
        // transaction, yêu cầu sẽ thành Đã duyệt và phòng thành Đã đặt cọc dù bản ghi lịch
        // sử — thứ hai mà người dùng và S2-09 cần — không tồn tại.
        Insert("""
            CREATE TRIGGER chan_ban_ghi_lich_su BEFORE INSERT ON yeu_cau_thue_lich_su
            BEGIN SELECT RAISE(ABORT, 'lich su hong'); END;
            """);
        var id = TaoYeuCau();

        await Assert.ThrowsAnyAsync<Exception>(() => _service.DuyetThueNgayAsync(id, _chuNhaId, default));

        Assert.Equal("DANG_HIEN_THI", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", _tinDangId)));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT phien_ban FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.PhongTrong, TrangThaiPhong());
        Assert.Equal("0", Scalar("SELECT phien_ban FROM phong_tro WHERE id=$id", ("$id", _phongId)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task Duyet_GhiNhatKyHoatDongChuyenPhongSangDaDatCoc()
    {
        var id = TaoYeuCau();

        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);

        Assert.Equal("1", Scalar(
            "SELECT COUNT(*) FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='phong_tro' AND doi_tuong_id=$phong AND hanh_dong='DOI_TRANG_THAI'",
            ("$phong", _phongId)));
        Assert.Equal(_chuNhaId.ToString(), Scalar(
            "SELECT nguoi_thuc_hien_id FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='phong_tro' AND doi_tuong_id=$phong",
            ("$phong", _phongId)));
        Assert.Equal(ChuNhaHoTen, Scalar(
            "SELECT ten_nguoi_thuc_hien FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='phong_tro' AND doi_tuong_id=$phong",
            ("$phong", _phongId)));
        Assert.Equal("CHU_NHA", Scalar(
            "SELECT vai_tro_luc_thuc_hien FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='phong_tro' AND doi_tuong_id=$phong",
            ("$phong", _phongId)));
        Assert.Equal("{\"trang_thai\":\"TRONG\"}", Scalar(
            "SELECT du_lieu_truoc FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='phong_tro' AND doi_tuong_id=$phong",
            ("$phong", _phongId)));
        Assert.Equal("{\"trang_thai\":\"DA_DAT_COC\"}", Scalar(
            "SELECT du_lieu_sau FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='phong_tro' AND doi_tuong_id=$phong",
            ("$phong", _phongId)));
        Assert.Equal(Now(), Scalar(
            "SELECT thoi_diem FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='phong_tro' AND doi_tuong_id=$phong",
            ("$phong", _phongId)));
    }

    [Fact]
    public async Task Duyet_LoiGiuaChung_KhongGhiNhatKyPhong()
    {
        Insert("""
            CREATE TRIGGER chan_ban_ghi_lich_su BEFORE INSERT ON yeu_cau_thue_lich_su
            BEGIN SELECT RAISE(ABORT, 'lich su hong'); END;
            """);
        var id = TaoYeuCau();

        await Assert.ThrowsAnyAsync<Exception>(() => _service.DuyetThueNgayAsync(id, _chuNhaId, default));

        Assert.Equal("0", Scalar(
            "SELECT COUNT(*) FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='phong_tro' AND doi_tuong_id=$phong",
            ("$phong", _phongId)));
        Assert.Equal(LichHenTrangThai.PhongTrong, TrangThaiPhong());
    }

    [Fact]
    public async Task Duyet_KhongPhaiChuNhaPhong_ThenTuChoi()
    {
        // KHACH_THUE và cả chủ nhà khác đều phải bị chặn.
        var id = TaoYeuCau();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DuyetThueNgayAsync(id, _khachAccountId, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DuyetThueNgayAsync(id, _chuNhaKhacId, default));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.PhongTrong, TrangThaiPhong());
    }

    [Fact]
    public async Task Duyet_YeuCauKhongTonTai_ThenBaoKhongTimThay()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.DuyetThueNgayAsync(999999, _chuNhaId, default));
    }

    [Fact]
    public async Task Duyet_HaiYeuCauCungPhong_ChiYeuCauDauTienChuyenPhong()
    {
        // Phòng chỉ đặt cọc một lần: yêu cầu thứ hai phải bị chặn và phòng giữ nguyên.
        var thuNhat = TaoYeuCau();
        var thuHai = TaoYeuCau();

        await _service.DuyetThueNgayAsync(thuNhat, _chuNhaId, default);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DuyetThueNgayAsync(thuHai, _chuNhaId, default));

        Assert.Equal(LichHenTrangThai.DaDuyet, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", thuNhat)));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", thuHai)));
        Assert.Equal(LichHenTrangThai.PhongDaDatCoc, TrangThaiPhong());
        Assert.Equal("1", Scalar("SELECT phien_ban FROM phong_tro WHERE id=$id", ("$id", _phongId)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", thuHai)));
    }

    [Fact]
    public async Task Duyet_KhachChuaLienKetTaiKhoan_VanDuyetDuoc_ChiBoThongBao()
    {
        var khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES (NULL,'Khách vô danh',$now)", ("$now", Now()));
        var id = (int)Insert(
            "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
                + "so_nguoi_du_kien,trang_thai,ngay_tao,phien_ban) "
                + "VALUES ($ma,$tin,$khach,'THUE_NGAY','2026-04-01',2,'MOI',$now,0)",
            ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
            ("$tin", _tinDangId), ("$khach", khachId), ("$now", Now()));

        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);

        Assert.Equal(LichHenTrangThai.DaDuyet, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.PhongDaDatCoc, TrangThaiPhong());
        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task Duyet_TrangThaiPhongVaPhongIdDocDungTuTinDang()
    {
        // DocSql đi qua tin_dang để lấy phòng, không đọc yeu_cau_thue.phong_id.
        var id = TaoYeuCau();
        var doc = await _service.DocAsync(id, default);
        Assert.Equal(_phongId, doc!.PhongId);
        Assert.Equal(LichHenTrangThai.PhongTrong, doc.TrangThaiPhong);
        Assert.Equal(LichHenTrangThai.LoaiThueNgay, doc.LoaiYeuCau);
    }

    [Fact]
    public async Task Duyet_SauKhiDuyet_PhongVaYeuCauCungVaDaDatCoc()
    {
        var id = TaoYeuCau();

        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);

        var doc = await _service.DocAsync(id, default);
        Assert.Equal(LichHenTrangThai.DaDuyet, doc!.TrangThai);
        Assert.Equal(LichHenTrangThai.PhongDaDatCoc, doc.TrangThaiPhong);
        Assert.Equal(1, doc.PhienBanPhong);
    }

    [Fact]
    public async Task Duyet_XemLaiSauDuyet_KhongTheDuyetLanNua()
    {
        var id = TaoYeuCau();
        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DuyetThueNgayAsync(id, _chuNhaId, default));
        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public void HanhDongDuyetThueNgay_CoNhanTiengViet()
    {
        Assert.Equal("Chủ nhà duyệt thuê ngay", HanhDongYeuCau.Label(HanhDongYeuCau.DuyetThueNgay));
    }

    private void DatTrangThaiPhong(string trangThai) => Insert(
        "UPDATE phong_tro SET trang_thai=$ts WHERE id=$id", ("$ts", trangThai), ("$id", _phongId));
}
