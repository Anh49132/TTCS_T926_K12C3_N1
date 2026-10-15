using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QL_PhongTro.Data;
using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public sealed class NhacHanService(IServiceScopeFactory scopeFactory, ILogger<NhacHanService> logger, ITimeProvider clock)
{
    public async Task<NhacHanResult> ChayNhacHanAsync(DateOnly? ngayGiaLap = null, CancellationToken cancellationToken = default)
    {
        var result = new NhacHanResult();
        var ngayChay = ngayGiaLap ?? DateOnly.FromDateTime(clock.UtcNow.AddHours(7));

        using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var thongBaoService = scope.ServiceProvider.GetRequiredService<ThongBaoHoaDonService>();

        // 1. Nhắc sắp đến hạn (còn 2 ngày)
        var sapDenHan = await db.HoaDons
            .AsNoTracking()
            .Where(h => h.TrangThai == ""DA_PHAT_HANH""
                && h.HanThanhToan == ngayChay.AddDays(2)
                && !db.ThongBaos.Any(t => t.HoaDonId == h.Id && t.LoaiThongBao == LoaiThongBaoHoaDon.SapDenHan))
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
                var taiKhoan = await db.TaiKhoans
                    .AsNoTracking()
                    .Where(t => t.Id == khachDungTenId.Value && t.DangHoatDong && t.EmailConfirmed)
                    .Select(t => new { t.Id, t.Email, t.HoTen })
                    .SingleOrDefaultAsync(cancellationToken);

                if (taiKhoan != null && !string.IsNullOrWhiteSpace(taiKhoan.Email))
                {
                    var soConPhaiTra = await TinhSoConPhaiTraAsync(db, invoice.Id, cancellationToken);
                    if (soConPhaiTra > 0)
                    {
                        await thongBaoService.TaoThongBaoSapDenHanAsync(invoice.Id, taiKhoan.Id, taiKhoan.Email, taiKhoan.HoTen, soConPhaiTra, cancellationToken);
                        result.SapDenHan++;
                    }
                }
            }
        }

        // Quá hạn processing is intentionally omitted for this branch (S4-08).
        // It will be implemented in a later branch (S4-09?).

        return result;
    }

    private static async Task<long> TinhSoConPhaiTraAsync(AppDbContext db, int hoaDonId, CancellationToken cancellationToken)
    {
        var tongThanhToan = await db.ThanhToans
            .Where(t => t.HoaDonId == hoaDonId && t.TrangThai == ""DA_XAC_NHAN"")
            .SumAsync(t => (long?)t.SoTien, cancellationToken) ?? 0L;

        var tongCanTru = await db.GiaoDichCocs
            .Where(g => g.HoaDonId == hoaDonId && g.TrangThai == ""DA_XAC_NHAN"" && g.Loai == ""CAN_TRU"")
            .SumAsync(g => (long?)g.SoTien, cancellationToken) ?? 0L;

        var invoice = await db.HoaDons
            .AsNoTracking()
            .Where(h => h.Id == hoaDonId)
            .Select(h => h.TongTien)
            .SingleOrDefaultAsync(cancellationToken);

        var conPhaiTra = invoice - tongThanhToan - tongCanTru;
        return conPhaiTra > 0 ? conPhaiTra : 0;
    }
}

public sealed class NhacHanResult
{
    public int SapDenHan { get; set; }
    public int QuaHan { get; set; } // Kept for compatibility, remains 0 in this branch
    public int Tong => SapDenHan + QuaHan;
}
