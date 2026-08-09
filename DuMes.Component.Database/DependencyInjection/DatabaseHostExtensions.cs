using DuMes.Component.Database.Internal.Aop;
using Microsoft.Extensions.Hosting;

namespace DuMes.Component.Database.DependencyInjection;

/// <summary>
///     宿主侧扩展：在业务建表 / 模块初始化之前确保库与基础设施就绪。
/// </summary>
public static class DatabaseHostExtensions
{
    /// <summary>
    ///     确保数据库组件就绪：主库 <c>CreateDatabase</c>、Schema、pgvector、
    ///     <c>AuditConfigIds</c> 上的 <c>log_audit</c>。
    ///     须在业务 <c>DatabaseCodeFirst.InitTables</c> / 模块 <c>InitializeAsync</c> 之前调用。幂等；
    ///     与内部 <c>IHostedService</c> Warmup 共用实现，先调用本方法后 <c>Run</c> 不会重复执行。
    /// </summary>
    /// <param name="services">已 <c>Build</c> 的服务提供者（如 <c>app.Services</c>）。</param>
    /// <param name="cancellationToken">取消标记（当前为同步就绪，保留参数以便调用方统一签名）。</param>
    public static Task EnsureComponentDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        cancellationToken.ThrowIfCancellationRequested();

        DatabaseComponentWarmup.EnsureReady(services);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     <see cref="EnsureComponentDatabaseAsync(IServiceProvider, CancellationToken)"/> 的 <see cref="IHost"/> 重载。
    /// </summary>
    public static Task EnsureComponentDatabaseAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.Services.EnsureComponentDatabaseAsync(cancellationToken);
    }
}
