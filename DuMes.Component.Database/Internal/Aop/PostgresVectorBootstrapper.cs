using System.Data;
using DuMes.Component.Database.Options;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector.Npgsql;
using SqlSugar;
using SqlSugar.IOC;

namespace DuMes.Component.Database.Internal.Aop;

/// <summary>
///     注册 Npgsql 的 pgvector 类型映射，并在 PG 库上 <c>CREATE EXTENSION IF NOT EXISTS vector</c>。
/// </summary>
internal static class PostgresVectorBootstrapper
{
    private static readonly object Gate = new();
    private static bool _mapperRegistered;

    public static void Ensure(DatabaseComponentOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        RegisterMapperOnce(logger, log: true);

        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var connection in options.Connections)
        {
            var dbType = DatabaseComponentOptions.ResolveDbType(connection);
            if (!IsPostgresFamily(dbType))
                continue;

            var key = BuildDatabaseKey(connection, dbType);
            if (!done.Add(key))
                continue;

            var configId = connection.ConfigId.Trim();
            var client = DbScoped.SugarScope.GetConnection(configId);
            TryCreateExtension(client, configId, logger);
        }
    }

    /// <summary>供 AOP 配置阶段尽早调用（可无 logger）。</summary>
    public static void RegisterMapper() => RegisterMapperOnce(logger: null, log: false);

    private static void RegisterMapperOnce(ILogger logger, bool log)
    {
        lock (Gate)
        {
            if (_mapperRegistered)
                return;

#pragma warning disable CS0618 // SqlSugar 走连接串建连，暂用全局映射；见 Npgsql 7+ 说明
            NpgsqlConnection.GlobalTypeMapper.UseVector();
#pragma warning restore CS0618
            _mapperRegistered = true;
            if (log)
                logger?.LogDebug("已注册 Npgsql pgvector 类型映射（GlobalTypeMapper.UseVector）");
        }
    }

    private static void TryCreateExtension(ISqlSugarClient client, string configId, ILogger logger)
    {
        try
        {
            client.Ado.ExecuteCommand("CREATE EXTENSION IF NOT EXISTS vector");
            ReloadNpgsqlTypes(client);
            logger.LogInformation("已确保 pgvector 扩展 ConfigId={ConfigId}", configId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "未能启用 pgvector 扩展 ConfigId={ConfigId}。请确认数据库已安装 pgvector，且角色有 CREATE EXTENSION 权限。",
                configId);
        }
    }

    private static void ReloadNpgsqlTypes(ISqlSugarClient client)
    {
        var conn = client.Ado.Connection;
        if (conn is not NpgsqlConnection npgsql)
            return;

        var shouldClose = false;
        if (npgsql.State != ConnectionState.Open)
        {
            npgsql.Open();
            shouldClose = true;
        }

        try
        {
            npgsql.ReloadTypes();
        }
        finally
        {
            if (shouldClose)
                npgsql.Close();
        }
    }

    private static bool IsPostgresFamily(IocDbType dbType)
    {
        return dbType is IocDbType.PostgreSQL
            or IocDbType.Kdbndp
            or IocDbType.OpenGauss
            or IocDbType.HG
            or IocDbType.GaussDB
            or IocDbType.GaussDBNative
            or IocDbType.Vastbase
            or IocDbType.PolarDB
            or IocDbType.TDSQLForPGODBC;
    }

    private static string BuildDatabaseKey(DatabaseConnectionOptions connection, IocDbType dbType)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            return $"{dbType}|{b.Host}|{b.Port}|{b.Database}";
        }
        catch
        {
            return $"{dbType}|{connection.ConnectionString}";
        }
    }
}
