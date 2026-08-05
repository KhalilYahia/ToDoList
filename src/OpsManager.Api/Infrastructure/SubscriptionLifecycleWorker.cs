using OpsManager.Service.Platform;

namespace OpsManager.Api.Infrastructure;

public sealed class SubscriptionLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionLifecycleWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> LogProcessed =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(SubscriptionLifecycleWorker)),
            "Subscription lifecycle updated {Count} subscriptions.");

    private static readonly Action<ILogger, Exception?> LogFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(SubscriptionLifecycleWorker)),
            "Subscription lifecycle processing failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ISubscriptionLifecycleService service =
                    scope.ServiceProvider.GetRequiredService<ISubscriptionLifecycleService>();
                int count = await service.ProcessExpirationsAsync(stoppingToken);
                LogProcessed(logger, count, null);
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
