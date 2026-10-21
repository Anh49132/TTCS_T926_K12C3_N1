using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;

namespace QL_PhongTro.Services;

public sealed class TinDangExpirationService(AppDbContext db, ITimeProvider clock)
{
    public Task<int> ExpireAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        return db.TinDangs
            .Where(post => post.TrangThai == "DANG_HIEN_THI"
                && post.NgayHetHan != null && post.NgayHetHan < now)
            .ExecuteUpdateAsync(update => update.SetProperty(post => post.TrangThai, "TAM_AN"), cancellationToken);
    }
}

public sealed class TinDangExpirationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<TinDangExpirationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<TinDangExpirationService>()
                    .ExpireAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không thể tự động tạm ẩn tin đăng đã hết hạn.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
