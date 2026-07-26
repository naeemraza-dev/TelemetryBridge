using Microsoft.EntityFrameworkCore;
using TelemetryBridge.Core;
using TelemetryBridge.Persistence;

namespace TelemetryBridge.Sample.Worker;

internal sealed class WorkItemProcessor(
    IDbContextFactory<TelemetryBridgeDbContext> contextFactory,
    ILogger<WorkItemProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureDatabaseAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await DatabaseInitializer.EnsureCreatedAsync(database, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var pending = await database.WorkItems
            .Where(item => item.ProcessedAt == null)
            .OrderBy(item => item.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        TelemetryBridgeDiagnostics.WorkItemsPending.Record(pending.Count);

        foreach (var item in pending)
        {
            var messageContext = new TelemetryMessageContext(item.TraceParent, item.TraceState, item.Baggage);
            using var activity = messageContext.StartConsumerActivity("order.created process");
            activity?.SetTag("messaging.system", "postgresql");
            activity?.SetTag("messaging.operation.type", "process");
            activity?.SetTag("telemetrybridge.workflow.name", "order-processing");
            WorkerLogs.Processed(logger, item.Operation);
            item.ProcessedAt = DateTimeOffset.UtcNow;
            TelemetryBridgeDiagnostics.WorkItemsProcessed.Add(1);
        }

        if (pending.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

internal static partial class WorkerLogs
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Processed durable operation {Operation}")]
    public static partial void Processed(ILogger logger, string operation);
}
