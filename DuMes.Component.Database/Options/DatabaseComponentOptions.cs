using SqlSugar.IOC;

namespace DuMes.Component.Database.Options;

/// <summary>
///     数据库组件配置（配置节名 <see cref="SectionName" />）。
/// </summary>
public sealed class DatabaseComponentOptions
{
    /// <summary>配置节名称：<c>Database</c>。</summary>
    public const string SectionName = "Database";

    /// <summary>
    ///     连接列表（至少一个）。每项一个 <see cref="DatabaseConnectionOptions.ConfigId" />，用于多库导航 / SaaS 分库。
    /// </summary>
    public List<DatabaseConnectionOptions> Connections { get; set; } = [];

    /// <summary>
    ///     慢 SQL 阈值（秒）。执行耗时大于等于该值时经 <c>WriteWarning("sql_slow", …)</c> 落盘。须 <c>&gt; 0</c>。默认 <c>1</c>。
    /// </summary>
    public double SlowSqlSeconds { get; set; } = 1;

    /// <summary>校验配置；失败抛出 <see cref="InvalidOperationException" />。</summary>
    public void Validate()
    {
        if (Connections == null || Connections.Count == 0)
            throw new InvalidOperationException($"配置无效：{SectionName}:{nameof(Connections)} 至少需要一个连接。");

        if (SlowSqlSeconds <= 0)
            throw new InvalidOperationException($"配置无效：{SectionName}:{nameof(SlowSqlSeconds)} 必须大于 0。");

        var configIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < Connections.Count; i++)
        {
            var path = $"{SectionName}:{nameof(Connections)}[{i}]";
            ValidateConnection(Connections[i], path, configIds, isSlave: false);

            var slaves = Connections[i].Slaves;
            if (slaves == null || slaves.Count == 0)
                continue;

            for (var s = 0; s < slaves.Count; s++)
                ValidateConnection(slaves[s], $"{path}:{nameof(DatabaseConnectionOptions.Slaves)}[{s}]", configIds, isSlave: true);
        }
    }

    private static void ValidateConnection(DatabaseConnectionOptions connection, string path, HashSet<string> configIds, bool isSlave)
    {
        if (connection == null)
            throw new InvalidOperationException($"配置无效：{path} 不能为空。");

        if (string.IsNullOrWhiteSpace(connection.ConfigId))
            throw new InvalidOperationException($"配置缺失：{path}:{nameof(DatabaseConnectionOptions.ConfigId)} 为必填项。");

        if (string.IsNullOrWhiteSpace(connection.ConnectionString))
            throw new InvalidOperationException($"配置缺失：{path}:{nameof(DatabaseConnectionOptions.ConnectionString)} 为必填项。");

        if (connection.DbType is { } dbType && !Enum.IsDefined(dbType))
            throw new InvalidOperationException($"配置无效：{path}:{nameof(DatabaseConnectionOptions.DbType)} 取值无效。");

        var id = connection.ConfigId.Trim();
        if (!configIds.Add(id))
            throw new InvalidOperationException($"配置无效：{nameof(DatabaseConnectionOptions.ConfigId)} 重复（忽略大小写）：{id}。");

        // 从库不应再嵌套从库
        if (isSlave && connection.Slaves is { Count: > 0 })
            throw new InvalidOperationException($"配置无效：{path} 从库不能再配置 {nameof(DatabaseConnectionOptions.Slaves)}。");
    }

    /// <summary>解析主库 <see cref="IocDbType" />（省略则为 PostgreSQL）。</summary>
    public static IocDbType ResolveDbType(DatabaseConnectionOptions connection)
    {
        return connection.DbType ?? IocDbType.PostgreSQL;
    }

    /// <summary>解析从库 <see cref="IocDbType" />（省略则继承主库）。</summary>
    public static IocDbType ResolveSlaveDbType(DatabaseConnectionOptions master, DatabaseConnectionOptions slave)
    {
        return slave.DbType ?? ResolveDbType(master);
    }
}
