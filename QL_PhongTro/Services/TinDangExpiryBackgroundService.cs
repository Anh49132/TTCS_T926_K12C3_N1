using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;

namespace QL_PhongTro;

internal sealed class TinDangExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ITimeProvider clock,
    ILogger<TinDangExpiryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ScanAsync(stoppingToken);

        using var timer = new PeriodicTimer(ScanInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ScanAsync(stoppingToken);
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var expiryService = scope.ServiceProvider.GetRequiredService<TinDangExpiryService>();
            var count = await expiryService.ScanExpiredAsync(clock.UtcNow, cancellationToken);
            if (count > 0)
                logger.LogInformation("Automatically hid {Count} expired rental listings.", count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is DbUpdateException or DbException)
        {
            logger.LogError(error, "Failed to scan and hide expired rental listings.");
        }
    }
}
