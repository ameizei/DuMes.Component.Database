using DuMes.Component.Database.Audit;
using DuMes.Component.Database.CodeFirst;
using DuMes.Component.Database.Internal.Config;
using DuMes.Component.Database.Internal.Postgres;
using DuMes.Component.Database.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using SqlSugar.IOC;

namespace DuMes.Component.Database.Internal.Aop;

/// <summary>
///     建库 / Schema / pgvector / <c>log_audit</c>。供公开
///     <c>EnsureComponentDatabaseAsync</c> 与 <see cref="DatabaseComponentWarmupHostedService"/> 共用；幂等。
/// </summary>
internal static class DatabaseComponentWarmup
{
    private static int _completed;

    /// <summary>
    ///     执行就绪步骤。已成功执行过则直接返回（可被显式 API 与 HostedService 各调用一次）。
    /// </summary>
    public static void EnsureReady(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;

        try
        {
            var options = services.GetRequiredService<DatabaseComponentOptions>();
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            // 触发 SqlSugar.IOC 作用域就绪
            _ = services.GetRequiredService<ISqlSugarClient>();

            DatabaseSqlLogger.Initialize(loggerFactory);
            var logger = loggerFactory.CreateLogger("DuMes.Component.Database.Bootstrap");

            DatabaseBootstrapper.EnsureCreated(options, logger);
            PostgresVectorBootstrapper.Ensure(options, logger);
            EnsureAuditTable(options, logger);
        }
        catch
        {
            Interlocked.Exchange(ref _completed, 0);
            throw;
        }
    }

    private static void EnsureAuditTable(DatabaseComponentOptions options, ILogger logger)
    {
        var auditConfigIds = options.ResolveAuditConfigIds();
        if (auditConfigIds.Count == 0)
        {
            logger.LogWarning("未解析到审计表 ConfigId，跳过 log_audit 建表");
            return;
        }

        foreach (var configId in auditConfigIds)
        {
            var resolved = DatabaseConfigIdResolver.Resolve(configId);
            var db = DbScoped.SugarScope.GetConnection(resolved);
            DatabaseCodeFirst.InitTables(db, typeof(DatabaseAuditRecord));
            logger.LogInformation("统一审计表 log_audit 已就绪 ConfigId={ConfigId}", resolved);
        }
    }
}
