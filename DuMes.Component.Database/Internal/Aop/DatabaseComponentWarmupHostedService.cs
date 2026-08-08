using DuMes.Component.Database.Internal.Postgres;
using DuMes.Component.Database.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace DuMes.Component.Database.Internal.Aop;

/// <summary>
///     宿主启动时：注入 SQL AOP 所用 <see cref="ILogger" />，并自动建库 / 架构 / pgvector 扩展。
///     CodeFirst 请业务侧调用 <c>DatabaseCodeFirst.InitTables(assembly)</c>。
/// </summary>
internal sealed class DatabaseComponentWarmupHostedService : IHostedService
{
    private readonly DatabaseComponentOptions _options;
    private readonly ILogger _logger;

    public DatabaseComponentWarmupHostedService(
        DatabaseComponentOptions options,
        ISqlSugarClient sugarClient,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sugarClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _options = options;
        DatabaseSqlLogger.Initialize(loggerFactory);
        _logger = loggerFactory.CreateLogger("DuMes.Component.Database.Bootstrap");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        DatabaseBootstrapper.EnsureCreated(_options, _logger);
        PostgresVectorBootstrapper.Ensure(_options, _logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
