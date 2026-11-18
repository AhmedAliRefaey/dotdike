using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<SyncWorker>();
await builder.Build().RunAsync();

// Disabled until durable lease and complete-snapshot publishing are implemented.
sealed class SyncWorker(ILogger<SyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogWarning("Synchronization is disabled: provider adapters and durable job processor are not configured.");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
