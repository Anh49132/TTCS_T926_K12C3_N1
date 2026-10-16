using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_PhongTro.Data;
using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public sealed class NhacHanService(IServiceScopeFactory scopeFactory, ITimeProvider clock)
{
    public async Task<NhacHanResult> ChayNhacHanAsync(DateOnly? ngayGiaLap = null, CancellationToken cancellationToken = default)
    {
        var result = new NhacHanResult();
        var ngayChay = ngayGiaLap ?? DateOnly.FromDateTime(clock.UtcNow.AddHours(7));

        using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // 1. Nhắc sắp đến hạn (còn 2 ngày)
        var sapDenHan = await db.HoaDons
            .AsNoTracking()
            .Where(h => h.TrangThai == "DA_PHAT_HANH"
                && h.HanThanhToan == ngayChay.AddDays(2)
                && !db.ThongBaoHoaDons.Any(t => t.HoaDonId == h.Id && t.LoaiThongBao == LoaiThongBaoHoaDon.SapDenHan))
            .Select(h => new { h.Id, h.HopDongId, h.Thang, h.Nam, h.TongTien, h.HanThanhToan })
            .ToListAsync(cancellationToken);

        foreach (var invoice in sapDenHan)
        {
            var khachDungTenId = await db.HopDongs
                .Where(h => h.Id == invoice.HopDongId)
                .Select(h => h.KhachDungTenId)
                .SingleAsync(cancellationToken);

            if (khachDungTenId.HasValue)
            {
                var taiKhoan = await (from profile in db.KhachThues.AsNoTracking()
                    join account in db.TaiKhoans.AsNoTracking() on profile.TaiKhoanId equals account.Id
                    where profile.Id == khachDungTenId.Value && account.DangHoatDong && account.EmailConfirmed
                    select new { account.Id, account.Email, account.HoTen })
                    .SingleOrDefaultAsync(cancellationToken);

                if (taiKhoan != null && !string.IsNullOrWhiteSpace(taiKhoan.Email))
                {
                    var soConPhaiTra = await TinhSoConPhaiTraAsync(db, invoice.Id, cancellationToken);
                    if (soConPhaiTra > 0)
                    {
                        db.ThongBaoHoaDons.Add(TaoThongBao(invoice.Id, taiKhoan.Id, taiKhoan.Email,
                            taiKhoan.HoTen, soConPhaiTra, invoice.HanThanhToan, LoaiThongBaoHoaDon.SapDenHan, clock.UtcNow));
                        result.SapDenHan++;
                    }
                }
            }
        }

        // 2. Nhắc quá hạn (ngày sau hạn thanh toán)
        var quaHan = await db.HoaDons
            .AsNoTracking()
            .Where(h => h.TrangThai == "DA_PHAT_HANH"
                && h.HanThanhToan == ngayChay.AddDays(-1)
                && !db.ThongBaoHoaDons.Any(t => t.HoaDonId == h.Id && t.LoaiThongBao == LoaiThongBaoHoaDon.QuaHan))
            .Select(h => new { h.Id, h.HopDongId, h.Thang, h.Nam, h.TongTien, h.HanThanhToan })
            .ToListAsync(cancellationToken);

        foreach (var invoice in quaHan)
        {
            var khachDungTenId = await db.HopDongs
                .Where(h => h.Id == invoice.HopDongId)
                .Select(h => h.KhachDungTenId)
                .SingleAsync(cancellationToken);

            if (khachDungTenId.HasValue)
            {
                var taiKhoan = await (from profile in db.KhachThues.AsNoTracking()
                    join account in db.TaiKhoans.AsNoTracking() on profile.TaiKhoanId equals account.Id
                    where profile.Id == khachDungTenId.Value && account.DangHoatDong && account.EmailConfirmed
                    select new { account.Id, account.Email, account.HoTen })
                    .SingleOrDefaultAsync(cancellationToken);

                if (taiKhoan != null && !string.IsNullOrWhiteSpace(taiKhoan.Email))
                {
                    var soConPhaiTra = await TinhSoConPhaiTraAsync(db, invoice.Id, cancellationToken);
                    if (soConPhaiTra > 0)
                    {
                        db.ThongBaoHoaDons.Add(TaoThongBao(invoice.Id, taiKhoan.Id, taiKhoan.Email,
                            taiKhoan.HoTen, soConPhaiTra, invoice.HanThanhToan, LoaiThongBaoHoaDon.QuaHan, clock.UtcNow));
                        result.QuaHan++;
                    }
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static ThongBaoHoaDon TaoThongBao(
        int hoaDonId, int nguoiNhanId, string email, string ten, long soConPhaiTra,
        DateOnly hanThanhToan, string loaiThongBao, DateTime ngayTaoUtc)
    {
        var laSapDenHan = loaiThongBao == LoaiThongBaoHoaDon.SapDenHan;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var tieuDe = laSapDenHan ? "Hóa đơn sắp đến hạn thanh toán" : "Hóa đơn đã quá hạn thanh toán";
        var noiDung = $"Xin chào {ten}, hóa đơn của bạn còn phải thanh toán {soConPhaiTra.ToString("N0", culture)} đ. " +
            $"Hạn thanh toán: {hanThanhToan:dd/MM/yyyy}.";
        return new ThongBaoHoaDon
        {
            NguoiNhanId = nguoiNhanId,
            HoaDonId = hoaDonId,
            LoaiThongBao = loaiThongBao,
            TieuDe = tieuDe,
            NoiDung = noiDung,
            DuongDan = $"/ThongBao/HoaDon/{hoaDonId}",
            EmailNhan = email,
            NgayTao = ngayTaoUtc,
            TrangThaiEmail = "CHO_GUI"
        };
    }

    private static async Task<long> TinhSoConPhaiTraAsync(AppDbContext db, int hoaDonId, CancellationToken cancellationToken)
    {
        var tongThanhToan = await db.ThanhToans
            .Where(t => t.HoaDonId == hoaDonId && t.TrangThai == "DA_XAC_NHAN")
            .SumAsync(t => (long?)t.SoTien, cancellationToken) ?? 0L;

        var tongCanTru = await db.GiaoDichCocs
            .Where(g => g.HoaDonId == hoaDonId && g.TrangThai == "DA_XAC_NHAN" && g.Loai == "CAN_TRU")
            .SumAsync(g => (long?)g.SoTien, cancellationToken) ?? 0L;

        var invoice = await db.HoaDons
            .AsNoTracking()
            .Where(h => h.Id == hoaDonId)
            .Select(h => h.TongTien)
            .SingleOrDefaultAsync(cancellationToken);

        var conPhaiTra = invoice - tongThanhToan - tongCanTru;
        return conPhaiTra > 0 ? conPhaiTra : 0;
    }

    public static async Task<NhacHanResult> ChayTuCommandLine(string databasePath, DateOnly? ngayGiaLap = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                ForeignKeys = true
            }.ToString()));
        services.AddScoped<ITimeProvider, SystemTimeProvider>();
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<NhacHanService>();
        return await service.ChayNhacHanAsync(ngayGiaLap);
    }
}

public sealed class NhacHanResult
{
    public int SapDenHan { get; set; }
    public int QuaHan { get; set; }
    public int Tong => SapDenHan + QuaHan;
}

public sealed class NhacHanHostedService(IServiceScopeFactory scopeFactory, ILogger<NhacHanHostedService> logger, ITimeProvider clock)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextRun = GetNext08h00Vn(clock.UtcNow);
                var delay = nextRun - clock.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    try { await Task.Delay(delay, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<NhacHanService>();
                await service.ChayNhacHanAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không xử lý được nhắc hạn hóa đơn.");
                try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    public static DateTime GetNext08h00Vn(DateTime utcNow)
    {
        var vnNow = utcNow.AddHours(7);
        var today08 = new DateTime(vnNow.Year, vnNow.Month, vnNow.Day, 8, 0, 0);
        return vnNow < today08 ? today08.AddHours(-7) : today08.AddDays(1).AddHours(-7);
    }
}
