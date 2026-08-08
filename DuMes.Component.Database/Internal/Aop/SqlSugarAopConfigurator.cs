using System.Text;
using DuMes.Component.Database.Options;
using DuMes.Component.Database.Serialization;
using DuMes.Component.Serilog.Constants;
using DuMes.Component.Serilog.Logging;
using Microsoft.Extensions.Logging;
using SqlSugar;
using SqlSugar.IOC;

namespace DuMes.Component.Database.Internal.Aop;

/// <summary>
///     为 SqlSugar 多库连接配置序列化、MoreSettings、Ulid/枚举全局映射与 SQL AOP。
/// </summary>
internal static class SqlSugarAopConfigurator
{
    private static readonly DatabaseSerializeService SerializeService = new();

    public static void Configure(SqlSugarClient db, DatabaseComponentOptions options)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var connection in options.Connections)
        {
            var configId = connection.ConfigId.Trim();
            var client = db.GetConnection(configId);
            ApplySerializeService(client);
            ApplyTypeMappings(client);
            ApplyMoreSettings(client, options, connection);
            BindAop(client, options, configId);
        }
    }

    /// <summary>
    ///     IsJson 等走 System.Text.Json（<see cref="DatabaseSerializeService"/>），替代 SqlSugar 默认 Newtonsoft。
    /// </summary>
    private static void ApplySerializeService(ISqlSugarClient client)
    {
        var external = client.CurrentConnectionConfig.ConfigureExternalServices ??= new ConfigureExternalServices();
        external.SerializeService = SerializeService;
    }

    /// <summary>
    ///     全局 EntityService：<see cref="Ulid"/> → <c>UlidTypeConverter</c>；
    ///     枚举 → SqlSugar 自带 <c>EnumToStringConvert</c>（库中存枚举名字符串）。
    /// </summary>
    private static void ApplyTypeMappings(ISqlSugarClient client)
    {
        var external = client.CurrentConnectionConfig.ConfigureExternalServices ??= new ConfigureExternalServices();
        var previous = external.EntityService;
        external.EntityService = (property, column) =>
        {
            previous?.Invoke(property, column);

            // 列上已显式指定转换器时不覆盖
            if (column.SqlParameterDbType != null)
                return;

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (type == typeof(Ulid))
            {
                column.SqlParameterDbType = typeof(SqlSugar.DbConvert.UlidTypeConverter);
                if (string.IsNullOrEmpty(column.DataType))
                    column.DataType = "varchar";
                if (column.Length <= 0)
                    column.Length = 26;
                return;
            }

            if (type.IsEnum)
            {
                column.SqlParameterDbType = typeof(SqlSugar.DbConvert.EnumToStringConvert);
                if (string.IsNullOrEmpty(column.DataType))
                    column.DataType = "varchar";
                if (column.Length <= 0)
                    column.Length = 64;
            }
        };
    }

    private static void ApplyMoreSettings(ISqlSugarClient client, DatabaseComponentOptions options, DatabaseConnectionOptions connection)
    {
        var dbType = DatabaseComponentOptions.ResolveDbType(connection);
        if (dbType != IocDbType.PostgreSQL)
            return;

        client.CurrentConnectionConfig.MoreSettings ??= new ConnMoreSettings();
        client.CurrentConnectionConfig.MoreSettings.PgSqlIsAutoToLower = options.PgSqlIsAutoToLower;
        client.CurrentConnectionConfig.MoreSettings.PgSqlIsAutoToLowerCodeFirst = options.PgSqlIsAutoToLower;
    }

    private static void BindAop(ISqlSugarClient client, DatabaseComponentOptions options, string configId)
    {
        client.Aop.OnLogExecuting = (sql, parameters) =>
        {
            var logger = DatabaseSqlLogger.Logger;
            if (!logger.IsEnabled(LogLevel.Debug))
                return;

            var nativeSql = ToNativeSql(client, sql, parameters);
            logger.LogDebug("SQL ConfigId={ConfigId} {Sql}", configId, nativeSql);
        };

        client.Aop.OnLogExecuted = (sql, parameters) =>
        {
            var elapsed = client.Ado.SqlExecutionTime;
            if (elapsed.TotalSeconds < options.SlowSqlSeconds)
                return;

            var nativeSql = ToNativeSql(client, sql, parameters);
            var ms = (long)elapsed.TotalMilliseconds;
            DatabaseSqlLogger.Logger.WriteWarning(
                "sql_slow",
                "慢SQL ConfigId={ConfigId} 耗时={ElapsedMs}ms 阈值={ThresholdSeconds}s SQL={Sql}",
                null,
                LogWriteTarget.File,
                configId,
                ms,
                options.SlowSqlSeconds,
                nativeSql);
        };

        client.Aop.OnError = exception =>
        {
            var sql = exception.Sql;
            var parameterSummary = SummarizeParameters(exception.Parametres as SugarParameter[]);
            DatabaseSqlLogger.Logger.WriteError(
                exception,
                "sql_error",
                "SQL错误 ConfigId={ConfigId} SQL={Sql} Params={Params}",
                null,
                LogWriteTarget.File,
                configId,
                sql,
                parameterSummary);
        };
    }

    private static string ToNativeSql(ISqlSugarClient client, string sql, SugarParameter[] parameters)
    {
        try
        {
            return UtilMethods.GetSqlString(client.CurrentConnectionConfig.DbType, sql, parameters);
        }
        catch
        {
            return sql;
        }
    }

    private static string SummarizeParameters(SugarParameter[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
                sb.Append(", ");

            var p = parameters[i];
            sb.Append(p.ParameterName);
            sb.Append('=');
            sb.Append(p.Value);
        }

        return sb.ToString();
    }
}
