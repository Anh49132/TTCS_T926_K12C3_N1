using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
namespace QL_PhongTro.Services;

public sealed class InvoiceNotificationDispatcher(AppDbContext db, IQueuedInvoiceEmailSender sender, ITimeProvider clock)
{
    private const int MaxAttempts = 4;

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
            && (
                (x.TrangThaiEmail == TrangThaiEmail.ChoGui && (!x.LanGuiTiepTheo.HasValue || x.LanGuiTiepTheo <= now))
                || (x.TrangThaiEmail == TrangThaiEmail.DangGui && x.KhoaXuLyDen < now)
            ))
            .OrderBy(x => x.Id).Take(10).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var item in candidates) {
            cancellationToken.ThrowIfCancellationRequested();
            var lease = clock.UtcNow.AddMinutes(2);
            var claimed = await db.ThongBaoHoaDons.Where(x => x.Id == item.Id && publishedIds.Contains(x.HoaDonId)
                && (
                    (x.TrangThaiEmail == TrangThaiEmail.ChoGui && (!x.LanGuiTiepTheo.HasValue || x.LanGuiTiepTheo <= now))
                    || (x.TrangThaiEmail == TrangThaiEmail.DangGui && x.KhoaXuLyDen < now)
                ))
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.TrangThaiEmail, "DANG_GUI").SetProperty(x => x.KhoaXuLyDen, lease)
                    .SetProperty(x => x.SoLanGui, x => x.SoLanGui + 1), cancellationToken);
            if (claimed == 0) continue;

            var soLanGui = item.SoLanGui + 1;
            var nextRetry = GetNextRetry(soLanGui, now);

            try {
                await sender.SendIssuedAsync(item.EmailNhan, item.TieuDe, item.NoiDung, item.DuongDan);
                await db.ThongBaoHoaDons.Where(x => x.Id == item.Id && x.TrangThaiEmail == "DANG_GUI" && x.KhoaXuLyDen == lease)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.TrangThaiEmail, "DA_GUI").SetProperty(x => x.NgayGuiThanhCong, clock.UtcNow)
                        .SetProperty(x => x.KhoaXuLyDen, (DateTime?)null).SetProperty(x => x.LoiGanNhat, (string?)null), cancellationToken);
                await UpsertEmailGuiLog(item, soLanGui, TrangThaiEmail.DaGui, now, null, null, cancellationToken);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                var nextStatus = soLanGui >= MaxAttempts ? TrangThaiEmail.ThatBai : TrangThaiEmail.ChoGui;
                var nextRetryTime = nextStatus == TrangThaiEmail.ThatBai ? (DateTime?)null : nextRetry;
                await db.ThongBaoHoaDons.Where(x => x.Id == item.Id && x.TrangThaiEmail == "DANG_GUI" && x.KhoaXuLyDen == lease)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.TrangThaiEmail, nextStatus)
                        .SetProperty(x => x.KhoaXuLyDen, (DateTime?)null)
                        .SetProperty(x => x.LoiGanNhat, "Không gửi được email hóa đơn.")
                        .SetProperty(x => x.LanGuiTiepTheo, nextRetryTime), cancellationToken);
                await UpsertEmailGuiLog(item, soLanGui, nextStatus, now, nextRetryTime, "Không gửi được email hóa đơn.", cancellationToken);
            }
        }
        return sent;
    }

    public static DateTime? GetNextRetry(int soLanGui, DateTime now) => soLanGui switch
    {
        1 => now.AddMinutes(5),
        2 => now.AddMinutes(10),
        3 => now.AddMinutes(15),
        _ => null
    };

    private async Task UpsertEmailGuiLog(ThongBaoHoaDon item, int soLanThu, string trangThai, DateTime ngayGui, DateTime? ngayThuTiep, string? loiGanNhat, CancellationToken cancellationToken)
    {
        var existing = await db.EmailGuiLogs.FirstOrDefaultAsync(x =>
            x.HoaDonId == item.HoaDonId && x.NguoiNhanId == item.NguoiNhanId && x.LoaiThongBao == item.LoaiThongBao, cancellationToken);
        if (existing != null) {
            existing.TrangThai = trangThai;
            existing.SoLanThu = soLanThu;
            existing.NgayGui = ngayGui;
            existing.NgayThuTiep = ngayThuTiep;
            existing.LoiGanNhat = loiGanNhat;
        } else {
            db.EmailGuiLogs.Add(new EmailGuiLog {
                HoaDonId = item.HoaDonId,
                LoaiThongBao = item.LoaiThongBao,
                NguoiNhanId = item.NguoiNhanId,
                EmailNguoiNhan = item.EmailNhan,
                TrangThai = trangThai,
                SoLanThu = soLanThu,
                NgayGui = ngayGui,
                NgayThuTiep = ngayThuTiep,
                LoiGanNhat = loiGanNhat,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
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
