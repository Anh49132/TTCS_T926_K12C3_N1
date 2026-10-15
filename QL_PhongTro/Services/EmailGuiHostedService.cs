using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QL_PhongTro.Services;

namespace QL_PhongTro.Services;

public sealed class EmailGuiHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailGuiHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailGuiService>();
                var processed = await emailService.XuLyHangDoiGuiAsync(cancellationToken: stoppingToken);
                if (processed > 0)
                {
                    logger.LogInformation("Đã gửi {Count} email từ hàng đợi", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Lỗi xử lý hàng đợi gửi email");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}