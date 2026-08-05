using OpsManager.Service.Tasks;

namespace OpsManager.Api.Infrastructure;

public sealed class SchedulerWorkerOptions
{
    public const string SectionName = "Scheduler";
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public int GenerationHorizonDays { get; set; } = 7;
}

public sealed class TaskOccurrenceGenerationWorker(
    IServiceScopeFactory scopeFactory,
    SchedulerWorkerOptions options,
    ILogger<TaskOccurrenceGenerationWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> LogGenerated =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(TaskOccurrenceGenerationWorker)),
            "Task schedule sweep created {Count} occurrences.");

    private static readonly Action<ILogger, Exception?> LogFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(TaskOccurrenceGenerationWorker)),
            "Task schedule sweep failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        using PeriodicTimer timer = new(TimeSpan.FromMinutes(Math.Max(1, options.IntervalMinutes)));
        do
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ITaskOccurrenceGeneratorService generator =
                    scope.ServiceProvider.GetRequiredService<ITaskOccurrenceGeneratorService>();
                int count = await generator.GenerateAllAsync(stoppingToken);
                LogGenerated(logger, count, null);
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
