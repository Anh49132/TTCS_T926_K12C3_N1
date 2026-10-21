using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace QL_PhongTro.Tests;

// S2-08 lát 5: CSDL chưa có bảng yeu_cau_thue thì trang phải báo trạng thái chờ dữ liệu chứ không
// ném lỗi 500. Bảng đó thuộc S2-06 và chỉ được tạo bởi RentalRequestSchema, nên đây là tình huống
// thật trên máy khác. Ngược lại, khi bảng đã có mà mã yêu cầu không tồn tại hoặc người xem không
// phải chủ nhà/khách của yêu cầu thì vẫn phải giấu, không lộ yêu cầu nào đang có.
public class LichHenChuaCoDuLieuTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà S2-08 l5cd";
    private const string ChuNhaKhacHoTen = "Chủ nhà khác S2-08 l5cd";
    private const string KhachHoTen = "Khách S2-08 l5cd";
    private const string KhachKhacHoTen = "Khách khác S2-08 l5cd";

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
    private int _yeuCauId;

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
    }

    public LichHenChuaCoDuLieuTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"s2_08_l5_chuadulieu_{Guid.NewGuid():N}.sqlite");

        LocalDatabaseInitializer.Create(_dbPath, seedPath);
        RentalRequestSchema.Initialize(_dbPath);
        DatabaseUpdates.Update(_dbPath, seedPath);

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + _dbPath).Options);
        _clock.UtcNow = new DateTime(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc);
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

    private string Now() => _clock.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

    private int TaiKhoanMoi(string hoTen, string vaiTro, string email) => (int)Insert(
        "INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,"
            + "ngay_tao,ngay_cap_nhat,must_change_password,email_confirmed,is_deleted) "
            + "VALUES ($ten,$email,$sdt,'x',$vaiTro,1,0,0,$now,$now,0,1,0)",
        ("$ten", hoTen), ("$email", email), ("$sdt", "09" + Random.Shared.Next(10000000, 99999999)),
        ("$vaiTro", vaiTro), ("$now", Now()));

    private void TaoDuLieu()
    {
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "s2_08_l5cd_chu_nha@test.local");
        _chuNhaKhacId = TaiKhoanMoi(ChuNhaKhacHoTen, "CHU_NHA", "s2_08_l5cd_chu_nha_khac@test.local");
        _khachAccountId = TaiKhoanMoi(KhachHoTen, "KHACH_THUE", "s2_08_l5cd_khach@test.local");
        _khachKhacAccountId = TaiKhoanMoi(KhachKhacHoTen, "KHACH_THUE", "s2_08_l5cd_khach_khac@test.local");

        var toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà S2-08 l5cd','1 Đường S2-08',1,1)", ("$chu", _chuNhaId));

        var phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L5CD-A101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));

        _tinDangId = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
                + "VALUES ($phong,$chu,'Tin L5CD-A101','Nội dung','DANG_HIEN_THI',$now)",
            ("$phong", phongId), ("$chu", _chuNhaId), ("$now", Now()));

        _khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES ($tk,$ten,$now)",
            ("$tk", _khachAccountId), ("$ten", KhachHoTen), ("$now", Now()));

        _yeuCauId = (int)Insert(
            "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
                + "so_nguoi_du_kien,trang_thai,ngay_tao,phien_ban) "
                + "VALUES ($ma,$tin,$khach,'XEM_PHONG','2026-04-01',2,'MOI',$now,0)",
            ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
            ("$tin", _tinDangId), ("$khach", _khachId), ("$now", Now()));
    }

    private void XoaBangYeuCau() => Insert("DROP TABLE yeu_cau_thue");

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
    public async Task CoBangYeuCau_TrueKhiDaCoBang()
    {
        Assert.True(await _service.CoBangYeuCauAsync(default));
    }

    [Fact]
    public async Task CoBangYeuCau_FalseKhiChuaCoBang()
    {
        XoaBangYeuCau();

        Assert.False(await _service.CoBangYeuCauAsync(default));
    }

    [Fact]
    public async Task ChiTiet_ChuaCoBang_ThenHienThongBaoChuaCoDuLieu()
    {
        XoaBangYeuCau();

        var result = await ControllerCho(_chuNhaId).ChiTiet(_yeuCauId, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("ChuaCoDuLieu", view.ViewName);
    }

    [Fact]
    public async Task DocAsync_ChuaCoBang_ThenKhongNemLoi()
    {
        // Mọi action đều đi qua LoadAsync, nên chỉ cần một chỗ này là không còn lỗi 500.
        XoaBangYeuCau();

        Assert.Null(await _service.DocAsync(_yeuCauId, default));
    }

    [Fact]
    public async Task XacNhanLich_ChuaCoBang_ThenBaoKhongTimThay()
    {
        XoaBangYeuCau();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.XacNhanLichAsync(_yeuCauId, _chuNhaId, _clock.UtcNow.AddDays(1), default));
    }

    [Fact]
    public async Task ChiTiet_DaCoBangMaMaYeuCauKhongTonTai_ThenNotFound()
    {
        var result = await ControllerCho(_chuNhaId).ChiTiet(999999, default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ChiTiet_KhongPhaiChuNhaHayKhach_ThenForbid()
    {
        Assert.IsType<ForbidResult>(await ControllerCho(_chuNhaKhacId).ChiTiet(_yeuCauId, default));
    }

    [Fact]
    public async Task ChiTiet_KhachKhacKhongXemDuocYeuCauNguoiKhac()
    {
        // Khách của yêu cầu khác cũng phải bị chặn, không lộ yêu cầu đang tồn tại.
        Assert.IsType<ForbidResult>(await ControllerCho(_khachKhacAccountId).ChiTiet(_yeuCauId, default));
    }

    [Fact]
    public async Task ChiTiet_ChuNhaVaKhachCuaYeuCau_ThenXemDuoc()
    {
        Assert.IsType<ViewResult>(await ControllerCho(_chuNhaId).ChiTiet(_yeuCauId, default));
        Assert.IsType<ViewResult>(await ControllerCho(_khachAccountId).ChiTiet(_yeuCauId, default));
    }
}
