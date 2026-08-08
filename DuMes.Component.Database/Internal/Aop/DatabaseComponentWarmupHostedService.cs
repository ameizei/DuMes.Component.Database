using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuMes.Component.Database.Internal.Aop;

/// <summary>
///     宿主启动时注入 SQL AOP 所用 <see cref="ILogger" />（控制台 Host / Web / Windows Service）。
/// </summary>
internal sealed class DatabaseComponentWarmupHostedService : IHostedService
{
    public DatabaseComponentWarmupHostedService(ILoggerFactory loggerFactory)
    {
        DatabaseSqlLogger.Initialize(loggerFactory);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
