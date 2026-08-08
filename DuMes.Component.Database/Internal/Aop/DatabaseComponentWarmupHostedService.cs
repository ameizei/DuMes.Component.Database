using DuMes.Component.Database.Audit;
using DuMes.Component.Database.CodeFirst;
using DuMes.Component.Database.Internal.Config;
using DuMes.Component.Database.Internal.Postgres;
using DuMes.Component.Database.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;
using SqlSugar.IOC;

namespace DuMes.Component.Database.Internal.Aop;

/// <summary>
///     宿主启动时：注入 SQL AOP 所用 <see cref="ILogger" />，自动建库 / 架构 / pgvector，
///     并在 <see cref="DatabaseComponentOptions.AuditConfigIds"/> 指定的连接上建统一审计表。
///     业务实体 CodeFirst 仍由业务侧 <c>DatabaseCodeFirst.InitTables(assembly)</c>。
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
        EnsureAuditTable();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void EnsureAuditTable()
    {
        var auditConfigIds = _options.ResolveAuditConfigIds();
        if (auditConfigIds.Count == 0)
        {
            _logger.LogWarning("未解析到审计表 ConfigId，跳过 log_audit 建表");
            return;
        }

        foreach (var configId in auditConfigIds)
        {
            var resolved = DatabaseConfigIdResolver.Resolve(configId);
            var db = DbScoped.SugarScope.GetConnection(resolved);
            DatabaseCodeFirst.InitTables(db, typeof(DatabaseAuditRecord));
            _logger.LogInformation("统一审计表 log_audit 已就绪 ConfigId={ConfigId}", resolved);
        }
    }
}
