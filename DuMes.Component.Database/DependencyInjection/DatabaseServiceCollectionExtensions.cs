using DuMes.Component.Database.Internal.Aop;
using DuMes.Component.Database.Internal.Config;
using DuMes.Component.Database.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSugar;
using SqlSugar.IOC;

namespace DuMes.Component.Database.DependencyInjection;

/// <summary>
///     服务集合扩展：注册 SqlSugar.IOC 多库 / 读写分离数据库组件（无仓储、无工作单元）。
/// </summary>
public static class DatabaseServiceCollectionExtensions
{
    /// <summary>
    ///     从配置节 <c>Database</c> 注册数据库组件。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置根；读取 <see cref="DatabaseComponentOptions.SectionName" />。</param>
    /// <param name="configureOptions">可选，覆盖配置项。</param>
    public static IServiceCollection AddComponentDatabase(this IServiceCollection services, IConfiguration configuration,
        Action<DatabaseComponentOptions> configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(DatabaseComponentOptions.SectionName);
        if (!section.Exists())
            throw new InvalidOperationException($"配置缺失：{DatabaseComponentOptions.SectionName}");

        var options = section.Get<DatabaseComponentOptions>() ?? new DatabaseComponentOptions();
        configureOptions?.Invoke(options);

        return Register(services, options);
    }

    /// <summary>
    ///     仅用代码配置注册（不读配置节）。
    /// </summary>
    public static IServiceCollection AddComponentDatabase(this IServiceCollection services,
        Action<DatabaseComponentOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new DatabaseComponentOptions();
        configureOptions(options);

        return Register(services, options);
    }

    private static IServiceCollection Register(IServiceCollection services, DatabaseComponentOptions options)
    {
        options.Validate();

        DatabaseConfigIdResolver.SetRegistered(options.Connections.Select(static c => c.ConfigId));

        services.AddSingleton(options);
        // 与已校验单例保持一致（含 configureOptions 回调结果），避免 IOptions<> 与单例分叉
        services.AddSingleton(
            Microsoft.Extensions.Options.Options.Create(options));

        var iocConfigs = BuildIocConfigs(options);
        services.AddSqlSugar(iocConfigs);
        services.ConfigurationSugar(db => SqlSugarAopConfigurator.Configure(db, options));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DatabaseComponentWarmupHostedService>());

        services.AddSingleton<ISqlSugarClient>(sp =>
        {
            DatabaseSqlLogger.Initialize(sp.GetRequiredService<ILoggerFactory>());
            return DbScoped.SugarScope;
        });

        return services;
    }

    private static List<IocConfig> BuildIocConfigs(DatabaseComponentOptions options)
    {
        var list = new List<IocConfig>(options.Connections.Count);

        foreach (var connection in options.Connections)
        {
            var masterDbType = DatabaseComponentOptions.ResolveDbType(connection);
            var ioc = new IocConfig
            {
                ConfigId = connection.ConfigId.Trim(),
                ConnectionString = connection.ConnectionString,
                DbType = masterDbType,
                IsAutoCloseConnection = true
            };

            if (connection.Slaves is { Count: > 0 })
                ioc.SlaveConnectionConfigs = connection.Slaves.Select(slave => new IocConfig
                {
                    ConfigId = slave.ConfigId.Trim(),
                    ConnectionString = slave.ConnectionString,
                    DbType = DatabaseComponentOptions.ResolveSlaveDbType(connection, slave),
                    IsAutoCloseConnection = true
                }).ToList();

            list.Add(ioc);
        }

        return list;
    }
}