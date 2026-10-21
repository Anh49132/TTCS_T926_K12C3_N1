using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

// S3-04 nghiệm thu: nhật ký phòng khi kích hoạt hợp đồng, chuỗi nhật ký qua
// duyệt thuê ngay rồi kích hoạt, hoàn tác khi kích hoạt thất bại, ẩn tin công
// khai và đồng bộ tin đăng ở bước kích hoạt. Mỗi test một bản CSDL riêng.
public sealed class S304NghiemThuTests : IDisposable
{
    static S304NghiemThuTests()
    {
        // Giả lập lỗi kích hoạt chỉ hoạt động trong Development.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    private const string ChuNhaHoTen = "Chủ nhà nghiệm thu";

    private readonly string _dbPath;
    private readonly MockTimeProvider _clock = new() { UtcNow = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc) };

    private const int ChuNhaId = 1;
    private const int ChuNhaKhacId = 2;
    private const int KhachTaiKhoanId = 3;
    private const int KhachThueId = 1;
    private const int PhongId = 1;
    private const int YeuCauId = 1;
    private const int TinDangId = 7; // tin của phòng kiểm thử, seed sau cùng để lên đầu trang công khai

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
    }

    public S304NghiemThuTests()
    {
        var appPath = AppPath();
        var seedPath = Path.Combine(appPath, "Data", "permissions.seed.json");
        _dbPath = Path.Combine(Path.GetTempPath(), $"s304_nghiem_thu_{Guid.NewGuid():N}.sqlite");

        LocalDatabaseInitializer.Create(_dbPath, seedPath);
        RentalRequestSchema.Initialize(_dbPath);
        TaoDuLieu();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private void TaoDuLieu()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath};Foreign Keys=True");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
            VALUES('Chủ nhà nghiệm thu','s304_chu_nha@test.local','0900000001','x','CHU_NHA',1,1,'2026-10-01','2026-10-01'),
                  ('Chủ nhà khác nghiệm thu','s304_chu_nha_khac@test.local','0900000002','x','CHU_NHA',1,1,'2026-10-01','2026-10-01'),
                  ('Khách nghiệm thu','s304_khach@test.local','0900000003','x','KHACH_THUE',1,1,'2026-10-01','2026-10-01');
            INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES(3,'Khách nghiệm thu','2026-10-01');
            INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) VALUES(1,'Tòa nhà nghiệm thu','1 Đường nghiệm thu',5,1);
            INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao,phien_ban)
            VALUES(1,'A203',2,25,3000000,3000000,2,'TRONG','2026-10-01',0),
                  (1,'A204',2,25,3000000,3000000,2,'TRONG','2026-10-01',0),
                  (1,'A205',2,25,3000000,3000000,2,'TRONG','2026-10-01',0),
                  (1,'B101',1,25,3000000,3000000,2,'TRONG','2026-10-01',0),
                  (1,'B102',1,25,3000000,3000000,2,'TRONG','2026-10-01',0),
                  (1,'B103',1,25,3000000,3000000,2,'TRONG','2026-10-01',0),
                  (1,'B104',1,25,3000000,3000000,2,'TRONG','2026-10-01',0);
            -- Sáu tin lót để trang công khai có 7 kết quả trước khi duyệt (trang 1: 6 tin, trang 2: 1 tin).
            INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_dang,ngay_het_han,ngay_tao)
            VALUES(2,1,'Tin A204','','DANG_HIEN_THI','2026-10-01 00:00:00','2027-01-01 00:00:00','2026-10-01 00:00:00'),
                  (3,1,'Tin A205','','DANG_HIEN_THI','2026-10-01 00:00:00','2027-01-01 00:00:00','2026-10-01 00:00:00'),
                  (4,1,'Tin B101','','DANG_HIEN_THI','2026-10-01 00:00:00','2027-01-01 00:00:00','2026-10-01 00:00:00'),
                  (5,1,'Tin B102','','DANG_HIEN_THI','2026-10-01 00:00:00','2027-01-01 00:00:00','2026-10-01 00:00:00'),
                  (6,1,'Tin B103','','DANG_HIEN_THI','2026-10-01 00:00:00','2027-01-01 00:00:00','2026-10-01 00:00:00'),
                  (7,1,'Tin B104','','DANG_HIEN_THI','2026-10-01 00:00:00','2027-01-01 00:00:00','2026-10-01 00:00:00'),
                  (1,1,'Tin A203','','DANG_HIEN_THI','2026-10-01 00:00:00','2027-01-01 00:00:00','2026-10-01 00:00:00');
            INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,trang_thai,ngay_tao,phien_ban)
            VALUES('YC-s304-0001',7,1,'THUE_NGAY','2026-11-05',1,'MOI','2026-10-01 00:00:00',0);
            """;
        command.ExecuteNonQuery();
    }

    private static ClaimsPrincipal Principal(int id) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, "CHU_NHA")], "test"));

    private AppDbContext Context(int actorId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath};Foreign Keys=True;Pooling=False").Options;
        var http = new DefaultHttpContext { User = Principal(actorId) };
        return new AppDbContext(options, new HttpContextAccessor { HttpContext = http });
    }

    private HopDongController HopDongController(AppDbContext db, int actorId = ChuNhaId)
    {
        var http = new DefaultHttpContext { User = Principal(actorId) };
        return new HopDongController(db, _clock)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new MemoryTempData())
        };
    }

    private async Task DuyetThueNgayAsync()
    {
        using var db = Context(ChuNhaId);
        await new LichHenService(db, _clock).DuyetThueNgayAsync(YeuCauId, ChuNhaId, default);
    }

    // Lập hợp đồng nháp từ yêu cầu đã duyệt (sau bước Duyệt thuê ngay).
    private async Task<int> TaoHopDongNhapTuYeuCauDaDuyetAsync()
    {
        using var db = Context(ChuNhaId);
        var controller = HopDongController(db);
        var vm = new HopDongCreateViewModel
        {
            Intent = "NHAP",
            YeuCauId = YeuCauId,
            GiaThue = (await db.PhongTros.SingleAsync(x => x.Id == PhongId)).GiaThue,
            TienCoc = 0,
            NgayBatDau = new DateOnly(2026, 11, 5),
            SoThang = 12,
            NgayChot = 5,
            ChiSoDien = 1240,
            ChiSoNuoc = 356,
            // Phòng đã tăng một phiên bản khi duyệt thuê ngay.
            PhienBanPhong = 1,
            SoThangCoc = 0
        };
        Assert.IsType<RedirectToActionResult>(await controller.Create(vm, default));
        return int.Parse(Scalar("SELECT id FROM hop_dong WHERE yeu_cau_thue_id=$req", ("$req", YeuCauId)));
    }

    private async Task<int> TaoHopDongNhapAsync()
    {
        await DuyetThueNgayAsync();
        return await TaoHopDongNhapTuYeuCauDaDuyetAsync();
    }

    private string Scalar(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath};Foreign Keys=True");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? "" : Convert.ToString(result)!;
    }

    private List<Dictionary<string, string>> Query(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath};Foreign Keys=True");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        using var reader = command.ExecuteReader();
        var rows = new List<Dictionary<string, string>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, string>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? "" : reader.GetString(i);
            rows.Add(row);
        }
        return rows;
    }

    private int JournalCount() => int.Parse(Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong"));

    private List<Dictionary<string, string>> JournalRows(string where, params (string Name, object? Value)[] parameters) =>
        Query("SELECT id, nguoi_thuc_hien_id, ten_nguoi_thuc_hien, vai_tro_luc_thuc_hien, loai_doi_tuong, doi_tuong_id, hanh_dong, du_lieu_truoc, du_lieu_sau FROM nhat_ky_hoat_dong WHERE " + where + " ORDER BY id", parameters);

    private long Insert(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath};Foreign Keys=True");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
        using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt64(idCommand.ExecuteScalar());
    }

    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    [Fact]
    public async Task KichHoat_ThanhCong_GhiDungNhatKyHopDongVaPhong_KemSnapshotTruocSau()
    {
        var hopDongId = await TaoHopDongNhapAsync();
        // Trạng thái trước kích hoạt: hợp đồng nháp, phòng đã đặt cọc, tin đã cho thuê.
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM hop_dong WHERE id=$id", ("$id", hopDongId)));
        Assert.Equal("DA_DAT_COC", Scalar("SELECT trang_thai FROM phong_tro WHERE id=$id", ("$id", PhongId)));
        Assert.Equal("DA_CHO_THUE", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", TinDangId)));

        var truoc = JournalCount();

        using (var db = Context(ChuNhaId))
            Assert.IsType<RedirectToActionResult>(await HopDongController(db).KichHoat(hopDongId, null, default));

        // Bước kích hoạt ghi đúng hai dòng nhật ký: hợp đồng và phòng.
        Assert.Equal(truoc + 2, JournalCount());
        var rows = JournalRows(
            "hanh_dong='DOI_TRANG_THAI' AND ((loai_doi_tuong='phong_tro' AND doi_tuong_id=$phong) OR (loai_doi_tuong='hop_dong' AND doi_tuong_id=$hd))",
            ("$phong", PhongId), ("$hd", hopDongId));
        // Dòng đầu là Đã đặt cọc khi duyệt thuê ngay; hai dòng sau là bước kích hoạt.
        Assert.Equal(3, rows.Count);

        var hopDongRow = rows[^2];
        Assert.Equal("hop_dong", hopDongRow["loai_doi_tuong"]);
        Assert.Equal(hopDongId.ToString(), hopDongRow["doi_tuong_id"]);
        Assert.Equal("{\"trang_thai\":\"NHAP\"}", hopDongRow["du_lieu_truoc"]);
        Assert.Equal("{\"trang_thai\":\"DANG_HIEU_LUC\"}", hopDongRow["du_lieu_sau"]);

        var phongRow = rows[^1];
        Assert.Equal("phong_tro", phongRow["loai_doi_tuong"]);
        Assert.Equal(PhongId.ToString(), phongRow["doi_tuong_id"]);
        Assert.Equal("{\"trang_thai\":\"DA_DAT_COC\"}", phongRow["du_lieu_truoc"]);
        Assert.Equal("{\"trang_thai\":\"DANG_THUE\"}", phongRow["du_lieu_sau"]);
        Assert.Equal(ChuNhaId.ToString(), phongRow["nguoi_thuc_hien_id"]);
        Assert.Equal(ChuNhaHoTen, phongRow["ten_nguoi_thuc_hien"]);
        Assert.Equal("CHU_NHA", phongRow["vai_tro_luc_thuc_hien"]);

        Assert.Equal("DANG_HIEU_LUC", Scalar("SELECT trang_thai FROM hop_dong WHERE id=$id", ("$id", hopDongId)));
        Assert.Equal("DANG_THUE", Scalar("SELECT trang_thai FROM phong_tro WHERE id=$id", ("$id", PhongId)));
    }

    [Fact]
    public async Task DuyetThueNgay_RoiKichHoat_GhiChuoiHaiDongNhatKyPhongNoiNhau()
    {
        var hopDongId = await TaoHopDongNhapAsync();
        using (var db = Context(ChuNhaId))
            Assert.IsType<RedirectToActionResult>(await HopDongController(db).KichHoat(hopDongId, null, default));

        // Hai dòng nhật ký phòng nối nhau: Trống → Đã đặt cọc (duyệt thuê ngay),
        // rồi Đã đặt cọc → Đang thuê (kích hoạt hợp đồng).
        var phongRows = JournalRows("loai_doi_tuong='phong_tro' AND doi_tuong_id=$id AND hanh_dong='DOI_TRANG_THAI'", ("$id", PhongId));
        Assert.Equal(2, phongRows.Count);
        Assert.Equal("{\"trang_thai\":\"TRONG\"}", phongRows[0]["du_lieu_truoc"]);
        Assert.Equal("{\"trang_thai\":\"DA_DAT_COC\"}", phongRows[0]["du_lieu_sau"]);
        Assert.Equal("{\"trang_thai\":\"DA_DAT_COC\"}", phongRows[1]["du_lieu_truoc"]);
        Assert.Equal("{\"trang_thai\":\"DANG_THUE\"}", phongRows[1]["du_lieu_sau"]);
        Assert.All(phongRows, row =>
        {
            Assert.Equal(ChuNhaId.ToString(), row["nguoi_thuc_hien_id"]);
            Assert.Equal(ChuNhaHoTen, row["ten_nguoi_thuc_hien"]);
            Assert.Equal("CHU_NHA", row["vai_tro_luc_thuc_hien"]);
        });

        // Hợp đồng cũng có dòng nháp → Đang hiệu lực ở bước kích hoạt.
        var hopDongRows = JournalRows("loai_doi_tuong='hop_dong' AND doi_tuong_id=$id AND hanh_dong='DOI_TRANG_THAI'", ("$id", hopDongId));
        Assert.Single(hopDongRows);
        Assert.Equal("{\"trang_thai\":\"NHAP\"}", hopDongRows[0]["du_lieu_truoc"]);
        Assert.Equal("{\"trang_thai\":\"DANG_HIEU_LUC\"}", hopDongRows[0]["du_lieu_sau"]);
    }

    [Fact]
    public async Task KichHoat_ThatBai_BuocHopDong_KhongGhiNhatKyVaGiuNguyenTrangThai()
    {
        var hopDongId = await TaoHopDongNhapAsync();
        var truoc = JournalCount();

        using (var db = Context(ChuNhaId))
        {
            var controller = HopDongController(db);
            Assert.IsType<ViewResult>(await controller.KichHoat(hopDongId, "contract", default));
            Assert.False(controller.ModelState.IsValid);
        }

        // Thất bại ở bước hợp đồng: không thêm dòng nhật ký nào, mọi trạng thái giữ nguyên.
        Assert.Equal(truoc, JournalCount());
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM hop_dong WHERE id=$id", ("$id", hopDongId)));
        Assert.Equal("DA_DAT_COC", Scalar("SELECT trang_thai FROM phong_tro WHERE id=$id", ("$id", PhongId)));
        Assert.Equal("DA_CHO_THUE", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", TinDangId)));
    }

    [Fact]
    public async Task KichHoat_ThatBai_BuocPhong_KhongGhiNhatKyVaGiuNguyenTrangThai()
    {
        var hopDongId = await TaoHopDongNhapAsync();
        var truoc = JournalCount();

        using (var db = Context(ChuNhaId))
        {
            var controller = HopDongController(db);
            Assert.IsType<ViewResult>(await controller.KichHoat(hopDongId, "room", default));
            Assert.False(controller.ModelState.IsValid);
        }

        // Thất bại ở bước phòng: hợp đồng vẫn nháp, phòng vẫn Đã đặt cọc,
        // phiên bản phòng không tăng và không có dòng nhật ký nào.
        Assert.Equal(truoc, JournalCount());
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM hop_dong WHERE id=$id", ("$id", hopDongId)));
        Assert.Equal("DA_DAT_COC", Scalar("SELECT trang_thai FROM phong_tro WHERE id=$id", ("$id", PhongId)));
        Assert.Equal("1", Scalar("SELECT phien_ban FROM phong_tro WHERE id=$id", ("$id", PhongId)));
        Assert.Equal("DA_CHO_THUE", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", TinDangId)));
    }

    [Fact]
    public async Task KichHoat_ThatBai_BuocTin_KhongGhiNhatKyVaGiuNguyenTrangThai()
    {
        var hopDongId = await TaoHopDongNhapAsync();
        var truoc = JournalCount();

        using (var db = Context(ChuNhaId))
        {
            var controller = HopDongController(db);
            Assert.IsType<ViewResult>(await controller.KichHoat(hopDongId, "listing", default));
            Assert.False(controller.ModelState.IsValid);
        }

        // Thất bại ở bước tin: hợp đồng và phòng hoàn tác về trạng thái trước kích hoạt.
        Assert.Equal(truoc, JournalCount());
        Assert.Equal("NHAP", Scalar("SELECT trang_thai FROM hop_dong WHERE id=$id", ("$id", hopDongId)));
        Assert.Equal("DA_DAT_COC", Scalar("SELECT trang_thai FROM phong_tro WHERE id=$id", ("$id", PhongId)));
        Assert.Equal("DA_CHO_THUE", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", TinDangId)));
    }

    [Fact]
    public async Task TinDaChoThue_VangKhoiTimTinVaTrangChu_TuBuocDuyetVaConVangSauKichHoat()
    {
        // Trước duyệt: 7 tin công khai, trang 1 có 6 tin, trang 2 có 1 tin.
        using (var db = Context(ChuNhaId))
        {
            var controller = new TimTinController(db, new TinDangExpirationService(db, _clock));
            var view = Assert.IsType<ViewResult>(await controller.Index(new TimTinViewModel(), default));
            var model = Assert.IsType<TimTinViewModel>(view.Model);
            Assert.Equal(7, model.TongKetQua);
            Assert.Equal(2, model.TongTrang);
            Assert.Equal(6, model.TinDangs.Count);
            Assert.Contains(model.TinDangs, tin => tin.Id == TinDangId);
        }
        using (var db = Context(ChuNhaId))
        {
            var home = new HomeController(new RegistrationSettings(), db,
                new YeuCauThueService(db, _clock), new TinDangExpirationService(db, _clock))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            var view = Assert.IsType<ViewResult>(await home.Index());
            var model = Assert.IsType<HomeViewModel>(view.Model);
            Assert.Equal(6, model.TinMoiNhat.Count);
            Assert.Contains(model.TinMoiNhat, tin => tin.Id == TinDangId);
        }

        // Duyệt thuê ngay: phòng Đã đặt cọc, tin Đã cho thuê.
        await DuyetThueNgayAsync();

        // Sau duyệt: tin vắng mặt khỏi trang công khai và trang chủ,
        // tổng kết quả giảm còn 6 và chỉ còn một trang.
        using (var db = Context(ChuNhaId))
        {
            var controller = new TimTinController(db, new TinDangExpirationService(db, _clock));
            var view = Assert.IsType<ViewResult>(await controller.Index(new TimTinViewModel(), default));
            var model = Assert.IsType<TimTinViewModel>(view.Model);
            Assert.Equal(6, model.TongKetQua);
            Assert.Equal(1, model.TongTrang);
            Assert.Equal(6, model.TinDangs.Count);
            Assert.DoesNotContain(model.TinDangs, tin => tin.Id == TinDangId);
        }
        using (var db = Context(ChuNhaId))
        {
            var home = new HomeController(new RegistrationSettings(), db,
                new YeuCauThueService(db, _clock), new TinDangExpirationService(db, _clock))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            var view = Assert.IsType<ViewResult>(await home.Index());
            var model = Assert.IsType<HomeViewModel>(view.Model);
            Assert.Equal(6, model.TinMoiNhat.Count);
            Assert.DoesNotContain(model.TinMoiNhat, tin => tin.Id == TinDangId);
        }

        // Kích hoạt hợp đồng: tin vẫn vắng mặt ở cả hai nơi, số kết quả và
        // phân trang không đổi.
        var hopDongId = await TaoHopDongNhapTuYeuCauDaDuyetAsync();
        using (var db = Context(ChuNhaId))
            Assert.IsType<RedirectToActionResult>(await HopDongController(db).KichHoat(hopDongId, null, default));

        using (var db = Context(ChuNhaId))
        {
            var controller = new TimTinController(db, new TinDangExpirationService(db, _clock));
            var view = Assert.IsType<ViewResult>(await controller.Index(new TimTinViewModel(), default));
            var model = Assert.IsType<TimTinViewModel>(view.Model);
            Assert.Equal(6, model.TongKetQua);
            Assert.Equal(1, model.TongTrang);
            Assert.Equal(6, model.TinDangs.Count);
            Assert.DoesNotContain(model.TinDangs, tin => tin.Id == TinDangId);
        }
        using (var db = Context(ChuNhaId))
        {
            var home = new HomeController(new RegistrationSettings(), db,
                new YeuCauThueService(db, _clock), new TinDangExpirationService(db, _clock))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            var view = Assert.IsType<ViewResult>(await home.Index());
            var model = Assert.IsType<HomeViewModel>(view.Model);
            Assert.Equal(6, model.TinMoiNhat.Count);
            Assert.DoesNotContain(model.TinMoiNhat, tin => tin.Id == TinDangId);
        }
    }

    [Fact]
    public async Task KichHoat_DoiMoiTinChuaChoThueDuocTaoSauBuocDuyet()
    {
        var hopDongId = await TaoHopDongNhapAsync();
        // Giao diện không cho đăng tin mới vào phòng Đã đặt cọc, nên tin ở
        // trạng thái khác Đã cho thuê (giả lập tin được tạo sau khi duyệt)
        // chỉ chèn được thẳng vào CSDL.
        var tinMoiId = (int)Insert(
            "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao) "
            + "VALUES ($phong,$chu,'Tin tạo sau duyệt','','TAM_AN','2026-10-07 01:00:00')",
            ("$phong", PhongId), ("$chu", ChuNhaId));
        Assert.Equal("TAM_AN", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", tinMoiId)));

        using (var db = Context(ChuNhaId))
            Assert.IsType<RedirectToActionResult>(await HopDongController(db).KichHoat(hopDongId, null, default));

        // Bước 3 của kích hoạt vẫn đồng bộ mọi tin chưa Đã cho thuê của phòng.
        Assert.Equal("DA_CHO_THUE", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", tinMoiId)));
        Assert.Equal("DA_CHO_THUE", Scalar("SELECT trang_thai FROM tin_dang WHERE id=$id", ("$id", TinDangId)));
    }
}
