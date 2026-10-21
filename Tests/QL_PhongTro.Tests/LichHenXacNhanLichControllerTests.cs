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
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace QL_PhongTro.Tests;

// S2-08 lát 1, AC1: phải chọn ngày giờ cụ thể thì mới xác nhận được. Ràng buộc nằm ở controller vì
// LichHenXacNhanLichAsync nhận DateTime không rỗng, nên test phải gọi controller thay vì gọi service.
public class LichHenXacNhanLichControllerTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà S2-08 l1ctl";
    private const string KhachHoTen = "Khách S2-08 l1ctl";

    private readonly string _dbPath;
    private readonly AppDbContext _db;
    private readonly MockTimeProvider _clock = new();
    private readonly LichHenService _service;

    private int _chuNhaId;
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

    public LichHenXacNhanLichControllerTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"s2_08_l1_ctl_{Guid.NewGuid():N}.sqlite");

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
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "s2_08_l1ctl_chu_nha@test.local");
        _khachAccountId = TaiKhoanMoi(KhachHoTen, "KHACH_THUE", "s2_08_l1ctl_khach@test.local");

        var toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà S2-08 l1ctl','1 Đường S2-08',1,1)", ("$chu", _chuNhaId));

        var phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L1CTL-A101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));

        _tinDangId = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
                + "VALUES ($phong,$chu,'Tin L1CTL-A101','Nội dung','DANG_HIEN_THI',$now)",
            ("$phong", phongId), ("$chu", _chuNhaId), ("$now", Now()));

        _khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES ($tk,$ten,$now)",
            ("$tk", _khachAccountId), ("$ten", KhachHoTen), ("$now", Now()));
    }

    private int TaoYeuCau(string trangThai = LichHenTrangThai.Moi, DateTime? lichHenUtc = null) => (int)Insert(
        "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
            + "so_nguoi_du_kien,lich_hen,trang_thai,ngay_tao,phien_ban) "
            + "VALUES ($ma,$tin,$khach,'XEM_PHONG','2026-04-01',2,$lich,$trangThai,$now,0)",
        ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
        ("$tin", _tinDangId), ("$khach", _khachId),
        ("$lich", lichHenUtc is { } l ? l.ToString("yyyy-MM-dd HH:mm:ss") : null),
        ("$trangThai", trangThai), ("$now", Now()));

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
    public async Task XacNhanLich_ThieuNgayGio_ThenBaoChonVaKhongDoiYeuCau()
    {
        var id = TaoYeuCau();
        var controller = ControllerCho(_chuNhaId);

        var result = await controller.XacNhanLich(id, null, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ChiTiet", redirect.ActionName);
        Assert.Equal("Hãy chọn ngày giờ hẹn.", (string?)controller.TempData["LichHenError"]);
        Assert.Equal(LichHenTrangThai.Moi, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("", Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task XacNhanLich_CoNgayGio_ThenXacNhanDuoc()
    {
        var id = TaoYeuCau();
        var controller = ControllerCho(_chuNhaId);

        // datetime-local gửi lên giờ Việt Nam, controller đổi sang UTC trước khi so sánh.
        var nhap = new DateTime(2026, 3, 12, 10, 0, 0, DateTimeKind.Unspecified);
        await controller.XacNhanLich(id, nhap, default);

        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal(nhap.AddHours(-7).ToString("yyyy-MM-dd HH:mm:ss"),
            Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task XacNhanLich_Luc0GioSang_ThenLuuDungUtc()
    {
        var id = TaoYeuCau();
        var controller = ControllerCho(_chuNhaId);
        var nhap = new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Unspecified);

        await controller.XacNhanLich(id, nhap, default);

        Assert.Equal(LichHenTrangThai.DaHenLich, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("2026-03-10 17:00:00", Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
    }

    [Fact]
    public async Task DoiLich_ThieuNgayGio_ThenBaoChonVaGiuNguyenLichCu()
    {
        var lichCu = _hienTai.AddDays(1);
        var id = TaoYeuCau(LichHenTrangThai.DaHenLich, lichCu);
        var controller = ControllerCho(_chuNhaId);

        var result = await controller.DoiLich(id, null, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Hãy chọn ngày giờ hẹn mới.", (string?)controller.TempData["LichHenError"]);
        Assert.Equal(lichCu.ToString("yyyy-MM-dd HH:mm:ss"),
            Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", id)));
    }

    [Fact]
    public async Task LichTrung_ChonChuaCoNgayGio_ThenKhongBaoLoiVaKhongCanhBao()
    {
        // Cảnh báo trùng lịch chỉ là gợi ý: thiếu ngày giờ thì trả danh sách rỗng, không ném lỗi.
        var id = TaoYeuCau();
        var controller = ControllerCho(_chuNhaId);

        var result = await controller.LichTrung(id, null, default);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(json.Value!).Cast<object>());
    }
}
