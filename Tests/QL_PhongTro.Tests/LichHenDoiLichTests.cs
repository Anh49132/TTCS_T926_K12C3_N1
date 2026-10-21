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

// S2-08 lát 4: chủ nhà đổi lịch hẹn và giữ lại lịch cũ.
// Dùng lại đúng harness của lát 1, mọi câu lệnh SQL thuần để không phụ thuộc entity S2-06.
public class LichHenDoiLichTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà S2-08 l4";
    private const string ChuNhaKhacHoTen = "Chủ nhà khác S2-08 l4";
    private const string KhachHoTen = "Khách S2-08 l4";

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

    public LichHenDoiLichTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"s2_08_l4_{Guid.NewGuid():N}.sqlite");

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
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "s2_08_l4_chu_nha@test.local");
        _chuNhaKhacId = TaiKhoanMoi(ChuNhaKhacHoTen, "CHU_NHA", "s2_08_l4_chu_nha_khac@test.local");
        _khachAccountId = TaiKhoanMoi(KhachHoTen, "KHACH_THUE", "s2_08_l4_khach@test.local");

        var toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà S2-08 l4','1 Đường S2-08',1,1)", ("$chu", _chuNhaId));
        Insert("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà khác S2-08 l4','2 Đường S2-08',1,1)", ("$chu", _chuNhaKhacId));

        var phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L4-A101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));

        _tinDangId = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
                + "VALUES ($phong,$chu,'Tin L4-A101','Nội dung','DANG_HIEN_THI',$now)",
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

    private int TaoYeuCauDaHenLich() => TaoYeuCau(LichHenTrangThai.DaHenLich, _hienTai.AddDays(1));

    [Fact]
    public async Task DoiLich_DoiLichHenVaGiuNguyenTrangThaiDaHenLich()
    {
        var cu = _hienTai.AddDays(1);
        var id = TaoYeuCauDaHenLich();
        var moi = cu.AddHours(3);

        await _service.DoiLichAsync(id, _chuNhaId, moi, default);

        Assert.Equal(moi.ToString("yyyy-MM-dd HH:mm:ss"), Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        // Đổi lịch không đổi trạng thái nghiệp vụ: yêu cầu vẫn đang có lịch hẹn.
        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(_chuNhaId.ToString(), Scalar("SELECT nguoi_xu_ly_id FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(Now(), Scalar("SELECT ngay_xu_ly FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("1", Scalar("SELECT phien_ban FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DoiLich_GiuLichCuTrongLichSuKhongPhaiTrenYeuCauThue()
    {
        // yeu_cau_thue không có cột lich_hen_cu/da_doi_lich và thuộc S2-06, nên lịch cũ chỉ nằm ở lịch sử.
        var cu = _hienTai.AddDays(1);
        var id = TaoYeuCauDaHenLich();
        var moi = cu.AddHours(3);

        await _service.DoiLichAsync(id, _chuNhaId, moi, default);

        Assert.Equal(cu.ToString("yyyy-MM-dd HH:mm:ss"),
            Scalar("SELECT lich_hen_cu FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        var doc = await _service.DocAsync(id, default);
        Assert.True(doc!.DaDoiLich);
        Assert.Equal(cu, doc.LichHenCu);
        Assert.Equal(moi, doc.LichHen);
    }

    [Fact]
    public async Task DoiLich_GhiLichSuVaThongBaoChoKhach()
    {
        var cu = _hienTai.AddDays(1);
        var id = TaoYeuCauDaHenLich();
        var moi = cu.AddHours(3);

        await _service.DoiLichAsync(id, _chuNhaId, moi, default);

        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(HanhDongYeuCau.DoiLich, Scalar("SELECT hanh_dong FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai_cu FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai_moi FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(moi.ToString("yyyy-MM-dd HH:mm:ss"),
            Scalar("SELECT lich_hen_moi FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal("CHU_NHA", Scalar("SELECT vai_tro_luc_thuc_hien FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));

        Assert.Equal(LoaiThongBaoYeuCau.YeuCauDoiLich, Scalar("SELECT loai FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(_khachAccountId.ToString(), Scalar("SELECT nguoi_nhan_id FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
        var noiDung = Scalar("SELECT noi_dung FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id));
        Assert.Contains(LichHenService.HienThoiGio(cu), noiDung);
        Assert.Contains(LichHenService.HienThoiGio(moi), noiDung);
    }

    [Fact]
    public async Task DoiLich_KhongGuiThongBaoChoChinhNguoiThaoTac()
    {
        var id = TaoYeuCauDaHenLich();
        await _service.DoiLichAsync(id, _chuNhaId, _hienTai.AddDays(1).AddHours(3), default);
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_thong_bao WHERE nguoi_nhan_id=$tk", ("$tk", _chuNhaId)));
    }

    [Fact]
    public async Task DoiLich_HienThiChuDaDoiLich_HaVuDungMaTrangThai()
    {
        // AC4 yêu cầu thấy chữ "Đã đổi lịch" nhưng bộ lọc của S2-07 chỉ nhận 5 trạng thái cũ,
        // nên nhãn phải suy ra từ cờ đã đổi lịch chứ không ghi thêm trạng thái mới.
        var id = TaoYeuCauDaHenLich();
        await _service.DoiLichAsync(id, _chuNhaId, _hienTai.AddDays(1).AddHours(3), default);

        var doc = await _service.DocAsync(id, default);

        Assert.Equal(LichHenTrangThai.DaHenLich, doc!.TrangThai);
        Assert.True(doc.DaDoiLich);
        Assert.Equal("Đã đổi lịch hẹn", LichHenTrangThai.LabelHienThi(doc.TrangThai, doc.DaDoiLich));
    }

    [Fact]
    public async Task DoiLich_ChuaDoiLich_HienThiChuXacNhanLich()
    {
        var id = TaoYeuCauDaHenLich();

        var doc = await _service.DocAsync(id, default);

        Assert.False(doc!.DaDoiLich);
        Assert.Equal("Đã xác nhận lịch hẹn", LichHenTrangThai.LabelHienThi(doc.TrangThai, doc.DaDoiLich));
    }

    [Fact]
    public async Task DoiLich_HaiLanLienTiep_GiuDayDuChuoiLichCu()
    {
        var cu = _hienTai.AddDays(1);
        var id = TaoYeuCauDaHenLich();
        var giua = cu.AddHours(2);
        var moi = giua.AddHours(2);

        await _service.DoiLichAsync(id, _chuNhaId, giua, default);
        await _service.DoiLichAsync(id, _chuNhaId, moi, default);

        Assert.Equal(moi.ToString("yyyy-MM-dd HH:mm:ss"), Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("2", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));

        var doc = await _service.DocAsync(id, default);
        Assert.True(doc!.DaDoiLich);
        Assert.Equal(giua, doc.LichHenCu);

        var chuoi = DocChuoiLichSu(id);
        Assert.Equal(2, chuoi.Count);
        Assert.Equal(cu, chuoi[0].Cu);
        Assert.Equal(giua, chuoi[0].Moi);
        Assert.Equal(giua, chuoi[1].Cu);
        Assert.Equal(moi, chuoi[1].Moi);
    }

    [Fact]
    public async Task DoiLich_YeuCauChuaHenLich_ThenTuChoi()
    {
        // Chưa có lịch thì chưa có gì để đổi.
        var id = TaoYeuCau();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DoiLichAsync(id, _chuNhaId, _hienTai.AddDays(2), default));
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DoiLich_TrangThaiDaHenLich_NhungChuaCoLichHen_ThenTuChoi()
    {
        // Trạng thái nói đã hẹn nhưng cột lịch trống: dữ liệu mâu thuẫn, không đổi được.
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DoiLichAsync(id, _chuNhaId, _hienTai.AddDays(2), default));
    }

    [Theory]
    [InlineData(LichHenTrangThai.TuChoi)]
    [InlineData(LichHenTrangThai.DaDuyet)]
    [InlineData(LichHenTrangThai.DaHuy)]
    public async Task DoiLich_YeuCauDaDong_ThenTuChoi(string trangThai)
    {
        var id = TaoYeuCau(trangThai, _hienTai.AddDays(1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DoiLichAsync(id, _chuNhaId, _hienTai.AddDays(2), default));
        Assert.Equal(trangThai, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DoiLich_TrungGioVoiLichCu_ThenTuChoi()
    {
        var cu = _hienTai.AddDays(1);
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, cu);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DoiLichAsync(id, _chuNhaId, cu, default));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        var doc = await _service.DocAsync(id, default);
        Assert.False(doc!.DaDoiLich);
        Assert.Null(doc.LichHenCu);
    }

    [Fact]
    public async Task DoiLich_LichMoiQuaKhan_ThenTuChoi()
    {
        var id = TaoYeuCauDaHenLich();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DoiLichAsync(id, _chuNhaId, _hienTai.AddMinutes(-30), default));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DoiLich_KhongPhaiChuNhaPhong_ThenTuChoi()
    {
        // KHACH_THUE và cả chủ nhà khác đều phải bị chặn.
        var cu = _hienTai.AddDays(1);
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, cu);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DoiLichAsync(id, _khachAccountId, cu.AddHours(2), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DoiLichAsync(id, _chuNhaKhacId, cu.AddHours(2), default));
        Assert.Equal(cu.ToString("yyyy-MM-dd HH:mm:ss"), Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DoiLich_YeuCauKhongTonTai_ThenBaoKhongTimThay()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.DoiLichAsync(999999, _chuNhaId, _hienTai.AddDays(2), default));
    }

    [Fact]
    public async Task DoiLich_KhachChuaLienKetTaiKhoan_VanDoiDuoc_ChiBoThongBao()
    {
        var khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES (NULL,'Khách vô danh',$now)", ("$now", Now()));
        var lichHen = _hienTai.AddDays(1);
        var id = (int)Insert(
            "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
                + "so_nguoi_du_kien,lich_hen,trang_thai,ngay_tao,phien_ban) "
                + "VALUES ($ma,$tin,$khach,'XEM_PHONG','2026-04-01',2,$lich,'DA_HEN_LICH',$now,0)",
            ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
            ("$tin", _tinDangId), ("$khach", khachId),
            ("$lich", lichHen.ToString("yyyy-MM-dd HH:mm:ss")), ("$now", Now()));

        await _service.DoiLichAsync(id, _chuNhaId, lichHen.AddHours(2), default);

        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("1", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_thong_bao WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DoiLich_GhiDongBoThoiDiemVaNguoiThucHien()
    {
        var id = TaoYeuCauDaHenLich();
        var moi = _hienTai.AddDays(2);

        await _service.DoiLichAsync(id, _chuNhaId, moi, default);

        Assert.Equal(Now(), Scalar("SELECT thoi_diem FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(_chuNhaId.ToString(),
            Scalar("SELECT nguoi_thuc_hien_id FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
        Assert.Equal(ChuNhaHoTen,
            Scalar("SELECT ten_nguoi_thuc_hien FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DoiLich_TrungLichVoiYeuCauKhac_ChiCanhBaoKhongChan()
    {
        // Cùng chủ nhà đã xác nhận một lịch khác trong vòng 30 phút: vẫn cho đổi,
        // đúng như cảnh báo ở lát 2.
        var lichHen = _hienTai.AddDays(1);
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, lichHen);
        var id2 = TaoYeuCau(LichHenTrangThai.DaHenLich, lichHen.AddMinutes(20));
        // 25 phút sau lịch của yêu cầu id, tức nằm trong cửa sổ ±30 phút.
        await _service.DoiLichAsync(id2, _chuNhaId, lichHen.AddMinutes(25), default);

        Assert.Equal(lichHen.AddMinutes(25).ToString("yyyy-MM-dd HH:mm:ss"),
            Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id2)));
        Assert.NotEmpty(await _service.TimLichTrungAsync(PhongId(), lichHen.AddMinutes(25), id2, default));
    }

    [Fact]
    public async Task DoiLich_GioLeNamRoiKhongBiLamTron()
    {
        // Lich lưu dạng TEXT nên phải đúng tới giây và giờ, không lệch nửa giờ như DateTime lưu kiểu số.
        var lichHen = new DateTime(2026, 4, 20, 7, 30, 0, DateTimeKind.Utc); // 14:30 giờ VN
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, lichHen);

        await _service.DoiLichAsync(id, _chuNhaId, lichHen.AddMinutes(90), default);

        Assert.Equal("2026-04-20 09:00:00", Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("2026-04-20 07:30:00", Scalar("SELECT lich_hen_cu FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DoiLich_DocVeVanChoThayLichTruocVaMoi()
    {
        var cu = _hienTai.AddDays(1);
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, cu);

        var chua = await _service.DocAsync(id, default);
        Assert.False(chua!.DaDoiLich);
        Assert.Null(chua.LichHenCu);

        await _service.DoiLichAsync(id, _chuNhaId, cu.AddHours(3), default);

        var sau = await _service.DocAsync(id, default);
        Assert.True(sau!.DaDoiLich);
        Assert.Equal(cu, sau.LichHenCu);
        Assert.Equal(cu.AddHours(3), sau.LichHen);
    }

    [Fact]
    public async Task DoiLich_KhongPhaiYeuCauCuaPhongNay_ThenTuChoi()
    {
        var toaNhaKhac = Scalar(
            "SELECT t.id FROM toa_nha t WHERE t.chu_nha_id=$id LIMIT 1", ("$id", _chuNhaKhacId));
        var phongKhac = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L4-B101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", int.Parse(toaNhaKhac)), ("$now", Now()));
        var tinKhac = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
                + "VALUES ($phong,$chu,'Tin L4-B101','Nội dung','DANG_HIEN_THI',$now)",
            ("$phong", phongKhac), ("$chu", _chuNhaKhacId), ("$now", Now()));
        var cu = _hienTai.AddDays(1);
        var id = (int)Insert(
            "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
                + "so_nguoi_du_kien,lich_hen,trang_thai,ngay_tao,phien_ban) "
                + "VALUES ($ma,$tin,$khach,'XEM_PHONG','2026-04-01',2,$lich,'DA_HEN_LICH',$now,0)",
            ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
            ("$tin", tinKhac), ("$khach", _khachId),
            ("$lich", cu.ToString("yyyy-MM-dd HH:mm:ss")), ("$now", Now()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DoiLichAsync(id, _chuNhaId, cu.AddHours(2), default));
        Assert.Equal(cu.ToString("yyyy-MM-dd HH:mm:ss"), Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public void HanhDongDoiLich_CoNhanTiengViet()
    {
        Assert.Equal("Chủ nhà đổi lịch hẹn", HanhDongYeuCau.Label(HanhDongYeuCau.DoiLich));
    }

    private int PhongId() => int.Parse(Scalar(
        "SELECT p.id FROM tin_dang td JOIN phong_tro p ON p.id = td.phong_id WHERE td.id = $id", ("$id", _tinDangId)));

    private System.Collections.Generic.List<(DateTime? Cu, DateTime? Moi)> DocChuoiLichSu(int id)
    {
        var connection = _db.Database.GetDbConnection();
        using var command = ((SqliteConnection)connection).CreateCommand();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        command.CommandText = "SELECT lich_hen_cu, lich_hen_moi FROM yeu_cau_thue_lich_su "
            + "WHERE yeu_cau_thue_id = $id ORDER BY id";
        command.Parameters.AddWithValue("$id", id);
        var ketQua = new System.Collections.Generic.List<(DateTime?, DateTime?)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            DateTime? Doc(int i) => reader.IsDBNull(i) ? null : DateTime.SpecifyKind(
                DateTime.Parse(reader.GetString(i)), DateTimeKind.Utc);
            ketQua.Add((Doc(0), Doc(1)));
        }
        return ketQua;
    }
}