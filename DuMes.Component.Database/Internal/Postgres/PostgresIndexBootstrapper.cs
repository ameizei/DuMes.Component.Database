using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using SqlSugar;

namespace DuMes.Component.Database.Internal.Postgres;

/// <summary>
///     为分区表 / 继承表补建 <see cref="SugarIndexAttribute"/> 索引（普通表仍走 SqlSugar CodeFirst）。
/// </summary>
/// <remarks>
///     分区：索引建在父表上（声明式分区会落到各子分区）。
///     继承：索引不随 <c>INHERITS</c> 传播，按实体各自建在对应表上（读取特性时 <c>inherit: false</c>）。
/// </remarks>
internal static class PostgresIndexBootstrapper
{
    private static readonly Regex IdentRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void Ensure(ISqlSugarClient db, Type entityType)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entityType);

        var indexes = entityType.GetCustomAttributes<SugarIndexAttribute>(inherit: false).ToList();
        if (indexes.Count == 0)
            return;

        var entity = db.EntityMaintenance.GetEntityInfo(entityType);
        var tableName = entity.DbTableName;
        if (string.IsNullOrWhiteSpace(tableName) || !IdentRegex.IsMatch(tableName))
            throw new InvalidOperationException($"实体 {entityType.Name} 的表名非法：{tableName}");

        var columnsByProperty = entity.Columns
            .Where(static c => !c.IsIgnore && !string.IsNullOrWhiteSpace(c.PropertyName) && !string.IsNullOrWhiteSpace(c.DbColumnName))
            .GroupBy(static c => c.PropertyName, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.First().DbColumnName, StringComparer.Ordinal);

        foreach (var index in indexes)
            CreateIndex(db, entityType.Name, tableName, index, columnsByProperty);
    }

    private static void CreateIndex(
        ISqlSugarClient db,
        string entityName,
        string tableName,
        SugarIndexAttribute index,
        IReadOnlyDictionary<string, string> columnsByProperty)
    {
        if (index.IndexFields == null || index.IndexFields.Count == 0)
            throw new InvalidOperationException($"实体 {entityName} 的索引 {index.IndexName} 未声明字段。");

        var indexName = ResolveIndexName(index.IndexName, tableName);
        if (string.IsNullOrWhiteSpace(indexName) || !IdentRegex.IsMatch(indexName))
            throw new InvalidOperationException($"实体 {entityName} 的索引名非法：{index.IndexName} → {indexName}");

        var sb = new StringBuilder();
        sb.Append(index.IsUnique ? "CREATE UNIQUE INDEX IF NOT EXISTS " : "CREATE INDEX IF NOT EXISTS ");
        sb.Append(indexName).Append(" ON ").Append(tableName).Append(" (");

        var first = true;
        foreach (var (propertyName, order) in index.IndexFields)
        {
            if (!columnsByProperty.TryGetValue(propertyName, out var columnName))
                throw new InvalidOperationException(
                    $"实体 {entityName} 的索引 {indexName} 引用了未知属性 {propertyName}。");

            if (!IdentRegex.IsMatch(columnName))
                throw new InvalidOperationException($"实体 {entityName} 索引列名非法：{columnName}");

            if (!first)
                sb.Append(", ");
            first = false;

            sb.Append(columnName);
            sb.Append(order == OrderByType.Desc ? " DESC" : " ASC");
        }

        sb.Append(");");
        db.Ado.ExecuteCommand(sb.ToString());
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
