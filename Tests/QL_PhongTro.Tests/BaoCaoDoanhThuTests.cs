using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class BaoCaoDoanhThuTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "baocao-" + Guid.NewGuid().ToString("N"));
    private readonly string _dbPath;
    private readonly MockTimeProvider _clock = new() { UtcNow = new DateTime(2026, 10, 15, 7, 0, 0, DateTimeKind.Utc) }; // 14:00 VN

    public BaoCaoDoanhThuTests()
    {
        Directory.CreateDirectory(_folder);
        _dbPath = Path.Combine(_folder, "test.sqlite");

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "QL_PhongTro", "Data")))
            root = root.Parent;
        var seedPath = Path.Combine(root!.FullName, "QL_PhongTro", "Data", "permissions.seed.json");

        LocalDatabaseInitializer.Create(_dbPath, seedPath);
        RentalRequestSchema.Initialize(_dbPath);
        DatabaseUpdates.Update(_dbPath, seedPath);

        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath, ForeignKeys = true, Pooling = false }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            -- The repository's empty-database initializer omits the legacy invoice core tables.
            -- Add only the tables needed by this report fixture; production schema is untouched.
            CREATE TABLE hoa_don (
                id INTEGER PRIMARY KEY, ma_hoa_don TEXT NOT NULL, hop_dong_id INTEGER NOT NULL,
                thang INTEGER NOT NULL, nam INTEGER NOT NULL, tu_ngay TEXT NOT NULL DEFAULT '2026-01-01',
                den_ngay TEXT NOT NULL DEFAULT '2026-01-31', ngay_chot TEXT NOT NULL DEFAULT '2026-01-01',
                so_nguoi_tinh_phi INTEGER NOT NULL DEFAULT 1, loai_hoa_don TEXT NOT NULL DEFAULT 'DINH_KY',
                ngay_lap TEXT NOT NULL DEFAULT '2026-01-01', ngay_phat_hanh TEXT NULL,
                ngay_phat_hanh_nghiep_vu TEXT NULL, han_thanh_toan TEXT NOT NULL DEFAULT '2026-01-08',
                tong_tien INTEGER NOT NULL, trang_thai TEXT NOT NULL, nguoi_lap_id INTEGER NOT NULL DEFAULT 10,
                nguoi_phat_hanh_id INTEGER NULL, thay_the_hoa_don_id INTEGER NULL UNIQUE,
                nguoi_huy_id INTEGER NULL, ngay_huy TEXT NULL, ly_do_huy TEXT NULL,
                ghi_chu TEXT NULL, phien_ban INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE thanh_toan (
                id INTEGER PRIMARY KEY, hoa_don_id INTEGER NOT NULL, so_tien INTEGER NOT NULL,
                ngay_thanh_toan TEXT NOT NULL, trang_thai TEXT NOT NULL
            );

            INSERT INTO tai_khoan(id, ho_ten, email, so_dien_thoai, mat_khau, vai_tro, dang_hoat_dong, email_confirmed, ngay_tao, ngay_cap_nhat)
            VALUES(10, 'Chủ nhà 1', 'chunha1@test.vn', '0901000001', 'hash', 'CHU_NHA', 1, 1, '2026-01-01', '2026-01-01'),
                  (20, 'Chủ nhà 2', 'chunha2@test.vn', '0901000002', 'hash', 'CHU_NHA', 1, 1, '2026-01-01', '2026-01-01'),
                  (30, 'Quản lý 1', 'quanly1@test.vn', '0901000003', 'hash', 'QUAN_LY', 1, 1, '2026-01-01', '2026-01-01'),
                  (40, 'Khách thuê 1', 'khach1@test.vn', '0901000004', 'hash', 'KHACH_THUE', 1, 1, '2026-01-01', '2026-01-01'),
                  (50, 'Admin', 'admin@test.vn', '0901000005', 'hash', 'ADMIN', 1, 1, '2026-01-01', '2026-01-01');

            INSERT INTO khach_thue(id, tai_khoan_id, ho_ten, so_dien_thoai, so_giay_to, ngay_tao)
            VALUES(40, 40, 'Khách thuê 1', '0901000004', '001234567890', '2026-01-01');

            INSERT INTO toa_nha(id, chu_nha_id, quan_ly_id, ten_toa_nha, dia_chi, dang_hoat_dong)
            VALUES(1, 10, 30, 'Toà nhà Sunrise', '123 Cầu Giấy', 1),
                  (2, 20, NULL, 'Toà nhà Moonlit', '456 Ba Đình', 1);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); } catch { }
    }

    private AppDbContext CreateContext(int actorId = 10)
    {
        var connStr = new SqliteConnectionStringBuilder { DataSource = _dbPath, ForeignKeys = true, Pooling = false }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connStr)
            .Options;
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                    new Claim(ClaimTypes.Role, "CHU_NHA")
                ], "TestAuth"))
            }
        };
        return new AppDbContext(options, accessor);
    }

    [Fact]
    public async Task RevenueAndOccupancyCalculations_MatchDatabasePrecisely()
    {
        using var db = CreateContext();

        // Thêm 3 phòng cho Toà 1
        db.PhongTros.AddRange(
            new PhongTro { Id = 101, ToaNhaId = 1, MaPhong = "101", TrangThai = "DANG_THUE", GiaThue = 5_000_000, DienTich = 25, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1) },
            new PhongTro { Id = 102, ToaNhaId = 1, MaPhong = "102", TrangThai = "DANG_THUE", GiaThue = 3_000_000, DienTich = 25, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1) },
            new PhongTro { Id = 103, ToaNhaId = 1, MaPhong = "103", TrangThai = "TRONG", GiaThue = 4_000_000, DienTich = 25, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1) }
        );

        // Hợp đồng cho phòng 101 và 102
        db.HopDongs.AddRange(
            new HopDongThamChieu { Id = 1001, PhongId = 101, TrangThai = "DANG_HIEU_LUC", MaHopDong = "HD-1001", KhachDungTenId = 40 },
            new HopDongThamChieu { Id = 1002, PhongId = 102, TrangThai = "DANG_HIEU_LUC", MaHopDong = "HD-1002", KhachDungTenId = 40 }
        );

        db.KyHopDongs.AddRange(
            new KyHopDongThamChieu { Id = 1001, HopDongId = 1001, NgayBatDau = new DateOnly(2026, 5, 1), NgayKetThuc = new DateOnly(2026, 11, 30), GiaThue = 5_000_000 },
            new KyHopDongThamChieu { Id = 1002, HopDongId = 1002, NgayBatDau = new DateOnly(2026, 6, 1), NgayKetThuc = new DateOnly(2026, 12, 31), GiaThue = 3_000_000 }
        );

        // Hóa đơn tháng 10/2026
        // HĐ 1001: Phát hành 5.500.000, đã thu 4.000.000 -> còn 1.500.000
        // HĐ 1002: Phát hành 3.000.000, đã thu 3.000.000 -> còn 0
        db.HoaDons.AddRange(
            new HoaDon { Id = 2001, HopDongId = 1001, Thang = 10, Nam = 2026, TongTien = 5_500_000, TrangThai = "DA_PHAT_HANH", MaHoaDon = "HD-10-01" },
            new HoaDon { Id = 2002, HopDongId = 1002, Thang = 10, Nam = 2026, TongTien = 3_000_000, TrangThai = "DA_PHAT_HANH", MaHoaDon = "HD-10-02" }
        );

        db.ThanhToans.AddRange(
            new ThanhToanHoaDon { Id = 3001, HoaDonId = 2001, SoTien = 4_000_000, TrangThai = "DA_XAC_NHAN", NgayThanhToan = new DateOnly(2026, 10, 10) },
            new ThanhToanHoaDon { Id = 3002, HoaDonId = 2002, SoTien = 3_000_000, TrangThai = "DA_XAC_NHAN", NgayThanhToan = new DateOnly(2026, 10, 12) }
        );

        await db.SaveChangesAsync();

        var service = new BaoCaoService(db, _clock);
        var result = await service.LayBaoCaoAsync(
            actorId: 10,
            role: "CHU_NHA",
            toaNhaId: 1,
            tuThang: 10,
            tuNam: 2026,
            denThang: 10,
            denNam: 2026
        );

        Assert.Null(result.ErrorMessage);
        Assert.Single(result.ChiTietTheoThang);

        var row = result.ChiTietTheoThang[0];
        Assert.Equal(8_500_000, row.TongPhatHanh);
        Assert.Equal(7_000_000, row.TongDaThu);
        Assert.Equal(1_500_000, row.SoConPhaiThu);

        // Tỉ lệ thu: 7.000.000 / 8.500.000 = 82.35% -> 82.4%
        Assert.Equal(82.4m, row.TiLeThu);

        // Lấp đầy: 2 phòng đang thuê (101, 102) / 3 phòng cho thuê (101, 102, 103) = 66.7%
        Assert.Equal(2, row.SoPhongDangThue);
        Assert.Equal(3, row.TongPhongChoThue);
        Assert.Equal(66.7m, row.TiLeLapDay);

        var monthly = await service.LayBaoCaoThangAsync(10, "CHU_NHA", 1, 10, 2026);
        Assert.Equal(8_500_000, monthly.TongPhatHanh);
        Assert.Equal(7_000_000, monthly.TongDaThu);
        Assert.Equal(1_500_000, monthly.TongConPhaiThu);
        Assert.Equal(82.4m, monthly.TiLeThuTongThe);

        // Tổng hợp KPI
        Assert.Equal(8_500_000, result.TongPhatHanh);
        Assert.Equal(7_000_000, result.TongDaThu);
        Assert.Equal(1_500_000, result.TongConPhaiThu);
        Assert.Equal(82.4m, result.TiLeThuTongThe);
        Assert.Equal(66.7m, result.TiLeLapDayHienTai);
    }

    [Fact]
    public async Task DraftAndCancelledInvoices_AndUnconfirmedPayments_AreExcluded()
    {
        using var db = CreateContext();

        db.PhongTros.Add(new PhongTro { Id = 101, ToaNhaId = 1, MaPhong = "101", TrangThai = "DANG_THUE", GiaThue = 5_000_000, DienTich = 25, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1) });
        db.HopDongs.Add(new HopDongThamChieu { Id = 1001, PhongId = 101, TrangThai = "DANG_HIEU_LUC", MaHopDong = "HD-1001", KhachDungTenId = 40 });
        db.KyHopDongs.Add(new KyHopDongThamChieu { Id = 1001, HopDongId = 1001, NgayBatDau = new DateOnly(2026, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31) });

        // 1 hóa đơn đã phát hành (5.000.000), 1 hóa đơn nháp (10.000.000), 1 hóa đơn đã hủy (20.000.000)
        db.HoaDons.AddRange(
            new HoaDon { Id = 2001, HopDongId = 1001, Thang = 10, Nam = 2026, TongTien = 5_000_000, TrangThai = "DA_PHAT_HANH", MaHoaDon = "HD-01" },
            new HoaDon { Id = 2002, HopDongId = 1001, Thang = 10, Nam = 2026, TongTien = 10_000_000, TrangThai = "NHAP", MaHoaDon = "HD-02" },
            new HoaDon { Id = 2003, HopDongId = 1001, Thang = 10, Nam = 2026, TongTien = 20_000_000, TrangThai = "DA_HUY", MaHoaDon = "HD-03" }
        );

        // Thanh toán cho hóa đơn phát hành:
        // 1 đã xác nhận (3.000.000), 1 chờ xác nhận (1.000.000), 1 đã hủy (1.000.000)
        db.ThanhToans.AddRange(
            new ThanhToanHoaDon { Id = 3001, HoaDonId = 2001, SoTien = 3_000_000, TrangThai = "DA_XAC_NHAN", NgayThanhToan = new DateOnly(2026, 10, 5) },
            new ThanhToanHoaDon { Id = 3002, HoaDonId = 2001, SoTien = 1_000_000, TrangThai = "CHO_XAC_NHAN", NgayThanhToan = new DateOnly(2026, 10, 6) },
            new ThanhToanHoaDon { Id = 3003, HoaDonId = 2001, SoTien = 1_000_000, TrangThai = "DA_HUY", NgayThanhToan = new DateOnly(2026, 10, 7) }
        );

        await db.SaveChangesAsync();

        var service = new BaoCaoService(db, _clock);
        var result = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 10, 2026, 10, 2026);

        var row = Assert.Single(result.ChiTietTheoThang);
        // Chỉ tính hóa đơn DA_PHAT_HANH
        Assert.Equal(5_000_000, row.TongPhatHanh);
        // Chỉ tính thanh toán DA_XAC_NHAN
        Assert.Equal(3_000_000, row.TongDaThu);
        // Số còn phải thu = 5.000.000 - 3.000.000 = 2.000.000
        Assert.Equal(2_000_000, row.SoConPhaiThu);
        Assert.Equal(60.0m, row.TiLeThu);
    }

    [Fact]
    public async Task DepositOffset_ReducesRemainingAmountWithoutInflatingCashRevenue()
    {
        using var db = CreateContext();

        db.PhongTros.Add(new PhongTro { Id = 101, ToaNhaId = 1, MaPhong = "101", TrangThai = "DANG_THUE", GiaThue = 5_000_000, DienTich = 25, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1) });
        db.HopDongs.Add(new HopDongThamChieu { Id = 1001, PhongId = 101, TrangThai = "DANG_HIEU_LUC", MaHopDong = "HD-1001", KhachDungTenId = 40 });
        db.KyHopDongs.Add(new KyHopDongThamChieu { Id = 1001, HopDongId = 1001, NgayBatDau = new DateOnly(2026, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31) });

        db.HoaDons.Add(new HoaDon { Id = 2001, HopDongId = 1001, Thang = 10, Nam = 2026, TongTien = 5_000_000, TrangThai = "DA_PHAT_HANH", MaHoaDon = "HD-01" });

        // Thu tiền 2.000.000 + Cấn trừ cọc 2.000.000
        db.ThanhToans.Add(new ThanhToanHoaDon { Id = 3001, HoaDonId = 2001, SoTien = 2_000_000, TrangThai = "DA_XAC_NHAN", NgayThanhToan = new DateOnly(2026, 10, 5) });
        db.GiaoDichCocs.Add(new GiaoDichCoc
        {
            Id = 4001,
            HopDongId = 1001,
            HoaDonId = 2001,
            Loai = "CAN_TRU",
            SoTien = 2_000_000,
            TrangThai = "DA_XAC_NHAN",
            Ma = "CT-01",
            KhoaChongTrung = "K1",
            NguoiThucHienId = 10,
            NgayGiaoDich = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new BaoCaoService(db, _clock);
        var result = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 10, 2026, 10, 2026);

        var row = Assert.Single(result.ChiTietTheoThang);
        Assert.Equal(5_000_000, row.TongPhatHanh);
        // Tiền thực thu bằng tiền mặt/chuyển khoản là 2.000.000 (cấn cọc không tính là thu tiền thêm)
        Assert.Equal(2_000_000, row.TongDaThu);
        Assert.Equal(2_000_000, row.TongCanTru);
        // Còn phải thu = 5.000.000 - 2.000.000 (thu) - 2.000.000 (cấn cọc) = 1.000.000
        Assert.Equal(1_000_000, row.SoConPhaiThu);
    }

    [Fact]
    public async Task OccupancyExcludesStoppedRooms_AndHandlesZeroDenominator()
    {
        using var db = CreateContext();

        // Phòng 101: đang thuê
        // Phòng 102: ngừng cho thuê (bảo trì)
        db.PhongTros.AddRange(
            new PhongTro { Id = 101, ToaNhaId = 1, MaPhong = "101", TrangThai = "DANG_THUE", GiaThue = 5_000_000, DienTich = 25, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1) },
            new PhongTro { Id = 102, ToaNhaId = 1, MaPhong = "102", TrangThai = "NGUNG_CHO_THUE", GiaThue = 5_000_000, DienTich = 25, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1) }
        );

        db.HopDongs.Add(new HopDongThamChieu { Id = 1001, PhongId = 101, TrangThai = "DANG_HIEU_LUC", MaHopDong = "HD-1001", KhachDungTenId = 40 });
        db.KyHopDongs.Add(new KyHopDongThamChieu { Id = 1001, HopDongId = 1001, NgayBatDau = new DateOnly(2026, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31) });

        await db.SaveChangesAsync();

        var service = new BaoCaoService(db, _clock);
        var result = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 10, 2026, 10, 2026);

        var row = Assert.Single(result.ChiTietTheoThang);
        // Phòng 102 bị loại khỏi tổng số phòng đang cho thuê của toà
        Assert.Equal(1, row.SoPhongDangThue);
        Assert.Equal(1, row.TongPhongChoThue);
        Assert.Equal(100.0m, row.TiLeLapDay);

        // Trường hợp toà không có phòng nào cho thuê (chỉ toàn ngừng cho thuê)
        db.PhongTros.Remove((await db.PhongTros.FindAsync(101))!);
        await db.SaveChangesAsync();

        var resultEmpty = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 10, 2026, 10, 2026);
        var rowEmpty = Assert.Single(resultEmpty.ChiTietTheoThang);
        Assert.Equal(0, rowEmpty.TongPhongChoThue);
        Assert.Null(rowEmpty.TiLeLapDay); // Mẫu số 0 hiển thị không áp dụng
    }

    [Fact]
    public async Task RoomStatusHistory_TakesPrecedenceOverCurrentState()
    {
        using var db = CreateContext();

        // Phòng 101 hiện tại TRONG, nhưng trong quá khứ (tháng 6/2026) có lịch sử DANG_THUE
        db.PhongTros.Add(new PhongTro { Id = 101, ToaNhaId = 1, MaPhong = "101", TrangThai = "TRONG", GiaThue = 5_000_000, DienTich = 25, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1) });

        // Ghi nhận lịch sử trạng thái phòng tháng 6/2026
        db.LichSuTrangThaiPhongs.Add(new LichSuTrangThaiPhong
        {
            PhongId = 101,
            TrangThai = "DANG_THUE",
            TuThoiDiem = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            DenThoiDiem = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();

        var service = new BaoCaoService(db, _clock);
        var result = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 6, 2026, 6, 2026);

        var row = Assert.Single(result.ChiTietTheoThang);
        Assert.Equal(1, row.SoPhongDangThue);
        Assert.Equal(1, row.TongPhongChoThue);
        Assert.Equal(100.0m, row.TiLeLapDay);
    }

    [Fact]
    public async Task FilterValidation_RejectsMoreThan12Months_AndInvertedDates()
    {
        using var db = CreateContext();
        var service = new BaoCaoService(db, _clock);

        // Khoảng hơn 12 tháng (13 tháng: 1/2025 -> 1/2026)
        var over12 = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 1, 2025, 1, 2026);
        Assert.Equal("Khoảng thời gian xem báo cáo tối đa là 12 tháng một lần xem.", over12.ErrorMessage);

        // Tháng bắt đầu lớn hơn tháng kết thúc
        var inverted = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 11, 2026, 5, 2026);
        Assert.Equal("Tháng bắt đầu không được lớn hơn tháng kết thúc.", inverted.ErrorMessage);
    }

    [Fact]
    public async Task SixMonthChartData_MatchesInvoiceTableNumbersExactly()
    {
        using var db = CreateContext();

        db.PhongTros.Add(new PhongTro { Id = 101, ToaNhaId = 1, MaPhong = "101", TrangThai = "DANG_THUE", GiaThue = 5_000_000, DienTich = 25, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1) });
        db.HopDongs.Add(new HopDongThamChieu { Id = 1001, PhongId = 101, TrangThai = "DANG_HIEU_LUC", MaHopDong = "HD-1001", KhachDungTenId = 40 });
        db.KyHopDongs.Add(new KyHopDongThamChieu { Id = 1001, HopDongId = 1001, NgayBatDau = new DateOnly(2026, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31) });

        // Tạo hóa đơn cho 6 tháng: T5, T6, T7, T8, T9, T10 năm 2026
        for (int m = 5; m <= 10; m++)
        {
            db.HoaDons.Add(new HoaDon
            {
                Id = 2000 + m,
                HopDongId = 1001,
                Thang = m,
                Nam = 2026,
                TongTien = m * 1_000_000,
                TrangThai = "DA_PHAT_HANH",
                MaHoaDon = $"HD-2026-{m:D2}"
            });

            db.ThanhToans.Add(new ThanhToanHoaDon
            {
                Id = 3000 + m,
                HoaDonId = 2000 + m,
                SoTien = (m - 1) * 1_000_000,
                TrangThai = "DA_XAC_NHAN",
                NgayThanhToan = new DateOnly(2026, m, 15)
            });
        }

        await db.SaveChangesAsync();

        var service = new BaoCaoService(db, _clock);
        var result = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 5, 2026, 10, 2026);

        Assert.Equal(6, result.ChiTietTheoThang.Count);
        Assert.Equal(6, result.BieuDo6Thang.Count);

        for (int i = 0; i < 6; i++)
        {
            var tableRow = result.ChiTietTheoThang[i];
            var chartItem = result.BieuDo6Thang[i];

            Assert.Equal(tableRow.Thang, chartItem.Thang);
            Assert.Equal(tableRow.Nam, chartItem.Nam);
            Assert.Equal(tableRow.TongPhatHanh, chartItem.TongPhatHanh);
            Assert.Equal(tableRow.TongDaThu, chartItem.TongDaThu);
            Assert.Equal(tableRow.SoConPhaiThu, chartItem.SoConPhaiThu);
            Assert.Equal(tableRow.TiLeThu, chartItem.TiLeThu);
        }
    }

    [Fact]
    public async Task BackendSecurity_BlocksForeignBuildingAndTenant()
    {
        using var db = CreateContext();
        var service = new BaoCaoService(db, _clock);

        // Chủ nhà 1 cố gắng xem toà 2 của Chủ nhà 2 -> 403
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LayBaoCaoAsync(10, "CHU_NHA", 2, 10, 2026, 10, 2026));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LayBaoCaoThangAsync(10, "CHU_NHA", 2, 10, 2026));

        // Khách thuê cố gắng xem báo cáo -> 403
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LayBaoCaoAsync(40, "KHACH_THUE", 1, 10, 2026, 10, 2026));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LayBaoCaoThangAsync(40, "KHACH_THUE", 1, 10, 2026));

        // Quản lý xem toà được gán -> được phép (Toà 1 gán cho Quản lý 1 ID 30)
        var qlResult = await service.LayBaoCaoAsync(30, "QUAN_LY", 1, 10, 2026, 10, 2026);
        Assert.NotNull(qlResult);

        // Quản lý xem toà không được gán (Toà 2) -> 403
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LayBaoCaoAsync(30, "QUAN_LY", 2, 10, 2026, 10, 2026));

        // Controller Index action trả về ForbidResult khi bị UnauthorizedAccessException
        var controller = new BaoCaoController(service);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Role, "CHU_NHA")
        ], "TestAuth"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.Index(2, 10, 2026, 10, 2026);
        Assert.IsType<ForbidResult>(actionResult);
    }

    [Fact]
    public async Task MonthlyReport_WithoutIssuedInvoices_ReturnsZeroValuesAndZeroRate()
    {
        using var db = CreateContext();
        var service = new BaoCaoService(db, _clock);

        var result = await service.LayBaoCaoThangAsync(10, "CHU_NHA", 1, 10, 2026);

        Assert.Null(result.ErrorMessage);
        Assert.Equal(0, result.TongPhatHanh);
        Assert.Equal(0, result.TongDaThu);
        Assert.Equal(0, result.TongConPhaiThu);
        Assert.Equal(0m, result.TiLeThuTongThe);
    }

    [Fact]
    public async Task PerformanceBenchmark_500Rooms12Months_LoadsUnder3Seconds()
    {
        using var db = CreateContext();

        // Tạo 1 toà nhà với 500 phòng
        const int roomCount = 500;
        var rooms = new List<PhongTro>(roomCount);
        var contracts = new List<HopDongThamChieu>(roomCount);
        var periods = new List<KyHopDongThamChieu>(roomCount);
        var invoices = new List<HoaDon>(roomCount * 12);
        var payments = new List<ThanhToanHoaDon>(roomCount * 12);

        int invoiceId = 10000;
        int paymentId = 10000;

        for (int i = 1; i <= roomCount; i++)
        {
            int rId = 1000 + i;
            int cId = 2000 + i;
            rooms.Add(new PhongTro
            {
                Id = rId,
                ToaNhaId = 1,
                MaPhong = $"P{i:D3}",
                TrangThai = i <= 400 ? "DANG_THUE" : "TRONG",
                GiaThue = 4_000_000,
                DienTich = 25,
                SoNguoiToiDa = 2,
                NgayTao = new DateTime(2025, 1, 1)
            });

            if (i <= 400)
            {
                contracts.Add(new HopDongThamChieu { Id = cId, PhongId = rId, TrangThai = "DANG_HIEU_LUC", MaHopDong = $"HD-{cId}", KhachDungTenId = 40 });
                periods.Add(new KyHopDongThamChieu { Id = cId, HopDongId = cId, NgayBatDau = new DateOnly(2025, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31) });

                // 12 tháng hóa đơn
                for (int m = 1; m <= 12; m++)
                {
                    invoiceId++;
                    paymentId++;
                    invoices.Add(new HoaDon
                    {
                        Id = invoiceId,
                        HopDongId = cId,
                        Thang = m,
                        Nam = 2026,
                        TongTien = 4_500_000,
                        TrangThai = "DA_PHAT_HANH",
                        MaHoaDon = $"HD-26-{m}-{i}"
                    });

                    payments.Add(new ThanhToanHoaDon
                    {
                        Id = paymentId,
                        HoaDonId = invoiceId,
                        SoTien = 4_000_000,
                        TrangThai = "DA_XAC_NHAN",
                        NgayThanhToan = new DateOnly(2026, m, 10)
                    });
                }
            }
        }

        db.PhongTros.AddRange(rooms);
        db.HopDongs.AddRange(contracts);
        db.KyHopDongs.AddRange(periods);
        db.HoaDons.AddRange(invoices);
        db.ThanhToans.AddRange(payments);

        await db.SaveChangesAsync();

        var service = new BaoCaoService(db, _clock);

        // Đo thời gian thực hiện truy vấn và tổng hợp 12 tháng của 500 phòng
        var sw = Stopwatch.StartNew();
        var result = await service.LayBaoCaoAsync(
            actorId: 10,
            role: "CHU_NHA",
            toaNhaId: 1,
            tuThang: 1,
            tuNam: 2026,
            denThang: 12,
            denNam: 2026
        );
        sw.Stop();

        Assert.Null(result.ErrorMessage);
        Assert.Equal(12, result.ChiTietTheoThang.Count);
        Assert.Equal(6, result.BieuDo6Thang.Count);

        // Kiểm tra số liệu tổng quát
        // 400 phòng * 12 tháng * 4.500.000 = 21.600.000.000 đ
        Assert.Equal(21_600_000_000, result.TongPhatHanh);
        // 400 phòng * 12 tháng * 4.000.000 = 19.200.000.000 đ
        Assert.Equal(19_200_000_000, result.TongDaThu);
        Assert.Equal(2_400_000_000, result.TongConPhaiThu);

        // Tiêu chí chấp nhận: Trang tải xong dưới 3 giây với dữ liệu 12 tháng của 500 phòng
        Assert.True(sw.ElapsedMilliseconds < 3000, $"Thời gian xử lý: {sw.ElapsedMilliseconds}ms (yêu cầu < 3000ms)");
    }
}
