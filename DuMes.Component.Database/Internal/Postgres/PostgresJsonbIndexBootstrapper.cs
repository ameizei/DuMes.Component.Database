using System.Reflection;
using System.Text.RegularExpressions;
using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace DuMes.Component.Database.Internal.Postgres;

/// <summary>
///     按 <see cref="DatabaseJsonbIndexAttribute"/> 创建 jsonb GIN 索引。
/// </summary>
internal static class PostgresJsonbIndexBootstrapper
{
    private static readonly Regex IdentRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void Ensure(ISqlSugarClient db, Type entityType)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entityType);

        if (!PostgresFamily.IsPostgresFamily(db.CurrentConnectionConfig.DbType))
            return;

        var attrs = entityType.GetCustomAttributes<DatabaseJsonbIndexAttribute>(inherit: false).ToList();
        if (attrs.Count == 0)
            return;

        var entity = db.EntityMaintenance.GetEntityInfo(entityType);
        var tableName = entity.DbTableName;
        if (string.IsNullOrWhiteSpace(tableName) || !IdentRegex.IsMatch(tableName))
            throw new InvalidOperationException($"实体 {entityType.Name} 的表名非法：{tableName}");

        var columnsByProperty = entity.Columns
            .Where(static c => !c.IsIgnore && !string.IsNullOrWhiteSpace(c.PropertyName) && !string.IsNullOrWhiteSpace(c.DbColumnName))
            .GroupBy(static c => c.PropertyName, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);

        foreach (var attr in attrs)
            CreateIndex(db, entityType.Name, tableName, attr, columnsByProperty);
    }

    private static void CreateIndex(
        ISqlSugarClient db,
        string entityName,
        string tableName,
        DatabaseJsonbIndexAttribute attr,
        IReadOnlyDictionary<string, EntityColumnInfo> columnsByProperty)
    {
        if (!columnsByProperty.TryGetValue(attr.PropertyName, out var column))
            throw new InvalidOperationException(
                $"实体 {entityName} 的 jsonb 索引 {attr.IndexName} 引用了未知属性 {attr.PropertyName}。");

        if (!IsJsonbColumn(column))
            throw new InvalidOperationException(
                $"实体 {entityName} 的属性 {attr.PropertyName} 不是 jsonb 列（须 IsJson 或 ColumnDataType=jsonb），无法创建 GIN 索引。");

        var columnName = column.DbColumnName;
        if (!IdentRegex.IsMatch(columnName))
            throw new InvalidOperationException($"实体 {entityName} 索引列名非法：{columnName}");

        var indexName = ResolveIndexName(attr.IndexName, tableName);
        if (string.IsNullOrWhiteSpace(indexName) || !IdentRegex.IsMatch(indexName))
            throw new InvalidOperationException($"实体 {entityName} 的索引名非法：{attr.IndexName} → {indexName}");

        var opsSql = attr.Ops switch
        {
            DatabaseJsonbIndexOps.PathOps => "jsonb_path_ops",
            DatabaseJsonbIndexOps.Ops => "jsonb_ops",
            _ => throw new ArgumentOutOfRangeException(nameof(attr), attr.Ops, null)
        };

        // 分区父表：不带 ONLY，PG 会同步到各子分区（与 B-tree [SugarIndex] 补建一致）
        var sql =
            $"CREATE INDEX IF NOT EXISTS {indexName} ON {tableName} USING GIN ({columnName} {opsSql});";
        db.Ado.ExecuteCommand(sql);
    }

    private static bool IsJsonbColumn(EntityColumnInfo column)
    {
        if (column.IsJson)
            return true;

        var dt = (column.DataType ?? string.Empty).Trim();
        return dt.Equals("jsonb", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveIndexName(string rawName, string tableName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return null;

        return rawName
            .Replace("{table}", tableName, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}
