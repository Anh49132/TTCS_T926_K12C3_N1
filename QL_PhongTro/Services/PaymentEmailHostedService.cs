namespace QL_PhongTro.Services;
public sealed class PaymentEmailHostedService(IServiceScopeFactory scopes,ILogger<PaymentEmailHostedService> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try{await using var scope=scopes.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<PaymentEmailDispatcher>().DispatchAsync(stoppingToken);}
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception){logger.LogWarning("Chưa xử lý được hàng đợi email thanh toán; sẽ thử lại.");}
            await Task.Delay(TimeSpan.FromSeconds(30),stoppingToken);
        }
    }
}
