using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using System;
using System.IO;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace QL_PhongTro.Tests;

// S2-08 lát 6, AC4: sau khi duyệt Thuê ngay, chủ nhà có nút "Lập hợp đồng".
// Màn hình hợp đồng thuộc S3-01 và chưa có trên dev, nên bài test này chỉ kiểm điều kiện hiện/ẩn
// của nút và trang tạm có đủ mã yêu cầu, không đoán route của S3-01.
public class LichHenLapHopDongTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà S2-08 l6hd";
    private const string ChuNhaKhacHoTen = "Chủ nhà khác S2-08 l6hd";
    private const string KhachHoTen = "Khách S2-08 l6hd";

    private readonly string _dbPath;
    private readonly AppDbContext _db;
    private readonly MockTimeProvider _clock = new();
    private readonly LichHenService _service;

    private int _chuNhaId;
    private int _chuNhaKhacId;
    private int _khachAccountId;
    private int _khachId;
    private int _tinDangId;
    private int _phongId;
    private DateTime _hienTai;

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
    }

    public LichHenLapHopDongTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"s2_08_l6_hopdong_{Guid.NewGuid():N}.sqlite");

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
            + "VALUES ($ten,$email,$sdt,'x',$vaiTro,1,0,0,$now,$now,0,1,0)",
        ("$ten", hoTen), ("$email", email), ("$sdt", "09" + Random.Shared.Next(10000000, 99999999)),
        ("$vaiTro", vaiTro), ("$now", Now()));

    private void TaoDuLieu()
    {
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "s2_08_l6hd_chu_nha@test.local");
        _chuNhaKhacId = TaiKhoanMoi(ChuNhaKhacHoTen, "CHU_NHA", "s2_08_l6hd_chu_nha_khac@test.local");
        _khachAccountId = TaiKhoanMoi(KhachHoTen, "KHACH_THUE", "s2_08_l6hd_khach@test.local");

        var toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà S2-08 l6hd','1 Đường S2-08',1,1)", ("$chu", _chuNhaId));

        _phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'L6HD-A101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));

        _tinDangId = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
                + "VALUES ($phong,$chu,'Tin L6HD-A101','Nội dung','DANG_HIEN_THI',$now)",
            ("$phong", _phongId), ("$chu", _chuNhaId), ("$now", Now()));

        _khachId = (int)Insert("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES ($tk,$ten,$now)",
            ("$tk", _khachAccountId), ("$ten", KhachHoTen), ("$now", Now()));
    }

    private int TaoYeuCau(string trangThai = LichHenTrangThai.Moi) => (int)Insert(
        "INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,"
            + "so_nguoi_du_kien,trang_thai,ngay_tao,phien_ban) "
            + "VALUES ($ma,$tin,$khach,$loai,'2026-04-01',2,$trangThai,$now,0)",
        ("$ma", "YC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()),
        ("$tin", _tinDangId), ("$khach", _khachId),
        ("$loai", LichHenTrangThai.LoaiThueNgay),
        ("$trangThai", trangThai), ("$now", Now()));

    private void DatPhong(string trangThai) => Insert(
        "UPDATE phong_tro SET trang_thai=$t WHERE id=$id", ("$t", trangThai), ("$id", _phongId));

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

    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => "/";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/";
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
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

    private async Task<LichHenChiTietViewModel> ChiTietAsync(int accountId, int id)
    {
        var controller = ControllerCho(accountId);
        var view = await controller.ChiTiet(id, default);
        return Assert.IsType<LichHenChiTietViewModel>(Assert.IsType<ViewResult>(view).Model);
    }

    [Fact]
    public async Task NutLapHopDong_HienKhiYeuCauDaDuyetVaPhongDaDatCoc()
    {
        var id = TaoYeuCau(LichHenTrangThai.DaDuyet);
        DatPhong(LichHenTrangThai.PhongDaDatCoc);

        var view = await ChiTietAsync(_chuNhaId, id);

        Assert.True(view.HienThiNutLapHopDong);
    }

    [Fact]
    public async Task NutLapHopDong_HienSauKhiDuyetThueNgayThatBai()
    {
        // Điều kiện nút phải đúng sau khi bấm Duyệt, không cần mở form riêng.
        var id = TaoYeuCau();

        await _service.DuyetThueNgayAsync(id, _chuNhaId, default);
        var view = await ChiTietAsync(_chuNhaId, id);

        Assert.True(view.HienThiNutLapHopDong);
    }

    [Theory]
    [InlineData(LichHenTrangThai.Moi, LichHenTrangThai.PhongTrong)]
    [InlineData(LichHenTrangThai.DaHenLich, LichHenTrangThai.PhongTrong)]
    [InlineData(LichHenTrangThai.TuChoi, LichHenTrangThai.PhongDaDatCoc)]
    [InlineData(LichHenTrangThai.DaHuy, LichHenTrangThai.PhongDaDatCoc)]
    public async Task NutLapHopDong_AnO_moi_truong_thai_khac(string trangThai, string trangThaiPhong)
    {
        var id = TaoYeuCau(trangThai);
        DatPhong(trangThaiPhong);

        var view = await ChiTietAsync(_chuNhaId, id);

        Assert.False(view.HienThiNutLapHopDong);
    }

    [Fact]
    public async Task NutLapHopDong_KhachKhongThay()
    {
        var id = TaoYeuCau(LichHenTrangThai.DaDuyet);
        DatPhong(LichHenTrangThai.PhongDaDatCoc);

        var view = await ChiTietAsync(_khachAccountId, id);

        Assert.False(view.LaChuNha);
        Assert.False(view.HienThiNutLapHopDong);
    }

    [Fact]
    public async Task TrangTamHienMaYeuCauChoChuNha()
    {
        var id = TaoYeuCau(LichHenTrangThai.DaDuyet);
        DatPhong(LichHenTrangThai.PhongDaDatCoc);

        var controller = ControllerCho(_chuNhaId);
        var view = await controller.LapHopDong(id, default);

        var redirect = Assert.IsType<RedirectToActionResult>(view);
        Assert.Equal("Create", redirect.ActionName);
        Assert.Equal("HopDong", redirect.ControllerName);
        Assert.Equal(id, redirect.RouteValues!["yeuCauId"]);
    }

    [Theory]
    [InlineData(LichHenTrangThai.Moi, LichHenTrangThai.PhongDaDatCoc)]
    [InlineData(LichHenTrangThai.DaHenLich, LichHenTrangThai.PhongDaDatCoc)]
    public async Task TrangTam_QuayLaiYeuCauKhiChuaDuyetHoacPhongChuaDatCoc(string trangThai, string trangThaiPhong)
    {
        // Server là nơi quyết định, không tin điều kiện do nút gửi lên.
        var id = TaoYeuCau(trangThai);
        DatPhong(trangThaiPhong);

        var controller = ControllerCho(_chuNhaId);
        var result = await controller.LapHopDong(id, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ChiTiet", redirect.ActionName);
        Assert.Equal("Chỉ yêu cầu đã duyệt mới lập được hợp đồng.",
            (string?)controller.TempData["LichHenError"]);
    }

    [Fact]
    public async Task TrangTam_KhongPhaiChuNhaPhong_ThenForbid()
    {
        var id = TaoYeuCau(LichHenTrangThai.DaDuyet);
        DatPhong(LichHenTrangThai.PhongDaDatCoc);

        var controller = ControllerCho(_chuNhaKhacId);
        Assert.IsType<ForbidResult>(await controller.LapHopDong(id, default));
    }

    [Fact]
    public async Task TrangTam_KhachKhongMoDuoc()
    {
        var id = TaoYeuCau(LichHenTrangThai.DaDuyet);
        DatPhong(LichHenTrangThai.PhongDaDatCoc);

        var controller = ControllerCho(_khachAccountId);
        Assert.IsType<ForbidResult>(await controller.LapHopDong(id, default));
    }

    [Fact]
    public async Task TrangTam_YeuCauKhongTonTai_ThenNotFound()
    {
        var controller = ControllerCho(_chuNhaId);
        Assert.IsType<NotFoundResult>(await controller.LapHopDong(999999, default));
    }

    [Fact]
    public void DuongDanLapHopDong_GomVaoMotCho()
    {
        // S3-01 có route thật thì đổi đúng hằng này, không sửa rải rác nhiều chỗ.
        Assert.Equal("/LichHen/LapHopDong", LichHenChiTietViewModel.DuongDanLapHopDong);
    }
}
