using OpsManager.Service.Infrastructure;

namespace OpsManager.Api.Infrastructure;

public sealed class OperationalNotificationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OperationalNotificationWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> LogDispatched =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(OperationalNotificationWorker)),
            "Operational notification sweep created {Count} notifications.");

    private static readonly Action<ILogger, Exception?> LogFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(OperationalNotificationWorker)),
            "Operational notification sweep failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IOperationalNotificationDispatchService service =
                    scope.ServiceProvider.GetRequiredService<IOperationalNotificationDispatchService>();
                int count = await service.DispatchAsync(stoppingToken);
                LogDispatched(logger, count, null);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogFailure(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
