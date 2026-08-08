using System.Text.RegularExpressions;
using DuMes.Component.Database.Options;
using Microsoft.Extensions.Logging;
using Npgsql;
using SqlSugar;
using SqlSugar.IOC;

namespace DuMes.Component.Database.Internal.Aop;

/// <summary>
///     连接初始化：自动建库（<c>DbMaintenance.CreateDatabase</c>）与按库类型建架构。内置开启，不开放配置。
/// </summary>
internal static class DatabaseBootstrapper
{
    private static readonly Regex SchemaNameRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private enum SchemaDialect
    {
        /// <summary>不支持独立架构（如 MySQL 系 schema=库、Sqlite、时序库等）。</summary>
        Unsupported,

        /// <summary><c>CREATE SCHEMA IF NOT EXISTS</c>（PostgreSQL 及兼容库）。</summary>
        Postgres,

        /// <summary>SQL Server：先查 <c>sys.schemas</c> 再 <c>CREATE SCHEMA</c>。</summary>
        SqlServer
    }

    public static void EnsureCreated(DatabaseComponentOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var createdDatabases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var connection in options.Connections)
        {
            var configId = connection.ConfigId.Trim();
            var client = DbScoped.SugarScope.GetConnection(configId);
            var dbType = DatabaseComponentOptions.ResolveDbType(connection);

            TryCreateDatabase(client, connection, configId, createdDatabases, logger);
            TryCreateSchemas(client, connection, configId, dbType, logger);
        }
    }

    private static void TryCreateDatabase(
        ISqlSugarClient client,
        DatabaseConnectionOptions connection,
        string configId,
        HashSet<string> createdDatabases,
        ILogger logger)
    {
        var databaseKey = BuildDatabaseKey(connection);
        if (!createdDatabases.Add(databaseKey))
        {
            logger.LogDebug("跳过建库（同库已处理） ConfigId={ConfigId} Key={DatabaseKey}", configId, databaseKey);
            return;
        }

        logger.LogInformation("自动建库 ConfigId={ConfigId} DbType={DbType} …", configId, DatabaseComponentOptions.ResolveDbType(connection));
        // 不存在则创建，已存在不重复创建（SqlSugar DbMaintenance）；Oracle 等个别库不支持需手动建库
        client.DbMaintenance.CreateDatabase();
        logger.LogInformation("自动建库完成 ConfigId={ConfigId}", configId);
    }

    private static void TryCreateSchemas(
        ISqlSugarClient client,
        DatabaseConnectionOptions connection,
        string configId,
        IocDbType dbType,
        ILogger logger)
    {
        var dialect = ResolveSchemaDialect(dbType);
        if (dialect == SchemaDialect.Unsupported)
        {
            logger.LogDebug("当前库类型不支持独立架构，跳过建架构 ConfigId={ConfigId} DbType={DbType}", configId, dbType);
            return;
        }

        var schemas = ResolveSchemaNames(dbType, connection, configId);
        if (schemas.Count == 0)
        {
            logger.LogDebug("未解析到架构名，跳过建架构 ConfigId={ConfigId} DbType={DbType}", configId, dbType);
            return;
        }

        foreach (var schema in schemas)
        {
            if (!SchemaNameRegex.IsMatch(schema))
                throw new InvalidOperationException(
                    $"配置无效：连接 {configId} 的架构名「{schema}」非法（仅允许字母/数字/下划线，且不以数字开头）。");

            if (IsBuiltinSchema(dbType, schema))
            {
                logger.LogDebug("跳过内置架构 ConfigId={ConfigId} Schema={Schema}", configId, schema);
                continue;
            }

            var sql = BuildCreateSchemaSql(dialect, schema);
            logger.LogInformation("自动建架构 ConfigId={ConfigId} DbType={DbType} Schema={Schema} …", configId, dbType, schema);
            client.Ado.ExecuteCommand(sql);
        }
    }

    /// <summary>
    ///     按 <see cref="IocDbType"/> 选择建架构方言；MySQL 系（schema=数据库）、Sqlite、时序/分析库等返回 Unsupported。
    /// </summary>
    private static SchemaDialect ResolveSchemaDialect(IocDbType dbType)
    {
        return dbType switch
        {
            // PostgreSQL 及兼容：CREATE SCHEMA IF NOT EXISTS
            IocDbType.PostgreSQL
                or IocDbType.Kdbndp
                or IocDbType.OpenGauss
                or IocDbType.HG
                or IocDbType.GaussDB
                or IocDbType.GaussDBNative
                or IocDbType.Vastbase
                or IocDbType.PolarDB
                or IocDbType.TDSQLForPGODBC
                or IocDbType.Dm // 达梦常见兼容该语法
                => SchemaDialect.Postgres,

            IocDbType.SqlServer => SchemaDialect.SqlServer,

            // MySQL 系：schema 即 database，已由 CreateDatabase 覆盖
            // Sqlite / Access / Oracle（架构≈用户）/ 时序·分析·文档库等：不在此自动建架构
            _ => SchemaDialect.Unsupported
        };
    }

    private static string BuildCreateSchemaSql(SchemaDialect dialect, string schema)
    {
        return dialect switch
        {
            SchemaDialect.Postgres => $"CREATE SCHEMA IF NOT EXISTS {schema}",
            SchemaDialect.SqlServer =>
                $"""
                 IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{schema}')
                     EXEC(N'CREATE SCHEMA [{schema}]');
                 """,
            _ => throw new InvalidOperationException($"内部错误：不支持的架构方言 {dialect}。")
        };
    }

    /// <summary>
    ///     PG 系：连接串 <c>searchpath</c> / <c>Search Path</c>；
    ///     SQL Server：无 searchpath 时用 <see cref="DatabaseConnectionOptions.ConfigId"/> 作为架构名。
    /// </summary>
    private static List<string> ResolveSchemaNames(IocDbType dbType, DatabaseConnectionOptions connection, string configId)
    {
        var fromSearchPath = ParseSearchPathSchemas(connection.ConnectionString);
        if (fromSearchPath.Count > 0)
            return fromSearchPath;

        if (ResolveSchemaDialect(dbType) == SchemaDialect.SqlServer)
            return [configId];

        return [];
    }

    private static bool IsBuiltinSchema(IocDbType dbType, string schema)
    {
        if (string.Equals(schema, "public", StringComparison.OrdinalIgnoreCase))
            return true;

        if (dbType == IocDbType.SqlServer
            && (string.Equals(schema, "dbo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(schema, "guest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(schema, "sys", StringComparison.OrdinalIgnoreCase)
                || string.Equals(schema, "INFORMATION_SCHEMA", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static List<string> ParseSearchPathSchemas(string connectionString)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(connectionString))
            return list;

        var searchPath = TryReadSearchPath(connectionString);
        if (string.IsNullOrWhiteSpace(searchPath))
            return list;

        foreach (var part in searchPath.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length == 0)
                continue;

            // 去掉可选双引号
            var name = part.Length >= 2 && part[0] == '"' && part[^1] == '"'
                ? part[1..^1]
                : part;

            if (name.Length == 0)
                continue;

            if (!list.Contains(name, StringComparer.OrdinalIgnoreCase))
                list.Add(name);
        }

        return list;
    }

    private static string TryReadSearchPath(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrWhiteSpace(builder.SearchPath))
                return builder.SearchPath;
        }
        catch
        {
            // 非 Npgsql 连接串：下面做通用键值解析
        }

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = segment[..eq].Trim();
            if (key.Equals("Search Path", StringComparison.OrdinalIgnoreCase)
                || key.Equals("SearchPath", StringComparison.OrdinalIgnoreCase)
                || key.Equals("searchpath", StringComparison.OrdinalIgnoreCase))
                return segment[(eq + 1)..].Trim();
        }

        return null;
    }

    /// <summary>同一物理库只建一次（Host+Port+Database）。</summary>
    private static string BuildDatabaseKey(DatabaseConnectionOptions connection)
    {
        var dbType = DatabaseComponentOptions.ResolveDbType(connection);
        if (ResolveSchemaDialect(dbType) == SchemaDialect.Postgres)
        {
            try
            {
                var b = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
                return $"{dbType}|{b.Host}|{b.Port}|{b.Database}";
            }
            catch
            {
                // fall through
            }
        }

        return $"{dbType}|{connection.ConnectionString}";
    }
}
