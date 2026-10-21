namespace QL_PhongTro.Services;

public sealed class RoomImageCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<RoomImageCleanupHostedService> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = configuration.GetValue("RoomImageCleanupMinutes", 10);
        var interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<RoomImageDeletionService>();
                var result = await service.RetryPendingDeletesAsync(cancellationToken: stoppingToken);
                if (result.Deleted > 0 || result.Failed > 0)
                    logger.LogInformation("Room image cleanup retry finished. Deleted: {Deleted}; failed: {Failed}", result.Deleted, result.Failed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Room image cleanup retry failed.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
