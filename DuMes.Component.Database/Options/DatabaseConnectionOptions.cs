using SqlSugar.IOC;

namespace DuMes.Component.Database.Options;

/// <summary>
///     单条数据库连接（主库或从库项）。多库以 <see cref="ConfigId" /> 区分（SaaS / 多库导航）。
/// </summary>
public sealed class DatabaseConnectionOptions
{
    /// <summary>
    ///     连接标识。同一 <see cref="DatabaseComponentOptions.Connections" />（含从库）内忽略大小写唯一。
    /// </summary>
    public string ConfigId { get; set; }

    /// <summary>连接字符串；空则校验失败。</summary>
    public string ConnectionString { get; set; }

    /// <summary>
    ///     数据库类型。主库省略时为 <see cref="IocDbType.PostgreSQL" />；
    ///     从库省略时继承所属主库。
    /// </summary>
    public IocDbType? DbType { get; set; }

    /// <summary>
    ///     读写分离从库列表（仅主库项有效）。项须含 <see cref="ConfigId" /> 与 <see cref="ConnectionString" />。
    /// </summary>
    public List<DatabaseConnectionOptions> Slaves { get; set; } = [];
}
