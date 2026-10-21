using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace QL_PhongTro.Tests;

// S2-08 lát 5, AC3: mỗi lần đổi trạng thái đều ghi lịch sử và khách xem được lịch sử đó.
// Dùng lại đúng harness của lát 3, mọi câu lệnh SQL thuần để không phụ thuộc entity S2-06.
public class LichHenLichSuTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà S2-08 l5";
    private const string ChuNhaKhacHoTen = "Chủ nhà khác S2-08 l5";
    private const string KhachHoTen = "Khách S2-08 l5";
    private const string KhachKhacHoTen = "Khách khác S2-08 l5";

    private readonly string _dbPath;
    private readonly AppDbContext _db;
    private readonly MockTimeProvider _clock = new();
    private readonly LichHenService _service;

    private int _chuNhaId;
    private int _chuNhaKhacId;
    private int _khachAccountId;
    private int _khachKhacAccountId;
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

    public LichHenLichSuTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"s2_08_l5_{Guid.NewGuid():N}.sqlite");

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
            + "VALUES ($ten,$email,$sdt,'x',$vaiTro,1,0,0,$now,$now,0,1,0)",
        ("$ten", hoTen), ("$email", email), ("$sdt", "09" + Random.Shared.Next(10000000, 99999999)),
        ("$vaiTro", vaiTro), ("$now", Now()));

    private void TaoDuLieu()
    {
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "s2_08_l5_chu_nha@test.local");
        _chuNhaKhacId = TaiKhoanMoi(ChuNhaKhacHoTen, "CHU_NHA", "s2_08_l5_chu_nha_khac@test.local");
        _khachAccountId = TaiKhoanMoi(KhachHoTen, "KHACH_THUE", "s2_08_l5_khach@test.local");
        _khachKhacAccountId = TaiKhoanMoi(KhachKhacHoTen, "KHACH_THUE", "s2_08_l5_khach_khac@test.local");

        var toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà S2-08 l5','1 Đường S2-08',1,1)", ("$chu", _chuNhaId));

        var phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L5-A101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));

        _tinDangId = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
                + "VALUES ($phong,$chu,'Tin L5-A101','Nội dung','DANG_HIEN_THI',$now)",
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

    private string CountLichSu(int yeuCauId) =>
        Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", yeuCauId));

    private LichHenController ControllerCho(int accountId)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "test")) };
        http.RequestServices = new EmptyServiceProvider();
        var controller = new LichHenController(_service)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            Url = new StubUrlHelper()
        };
        controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        return controller;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => "/";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/";
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }

    [Fact]
    public async Task LichSu_ChuaCoThayDoi_ThenChiCoDongGửiYêuCầu()
    {
        // Dòng mở đầu được suy ra lúc hiển thị, không nằm trong bảng.
        var id = TaoYeuCau();

        var muc = Assert.Single(await _service.LichSuAsync(_chuNhaId, id, default));

        Assert.Equal(HanhDongYeuCau.TaoYeuCau, muc.HanhDong);
        Assert.Equal(LichHenTrangThai.Moi, muc.TrangThaiMoi);
        Assert.Null(muc.TrangThaiCu);
    }

    [Fact]
    public async Task LichSu_DongGửiYêuCầu_CóNgườiThựcHiệnVàThờiĐiểm()
    {
        // AC3 đòi cả người thực hiện lẫn thời điểm, kể cả ở dòng mở đầu. Thời điểm lấy từ
        // ngay_tao của yêu cầu nên đúng lúc khách gửi, không phải lúc mở trang.
        var id = TaoYeuCau();

        var muc = Assert.Single(await _service.LichSuAsync(_khachAccountId, id, default));

        Assert.Equal(KhachHoTen, muc.TenNguoiThucHien);
        Assert.Equal(LichHenTrangThai.VaiTroKhachThue, muc.VaiTro);
        Assert.Equal(_hienTai, muc.ThoiDiem);
    }

    [Fact]
    public async Task LichSu_DongGửiYêuCầu_KhongGhiVaoBảng()
    {
        var id = TaoYeuCau();

        await _service.LichSuAsync(_khachAccountId, id, default);
        await _service.LichSuAsync(_khachAccountId, id, default);

        Assert.Equal("0", CountLichSu(id));
    }

    [Fact]
    public async Task LichSu_KhachVaChuNhaCuaYeuCau_ThenCungXemDuoc()
    {
        var id = TaoYeuCau();
        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);

        var chuNha = await _service.LichSuAsync(_chuNhaId, id, default);
        var khach = await _service.LichSuAsync(_khachAccountId, id, default);

        Assert.Equal(2, chuNha.Count);
        Assert.Equal(chuNha.Select(x => x.HanhDong), khach.Select(x => x.HanhDong));
    }

    [Fact]
    public async Task LichSu_DocDuocNguoiThucHienVaThoiDiem()
    {
        // AC3 yêu cầu lịch sử có người thực hiện và thời điểm.
        var id = TaoYeuCau();
        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);

        var muc = Assert.Single(await _service.LichSuAsync(_khachAccountId, id, default),
            x => x.HanhDong == HanhDongYeuCau.XacNhan);

        Assert.Equal(ChuNhaHoTen, muc.TenNguoiThucHien);
        Assert.Equal(LichHenTrangThai.VaiTroChuNha, muc.VaiTro);
        Assert.Equal(_hienTai, muc.ThoiDiem);
        Assert.Equal(LichHenTrangThai.Moi, muc.TrangThaiCu);
        Assert.Equal(LichHenTrangThai.DaHenLich, muc.TrangThaiMoi);
    }

    [Fact]
    public async Task LichSu_MoiNhatDauTien()
    {
        var id = TaoYeuCau();
        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);
        _clock.UtcNow = _hienTai.AddDays(2);
        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, "khách đã thuê chỗ khác", default);

        var lichSu = await _service.LichSuAsync(_chuNhaId, id, default);

        Assert.Equal(
            [HanhDongYeuCau.TuChoi, HanhDongYeuCau.XacNhan, HanhDongYeuCau.TaoYeuCau],
            lichSu.Select(x => x.HanhDong));
    }

    [Fact]
    public async Task LichSu_GiuLichCuVaMoiKhiDoiLich()
    {
        var lichCu = _hienTai.AddDays(1);
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, lichCu);
        var lichMoi = lichCu.AddHours(3);

        await _service.DoiLichAsync(id, _chuNhaId, lichMoi, default);

        var muc = Assert.Single(await _service.LichSuAsync(_khachAccountId, id, default),
            x => x.HanhDong == HanhDongYeuCau.DoiLich);
        Assert.Equal(lichCu, muc.LichHenCu);
        Assert.Equal(lichMoi, muc.LichHenMoi);
    }

    [Fact]
    public async Task LichSu_GiuLyDoVaGhiChuKhiTuChoi()
    {
        var id = TaoYeuCau();

        await _service.TuChoiAsync(id, _chuNhaId, LyDoTuChoi.Khac, "khách đã thuê chỗ khác", default);

        var muc = Assert.Single(await _service.LichSuAsync(_khachAccountId, id, default),
            x => x.HanhDong == HanhDongYeuCau.TuChoi);
        Assert.Equal(LyDoTuChoi.Khac, muc.LyDoTuChoi);
        Assert.Equal("khách đã thuê chỗ khác", muc.GhiChuTuChoi);
    }

    [Fact]
    public async Task LichSu_ChiDongMotYeuCau()
    {
        var id = TaoYeuCau();
        var idKhac = TaoYeuCau();
        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);

        Assert.Equal(2, (await _service.LichSuAsync(_chuNhaId, id, default)).Count);
        Assert.Single(await _service.LichSuAsync(_chuNhaId, idKhac, default));
    }

    [Fact]
    public async Task LichSu_KhachKhacVaChuNhaKhac_ThenKhongXemDuoc()
    {
        var id = TaoYeuCau();
        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.LichSuAsync(_khachKhacAccountId, id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.LichSuAsync(_chuNhaKhacId, id, default));
    }

    [Fact]
    public async Task LichSu_YeuCauKhongTonTai_ThenKhongTimThay()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.LichSuAsync(_chuNhaId, 999999, default));
    }

    [Fact]
    public async Task LichSu_ChiDoc_KhongThemBanGhiMoi()
    {
        // Xem lịch sử không được ghi thêm vào bảng, nên mở nhiều lần vẫn đúng số dòng.
        var id = TaoYeuCau();
        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);

        await _service.LichSuAsync(_khachAccountId, id, default);
        await _service.LichSuAsync(_khachAccountId, id, default);

        Assert.Equal("1", CountLichSu(id));
    }

    [Fact]
    public async Task ChiTiet_KhachCuaYeuCau_ThayLichSu()
    {
        var id = TaoYeuCau();
        await _service.XacNhanLichAsync(id, _chuNhaId, _hienTai.AddDays(1), default);

        var view = await ControllerCho(_khachAccountId).ChiTiet(id, default);

        var model = Assert.IsType<LichHenChiTietViewModel>(Assert.IsType<ViewResult>(view).Model);
        Assert.Equal(2, model.LichSu.Count);
        // Mới đến cũ: xác nhận ở trên, dòng gửi yêu cầu ở dưới cùng.
        Assert.Equal(HanhDongYeuCau.XacNhan, model.LichSu[0].HanhDong);
        Assert.Equal(HanhDongYeuCau.TaoYeuCau, model.LichSu[^1].HanhDong);
        Assert.False(model.LaChuNha);
        Assert.True(model.HienThiNutHuy);
    }

    [Fact]
    public async Task ChiTiet_KhachKhac_ThenForbid()
    {
        var id = TaoYeuCau();

        Assert.IsType<ForbidResult>(await ControllerCho(_khachKhacAccountId).ChiTiet(id, default));
    }

    [Theory]
    [InlineData(HanhDongYeuCau.XacNhan, "Chủ nhà xác nhận lịch hẹn")]
    [InlineData(HanhDongYeuCau.DoiLich, "Chủ nhà đổi lịch hẹn")]
    [InlineData(HanhDongYeuCau.DuyetThueNgay, "Chủ nhà duyệt thuê ngay")]
    [InlineData(HanhDongYeuCau.TuChoi, "Chủ nhà từ chối yêu cầu")]
    [InlineData(HanhDongYeuCau.Huy, "Hủy yêu cầu")]
    [InlineData(HanhDongYeuCau.TaoYeuCau, "Khách gửi yêu cầu")]
    public void HanhDongLabel_HienThiDungTen(string hanhDong, string nhan)
    {
        Assert.Equal(nhan, HanhDongYeuCau.Label(hanhDong));
    }
}
