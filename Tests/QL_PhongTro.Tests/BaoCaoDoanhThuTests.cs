using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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
            CREATE TABLE chi_tiet_hoa_don (
                id INTEGER PRIMARY KEY, hoa_don_id INTEGER NOT NULL, so_thu_tu INTEGER NOT NULL,
                dich_vu_id INTEGER NULL, cau_hinh_dich_vu_id INTEGER NULL, ky_hop_dong_id INTEGER NULL,
                loai_khoan TEXT NOT NULL DEFAULT 'DICH_VU', ten_khoan TEXT NOT NULL DEFAULT '',
                cach_tinh_ap_dung TEXT NULL, don_vi_tinh TEXT NULL, so_luong TEXT NOT NULL DEFAULT '1',
                don_gia INTEGER NOT NULL DEFAULT 0, chi_so_dau TEXT NULL, chi_so_cuoi TEXT NULL,
                so_ngay_tinh_tien INTEGER NULL, so_ngay_trong_thang INTEGER NULL,
                thanh_tien INTEGER NOT NULL DEFAULT 0, ghi_chu TEXT NULL, chi_so_id INTEGER NULL, bao_hong_id INTEGER NULL
            );
            CREATE TABLE hop_dong_dich_vu (
                id INTEGER PRIMARY KEY, hop_dong_id INTEGER NOT NULL, dich_vu_id INTEGER NOT NULL,
                cau_hinh_dich_vu_id INTEGER NOT NULL, ten_dich_vu TEXT NOT NULL, cach_tinh TEXT NOT NULL,
                don_vi_tinh TEXT NOT NULL, don_gia INTEGER NOT NULL, ngay_ghi_nhan TEXT NOT NULL
            );
            CREATE INDEX ix_hoa_don_report_status_period_contract ON hoa_don(trang_thai,nam,thang,hop_dong_id);
            CREATE INDEX ix_thanh_toan_hoa_don_trang_thai ON thanh_toan(hoa_don_id,trang_thai);

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
        using (var invoiceGuards = conn.BeginTransaction())
        {
            InvoicePaymentReportSchema.Ensure(conn, invoiceGuards);
            InvoicePaymentProofSchema.Ensure(conn, invoiceGuards);
            ReportPerformanceSchema.Ensure(conn, invoiceGuards);
            InvoiceCollectionSchema.Ensure(conn, invoiceGuards);
            InvoiceCollectionCancellationSchema.Ensure(conn, invoiceGuards);
            TatToanSchema.EnsureInvoiceGuard(conn, invoiceGuards);
            XacNhanTatToanSchema.EnsureInvoiceGuards(conn, invoiceGuards);
            BaoHongChiPhiSchema.InstallForNewInvoiceModule(conn, invoiceGuards);
            invoiceGuards.Commit();
        }
    }

    private static string AntiForgeryToken(string html) => WebUtility.HtmlDecode(
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

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
        var buildingOccupancy = Assert.Single(monthly.LapDayTheoToaNha);
        Assert.Equal(2, buildingOccupancy.SoPhongDangThue);
        Assert.Equal(3, buildingOccupancy.TongPhongChoThue);
        Assert.Equal(1, buildingOccupancy.SoPhongTrong);
        Assert.Equal(66.7m, buildingOccupancy.TiLeLapDay);

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
        db.KyHopDongs.Add(new KyHopDongThamChieu { Id = 1001, HopDongId = 1001, NgayBatDau = new DateOnly(2026, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31), GiaThue = 1_000_000 });

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
        db.KyHopDongs.Add(new KyHopDongThamChieu { Id = 1001, HopDongId = 1001, NgayBatDau = new DateOnly(2026, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31), GiaThue = 1_000_000 });

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
        db.KyHopDongs.Add(new KyHopDongThamChieu { Id = 1001, HopDongId = 1001, NgayBatDau = new DateOnly(2026, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31), GiaThue = 1_000_000 });

        await db.SaveChangesAsync();

        var service = new BaoCaoService(db, _clock);
        var result = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 10, 2026, 10, 2026);

        var row = Assert.Single(result.ChiTietTheoThang);
        // Phòng 102 bị loại khỏi tổng số phòng đang cho thuê của toà
        Assert.Equal(1, row.SoPhongDangThue);
        Assert.Equal(1, row.TongPhongChoThue);
        Assert.Equal(100.0m, row.TiLeLapDay);
        var monthly = await service.LayBaoCaoThangAsync(10, "CHU_NHA", 1, 10, 2026);
        var monthlyBuilding = Assert.Single(monthly.LapDayTheoToaNha);
        Assert.Equal(1, monthlyBuilding.SoPhongDangThue);
        Assert.Equal(1, monthlyBuilding.TongPhongChoThue);
        Assert.Equal(100.0m, monthlyBuilding.TiLeLapDay);

        // Trường hợp toà không có phòng nào cho thuê (chỉ toàn ngừng cho thuê)
        (await db.PhongTros.FindAsync(101))!.TrangThai = "NGUNG_CHO_THUE";
        var activeTerm = await db.KyHopDongs.FindAsync(1001);
        activeTerm!.NgayKetThuc = new DateOnly(2026, 9, 30);
        (await db.HopDongs.FindAsync(1001))!.TrangThai = "DA_KET_THUC";
        await db.SaveChangesAsync();

        var resultEmpty = await service.LayBaoCaoAsync(10, "CHU_NHA", 1, 10, 2026, 10, 2026);
        var rowEmpty = Assert.Single(resultEmpty.ChiTietTheoThang);
        Assert.Equal(0, rowEmpty.TongPhongChoThue);
        Assert.Null(rowEmpty.TiLeLapDay); // Mẫu số 0 hiển thị không áp dụng
        var monthlyEmpty = await service.LayBaoCaoThangAsync(10, "CHU_NHA", 1, 10, 2026);
        Assert.Null(Assert.Single(monthlyEmpty.LapDayTheoToaNha).TiLeLapDay);
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
        db.KyHopDongs.Add(new KyHopDongThamChieu { Id = 1001, HopDongId = 1001, NgayBatDau = new DateOnly(2026, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31), GiaThue = 1_000_000 });

        // Tạo hóa đơn cho 6 tháng: T5, T6, T7, T8, T9, T10 năm 2026
        for (int m = 5; m <= 10; m++)
        {
            if (m == 7) continue; // Tháng không có hóa đơn vẫn phải có cột bằng 0.

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
        Assert.Equal(new[] { 5, 6, 7, 8, 9, 10 }, result.BieuDo6Thang.Select(x => x.Thang));
        Assert.Equal(0, result.BieuDo6Thang.Single(x => x.Thang == 7).TongPhatHanh);
        Assert.Equal(0, result.ChiTietTheoThang.Single(x => x.Thang == 7).TongPhatHanh);

        for (int i = 0; i < 6; i++)
        {
            var tableRow = result.ChiTietTheoThang[i];
            var chartItem = result.BieuDo6Thang[i];

            Assert.Equal(tableRow.Thang, chartItem.Thang);
            Assert.Equal(tableRow.Nam, chartItem.Nam);
            Assert.Equal(tableRow.TongPhatHanh, chartItem.TongPhatHanh);
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
    public async Task MonthlyOccupancy_UsesInclusiveContractDates_AndExcludesStoppedRooms()
    {
        using var db = CreateContext();
        var rooms = Enumerable.Range(1, 10).Select(i => new PhongTro
        {
            Id = 500 + i,
            ToaNhaId = 1,
            MaPhong = $"{500 + i}",
            TrangThai = i <= 8 ? "DANG_THUE" : "TRONG",
            GiaThue = 2_000_000,
            DienTich = 20,
            SoNguoiToiDa = 2,
            NgayTao = new DateTime(2026, 1, 1)
        }).ToArray();
        db.PhongTros.AddRange(rooms);
        for (var i = 1; i <= 8; i++)
        {
            var contractId = 800 + i;
            db.HopDongs.Add(new HopDongThamChieu
            {
                Id = contractId,
                PhongId = 500 + i,
                TrangThai = i == 2 ? "DA_KET_THUC" : "DANG_HIEU_LUC",
                NgayTraPhong = i == 2 ? new DateOnly(2026, 10, 31) : null,
                MaHopDong = $"HD-{contractId}",
                KhachDungTenId = 40
            });
            db.KyHopDongs.Add(new KyHopDongThamChieu
            {
                Id = contractId,
                HopDongId = contractId,
                NgayBatDau = i == 1 ? new DateOnly(2026, 10, 31) : new DateOnly(2026, 1, 1),
                NgayKetThuc = i == 2 ? new DateOnly(2026, 10, 31) : new DateOnly(2026, 12, 31),
                GiaThue = 1_000_000
            });
        }
        await db.SaveChangesAsync();

        var result = await new BaoCaoService(db, _clock).LayBaoCaoThangAsync(10, "CHU_NHA", 1, 10, 2026);
        var building = Assert.Single(result.LapDayTheoToaNha);

        Assert.Equal(8, building.SoPhongDangThue);
        Assert.Equal(10, building.TongPhongChoThue);
        Assert.Equal(80m, building.TiLeLapDay);
    }

    [Fact]
    public async Task MonthlyOccupancy_IsZeroWhenAllRentableRoomsAreVacant()
    {
        using var db = CreateContext(actorId: 20);
        db.PhongTros.AddRange(Enumerable.Range(1, 10).Select(i => new PhongTro
        {
            Id = 600 + i,
            ToaNhaId = 2,
            MaPhong = $"{600 + i}",
            TrangThai = "TRONG",
            GiaThue = 2_000_000,
            DienTich = 20,
            SoNguoiToiDa = 2,
            NgayTao = new DateTime(2026, 1, 1)
        }));
        db.PhongTros.Add(new PhongTro
        {
            Id = 611, ToaNhaId = 2, MaPhong = "611", TrangThai = "NGUNG_CHO_THUE",
            GiaThue = 2_000_000, DienTich = 20, SoNguoiToiDa = 2, NgayTao = new DateTime(2026, 1, 1)
        });
        await db.SaveChangesAsync();

        var result = await new BaoCaoService(db, _clock).LayBaoCaoThangAsync(20, "CHU_NHA", 2, 10, 2026);
        var building = Assert.Single(result.LapDayTheoToaNha);

        Assert.Equal(0, building.SoPhongDangThue);
        Assert.Equal(10, building.TongPhongChoThue);
        Assert.Equal(0, building.TiLeLapDay);
    }

    [Fact]
    public void ReportIndexes_AreInstalledAndValidated()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            ForeignKeys = true,
            Pooling = false
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM app_schema_version";
        Assert.Equal(39L, command.ExecuteScalar());
        ReportPerformanceSchema.Validate(connection);
    }

    [Fact]
    public void EmailGuiLogSchema_IsInstalledAndValidated()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            ForeignKeys = true,
            Pooling = false
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM app_schema_version";
        Assert.Equal(39L, command.ExecuteScalar());
        EmailGuiLogSchema.Validate(connection);
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
                periods.Add(new KyHopDongThamChieu { Id = cId, HopDongId = cId, NgayBatDau = new DateOnly(2025, 1, 1), NgayKetThuc = new DateOnly(2026, 12, 31), GiaThue = 1_000_000 });

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
        Assert.Equal(400, result.ChiTietTheoThang[^1].SoPhongDangThue);
        Assert.Equal(500, result.ChiTietTheoThang[^1].TongPhongChoThue);
        Assert.Equal(80m, result.ChiTietTheoThang[^1].TiLeLapDay);
        Assert.All(result.BieuDo6Thang, chart => Assert.Equal(1_800_000_000, chart.TongPhatHanh));
        var occupancy = Assert.Single(result.LapDayTheoToaNha);
        Assert.Equal(400, occupancy.SoPhongDangThue);
        Assert.Equal(500, occupancy.TongPhongChoThue);
        Assert.Equal(80m, occupancy.TiLeLapDay);

        // Kiểm tra số liệu tổng quát
        // 400 phòng * 12 tháng * 4.500.000 = 21.600.000.000 đ
        Assert.Equal(21_600_000_000, result.TongPhatHanh);
        // 400 phòng * 12 tháng * 4.000.000 = 19.200.000.000 đ
        Assert.Equal(19_200_000_000, result.TongDaThu);
        Assert.Equal(2_400_000_000, result.TongConPhaiThu);

        // HTTP acceptance check includes authentication, controller, aggregation, and Razor rendering.
        var password = "ReportPerf2026Aa1";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 4);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tai_khoan SET mat_khau={passwordHash},must_change_password=0 WHERE id=10");

        var root = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(root, "QL_PhongTro", "Data")))
            root = Directory.GetParent(root)!.FullName;
        using var web = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(Path.Combine(root, "QL_PhongTro"));
            builder.UseSetting("DatabasePath", _dbPath);
            builder.UseSetting("RoomImagesPath", _dbPath + ".room-images");
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
                services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });

        using var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginPage = await client.GetStringAsync("/Account/Login");
        var loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TaiKhoanDangNhap"] = "chunha1@test.vn",
            ["MatKhau"] = password,
            ["__RequestVerificationToken"] = AntiForgeryToken(loginPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        const string reportUrl = "/BaoCao?toaNhaId=1&tuThang=1&tuNam=2026&denThang=12&denNam=2026";
        using (var warmup = await client.GetAsync(reportUrl))
            Assert.Equal(HttpStatusCode.OK, warmup.StatusCode);
        var pageTimer = Stopwatch.StartNew();
        using var pageResponse = await client.GetAsync(reportUrl);
        var page = await pageResponse.Content.ReadAsStringAsync();
        pageTimer.Stop();
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("Doanh thu 6 tháng gần nhất", page);
        Assert.Contains("Tỷ lệ lấp đầy", page);
        Assert.True(pageTimer.ElapsedMilliseconds < 3000, $"Full report page: {pageTimer.ElapsedMilliseconds}ms (target < 3000ms)");

        // Service-only timing remains useful for identifying query regressions separately.
        Assert.True(sw.ElapsedMilliseconds < 3000, $"Thời gian xử lý: {sw.ElapsedMilliseconds}ms (yêu cầu < 3000ms)");
    }
}
