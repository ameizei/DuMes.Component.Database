using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace DuMes.Component.Database.Internal.Aop;

/// <summary>
///     宿主 <c>StartAsync</c> 时的兜底 Warmup（与
///     <c>EnsureComponentDatabaseAsync</c> 共用逻辑、幂等）。
///     Web Host 若需在模块 <c>InitializeAsync</c> 前就绪，应显式调用
///     <c>EnsureComponentDatabaseAsync</c>，不要只依赖本服务（其晚于 <c>Run</c> 之前的业务代码）。
/// </summary>
internal sealed class DatabaseComponentWarmupHostedService : IHostedService
{
    private readonly IServiceProvider _services;

    public DatabaseComponentWarmupHostedService(
        IServiceProvider services,
        ISqlSugarClient sugarClient,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(sugarClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _services = services;
        DatabaseSqlLogger.Initialize(loggerFactory);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        DatabaseComponentWarmup.EnsureReady(_services);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
