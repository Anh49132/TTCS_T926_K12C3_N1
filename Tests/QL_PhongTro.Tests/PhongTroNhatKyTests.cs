using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace QL_PhongTro.Tests;

// S3-04 Lát 4: chủ nhà của tòa nhà (và ADMIN) đọc lại nhật ký hoạt động của
// một phòng, gồm các dòng đổi trạng thái phòng và hợp đồng của phòng đó.
public class PhongTroNhatKyTests : IDisposable
{
    private const string ChuNhaHoTen = "Chủ nhà nk";
    private const string ChuNhaKhacHoTen = "Chủ nhà khác nk";

    private readonly string _dbPath;
    private readonly AppDbContext _db;

    private int _chuNhaId;
    private int _chuNhaKhacId;
    private int _quanLyId;
    private int _adminId;
    private int _phongId;
    private int _phongKhacId;
    private int _hopDongId;

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
    }

    public PhongTroNhatKyTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        var folder = Environment.GetEnvironmentVariable("S2_08_TEST_TEMP") ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, $"phong_nhat_ky_{Guid.NewGuid():N}.sqlite");

        LocalDatabaseInitializer.Create(_dbPath, seedPath);
        RentalRequestSchema.Initialize(_dbPath);
        DatabaseUpdates.Update(_dbPath, seedPath);

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + _dbPath).Options);
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

    private string Now() => "2026-03-10 00:00:00";

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
        _chuNhaId = TaiKhoanMoi(ChuNhaHoTen, "CHU_NHA", "nk_chu_nha@test.local");
        _chuNhaKhacId = TaiKhoanMoi(ChuNhaKhacHoTen, "CHU_NHA", "nk_chu_nha_khac@test.local");
        _quanLyId = TaiKhoanMoi("Quản lý nk", "QUAN_LY", "nk_quan_ly@test.local");
        _adminId = TaiKhoanMoi("Admin nk", "ADMIN", "nk_admin@test.local");
        var khachId = TaiKhoanMoi("Khách nk", "KHACH_THUE", "nk_khach@test.local");

        var toaNhaId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà nk','1 Đường nk',1,1)", ("$chu", _chuNhaId));
        var toaNhaKhacId = (int)Insert(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
                + "VALUES ($chu,'Tòa nhà khác nk','2 Đường nk',1,1)", ("$chu", _chuNhaKhacId));

        _phongId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'NK-A101',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaId), ("$now", Now()));
        _phongKhacId = (int)Insert(
            "INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,"
                + "trang_thai,ngay_tao,phien_ban) VALUES ($toa,'NK-B201',1,20,3000000,1000000,3,'TRONG',$now,0)",
            ("$toa", toaNhaKhacId), ("$now", Now()));

        var khachThueId = (int)Insert(
            "INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES ($tk,$ten,$now)",
            ("$tk", khachId), ("$ten", "Khách nk"), ("$now", Now()));
        _hopDongId = (int)Insert(
            "INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,tien_coc_thoa_thuan,"
                + "ngay_chot_hang_thang,trang_thai,nguoi_lap_id,ngay_tao) "
                + "VALUES ($ma,$phong,$khach,1000000,1,'DANG_HIEU_LUC',$lap,$now)",
            ("$ma", "HD-nk-0001"), ("$phong", _phongId), ("$khach", khachThueId),
            ("$lap", _chuNhaId), ("$now", Now()));

        Insert(
            "INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,"
                + "loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) "
                + "VALUES ($nguoi,$ten,'CHU_NHA','phong_tro',$phong,'DOI_TRANG_THAI',"
                + "'{\"trang_thai\":\"TRONG\"}','{\"trang_thai\":\"DA_DAT_COC\"}',$t)",
            ("$nguoi", _chuNhaId), ("$ten", ChuNhaHoTen), ("$phong", _phongId),
            ("$t", "2026-03-10 02:00:00"));
        Insert(
            "INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,"
                + "loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) "
                + "VALUES ($nguoi,$ten,'CHU_NHA','hop_dong',$hd,'DOI_TRANG_THAI',"
                + "'{\"trang_thai\":\"NHAP\"}','{\"trang_thai\":\"DANG_HIEU_LUC\"}',$t)",
            ("$nguoi", _chuNhaId), ("$ten", ChuNhaHoTen), ("$hd", _hopDongId),
            ("$t", "2026-03-10 03:00:00"));
        Insert(
            "INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,"
                + "loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) "
                + "VALUES ($nguoi,$ten,'CHU_NHA','phong_tro',$phong,'DOI_TRANG_THAI',"
                + "'{\"trang_thai\":\"TRONG\"}','{\"trang_thai\":\"DANG_THUE\"}',$t)",
            ("$nguoi", _chuNhaKhacId), ("$ten", ChuNhaKhacHoTen), ("$phong", _phongKhacId),
            ("$t", "2026-03-10 04:00:00"));
    }

    private PhongTroController Controller(int actorId, string vaiTro)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
             new Claim(ClaimTypes.Role, vaiTro)], "test"));
        var http = new DefaultHttpContext { User = user };
        var imageStore = new RoomImageStore(Path.GetTempPath());
        return new PhongTroController(_db,
                new DichVuPhongService(_db, new DichVuService(_db)),
                imageStore,
                new RoomImageDeletionService(_db, imageStore, NullLogger<RoomImageDeletionService>.Instance))
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    [Fact]
    public async Task NhatKyPhong_ChuNhaXemDuocNhatKyPhongVaHopDong()
    {
        var controller = Controller(_chuNhaId, "CHU_NHA");

        var result = await controller.NhatKyPhong(_phongId, null, null, 1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<NhatKyPhongViewModel>(view.Model);
        Assert.Equal("NK-A101", model.MaPhong);
        Assert.Equal("Tòa nhà nk", model.TenToaNha);
        Assert.Equal("Trống", model.TrangThaiPhong);
        Assert.Equal(2, model.Total);
        Assert.Equal(2, model.Rows.Count);
        Assert.Equal("hop_dong", model.Rows[0].LoaiDoiTuong);
        Assert.Equal(_hopDongId, model.Rows[0].DoiTuongId);
        Assert.Equal("phong_tro", model.Rows[1].LoaiDoiTuong);
        Assert.Equal(_phongId, model.Rows[1].DoiTuongId);
    }

    [Fact]
    public async Task NhatKyPhong_AdminXemDuocMoiToaNNha()
    {
        var controller = Controller(_adminId, "ADMIN");

        var result = await controller.NhatKyPhong(_phongId, null, null, 1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<NhatKyPhongViewModel>(view.Model);
        Assert.Equal(2, model.Total);
    }

    [Fact]
    public async Task NhatKyPhong_QuanLyBiTuChoi()
    {
        var controller = Controller(_quanLyId, "QUAN_LY");

        var result = await controller.NhatKyPhong(_phongId, null, null, 1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task NhatKyPhong_ChuNhaKhacBiTuChoi()
    {
        var controller = Controller(_chuNhaKhacId, "CHU_NHA");

        var result = await controller.NhatKyPhong(_phongId, null, null, 1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task NhatKyPhong_PhongKhongTonTai_ThenNotFound()
    {
        var controller = Controller(_chuNhaId, "CHU_NHA");

        var result = await controller.NhatKyPhong(999999, null, null, 1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task NhatKyPhong_ChuNhaKhacXemPhongCuaMinh_ChiThayNhatKyRieng()
    {
        var controller = Controller(_chuNhaKhacId, "CHU_NHA");

        var result = await controller.NhatKyPhong(_phongKhacId, null, null, 1);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<NhatKyPhongViewModel>(view.Model);
        Assert.Equal(1, model.Total);
        Assert.Equal(_phongKhacId, model.Rows[0].DoiTuongId);
        Assert.Equal(ChuNhaKhacHoTen, model.Rows[0].TenNguoiThucHien);
    }

    [Fact]
    public async Task NhatKyPhong_LocNgay_ChiLayDungKhoang()
    {
        var controller = Controller(_chuNhaId, "CHU_NHA");

        var view = Assert.IsType<ViewResult>(
            await controller.NhatKyPhong(_phongId, new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 10), 1));
        var model = Assert.IsType<NhatKyPhongViewModel>(view.Model);
        Assert.Equal(2, model.Total);

        view = Assert.IsType<ViewResult>(
            await controller.NhatKyPhong(_phongId, new DateOnly(2026, 3, 11), new DateOnly(2026, 3, 11), 1));
        model = Assert.IsType<NhatKyPhongViewModel>(view.Model);
        Assert.Equal(0, model.Total);
        Assert.Empty(model.Rows);
    }

    [Fact]
    public async Task NhatKyPhong_NgayTuSauNgayDen_ThenBadRequest()
    {
        var controller = Controller(_chuNhaId, "CHU_NHA");

        var result = await controller.NhatKyPhong(_phongId, new DateOnly(2026, 3, 11), new DateOnly(2026, 3, 10), 1);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task NhatKyPhong_PhanTrang20DongMoiTrang()
    {
        for (var i = 0; i < 21; i++)
            Insert(
                "INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,"
                    + "loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) "
                    + "VALUES ($nguoi,$ten,'CHU_NHA','phong_tro',$phong,'SUA',NULL,NULL,$t)",
                ("$nguoi", _chuNhaId), ("$ten", ChuNhaHoTen), ("$phong", _phongId),
                ("$t", $"2026-03-11 00:{i:D2}:00"));

        var controller = Controller(_chuNhaId, "CHU_NHA");

        var trang1 = Assert.IsType<NhatKyPhongViewModel>(
            (Assert.IsType<ViewResult>(await controller.NhatKyPhong(_phongId, null, null, 1))).Model);
        Assert.Equal(23, trang1.Total);
        Assert.Equal(2, trang1.Pages);
        Assert.Equal(20, trang1.Rows.Count);

        var trang2 = Assert.IsType<NhatKyPhongViewModel>(
            (Assert.IsType<ViewResult>(await controller.NhatKyPhong(_phongId, null, null, 2))).Model);
        Assert.Equal(3, trang2.Rows.Count);
    }

    [Fact]
    public void NhatKy_BatBien_CapNhatVaXoaDeuBiChoi()
    {
        var id = Convert.ToInt64(Scalar("SELECT id FROM nhat_ky_hoat_dong ORDER BY id LIMIT 1"));

        // Sửa và xoá thẳng bằng SQL đều bị trigger audit chặn.
        Assert.Throws<SqliteException>(() => Execute(
            "UPDATE nhat_ky_hoat_dong SET hanh_dong='SUA' WHERE id=$id", ("$id", id)));
        Assert.Throws<SqliteException>(() => Execute(
            "DELETE FROM nhat_ky_hoat_dong WHERE id=$id", ("$id", id)));

        // Cấp EF cũng bị chặn trước khi chạm CSDL.
        var dong = _db.NhatKyHoatDongs.Single(e => e.Id == id);
        dong.HanhDong = "SUA";
        Assert.Throws<InvalidOperationException>(() => _db.SaveChanges());

        // Nội dung dòng nhật ký vẫn nguyên sau các lần bị chặn.
        Assert.Equal("DOI_TRANG_THAI",
            Scalar("SELECT hanh_dong FROM nhat_ky_hoat_dong WHERE id=$id", ("$id", id)));
    }

    private void Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        var connection = _db.Database.GetDbConnection();
        using var command = ((SqliteConnection)connection).CreateCommand();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
