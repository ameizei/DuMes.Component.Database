using SqlSugar.IOC;

namespace DuMes.Component.Database.Internal;

/// <summary>
///     PostgreSQL 及兼容库（人大金仓 / OpenGauss / GaussDB 等）判定，供建库、向量、分区、继承共用。
/// </summary>
internal static class PostgresFamily
{
    public static readonly HashSet<SqlSugar.DbType> SupportedDbTypes =
    [
        SqlSugar.DbType.PostgreSQL,
        SqlSugar.DbType.OpenGauss,
        SqlSugar.DbType.GaussDB,
        SqlSugar.DbType.GaussDBNative,
        SqlSugar.DbType.HG,
        SqlSugar.DbType.Kdbndp,
        SqlSugar.DbType.Vastbase,
        SqlSugar.DbType.PolarDB,
        SqlSugar.DbType.TDSQLForPGODBC
    ];

    public static bool IsPostgresFamily(IocDbType dbType)
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

    public static bool IsPostgresFamily(SqlSugar.DbType dbType) => SupportedDbTypes.Contains(dbType);
}
