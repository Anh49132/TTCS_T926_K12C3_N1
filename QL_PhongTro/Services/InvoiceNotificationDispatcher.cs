using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
namespace QL_PhongTro.Services;

public sealed class InvoiceNotificationDispatcher(AppDbContext db, IQueuedInvoiceEmailSender sender, ITimeProvider clock)
{
    public static async Task<bool> IsInstalledAsync(AppDbContext context, CancellationToken cancellationToken = default) =>
        await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('thong_bao','hoa_don')")
            .SingleAsync(cancellationToken) == 2;

    public async Task<int> DispatchAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsInstalledAsync(db, cancellationToken)) return 0;
        var now = clock.UtcNow;
        var publishedIds = db.HoaDons.Where(x => x.TrangThai == "DA_PHAT_HANH").Select(x => x.Id);
        var candidates = await db.ThongBaoHoaDons.AsNoTracking().Where(x =>
            (x.LoaiThongBao == LoaiThongBaoHoaDon.HoaDonMoi
                || x.LoaiThongBao == LoaiThongBaoHoaDon.SapDenHan
                || x.LoaiThongBao == LoaiThongBaoHoaDon.QuaHan)
            && publishedIds.Contains(x.HoaDonId)
            && (x.TrangThaiEmail == "CHO_GUI" || (x.TrangThaiEmail == "DANG_GUI" && x.KhoaXuLyDen < now)))
            .OrderBy(x => x.Id).Take(10).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var item in candidates) {
            cancellationToken.ThrowIfCancellationRequested();
            var lease = clock.UtcNow.AddMinutes(2);
            var claimed = await db.ThongBaoHoaDons.Where(x => x.Id == item.Id && publishedIds.Contains(x.HoaDonId)
                && (x.TrangThaiEmail == "CHO_GUI" || (x.TrangThaiEmail == "DANG_GUI" && x.KhoaXuLyDen < now)))
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.TrangThaiEmail, "DANG_GUI").SetProperty(x => x.KhoaXuLyDen, lease)
                    .SetProperty(x => x.SoLanGui, x => x.SoLanGui + 1), cancellationToken);
            if (claimed == 0) continue;
            try {
                await sender.SendIssuedAsync(item.EmailNhan, item.TieuDe, item.NoiDung, item.DuongDan);
                await db.ThongBaoHoaDons.Where(x => x.Id == item.Id && x.TrangThaiEmail == "DANG_GUI" && x.KhoaXuLyDen == lease)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.TrangThaiEmail, "DA_GUI").SetProperty(x => x.NgayGuiThanhCong, clock.UtcNow)
                        .SetProperty(x => x.KhoaXuLyDen, (DateTime?)null).SetProperty(x => x.LoiGanNhat, (string?)null), cancellationToken);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                // Do not store SMTP exception text: it can contain addresses or credentials.
                await db.ThongBaoHoaDons.Where(x => x.Id == item.Id && x.TrangThaiEmail == "DANG_GUI" && x.KhoaXuLyDen == lease)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.TrangThaiEmail, "THAT_BAI")
                        .SetProperty(x => x.KhoaXuLyDen, (DateTime?)null).SetProperty(x => x.LoiGanNhat, "Không gửi được email hóa đơn."), cancellationToken);
            }
        }
        return sent;
    }
}

public sealed class InvoiceNotificationHostedService(IServiceScopeFactory scopes, IOptions<PasswordResetOptions> options,
    ILogger<InvoiceNotificationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                var settings = options.Value;
                if (!string.IsNullOrWhiteSpace(settings.From) && (!string.IsNullOrWhiteSpace(settings.PickupDirectory) || !string.IsNullOrWhiteSpace(settings.Host))) {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<InvoiceNotificationDispatcher>().DispatchAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Chưa xử lý được hàng đợi thông báo hóa đơn; sẽ kiểm tra lại."); }
            try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
