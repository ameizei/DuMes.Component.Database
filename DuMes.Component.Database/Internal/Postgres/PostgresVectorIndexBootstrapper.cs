using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace DuMes.Component.Database.Internal.Postgres;

/// <summary>
///     按 <see cref="DatabaseVectorIndexAttribute"/> 创建 pgvector HNSW / IVFFlat 索引。
/// </summary>
internal static class PostgresVectorIndexBootstrapper
{
    private static readonly Regex IdentRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void Ensure(ISqlSugarClient db, Type entityType)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entityType);

        if (!PostgresFamily.IsPostgresFamily(db.CurrentConnectionConfig.DbType))
            return;

        var attrs = entityType.GetCustomAttributes<DatabaseVectorIndexAttribute>(inherit: false).ToList();
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
            CreateIndex(db, entityType, tableName, attr, columnsByProperty);
    }

    private static void CreateIndex(
        ISqlSugarClient db,
        Type entityType,
        string tableName,
        DatabaseVectorIndexAttribute attr,
        IReadOnlyDictionary<string, EntityColumnInfo> columnsByProperty)
    {
        var entityName = entityType.Name;
        if (!columnsByProperty.TryGetValue(attr.PropertyName, out var column))
            throw new InvalidOperationException(
                $"实体 {entityName} 的向量索引 {attr.IndexName} 引用了未知属性 {attr.PropertyName}。");

        if (!IsVectorColumn(entityType, attr.PropertyName, column))
            throw new InvalidOperationException(
                $"实体 {entityName} 的属性 {attr.PropertyName} 不是 vector 列（须 [DatabaseVector] / [DatabaseCoordinate]），无法创建近邻索引。");

        var columnName = column.DbColumnName;
        if (!IdentRegex.IsMatch(columnName))
            throw new InvalidOperationException($"实体 {entityName} 索引列名非法：{columnName}");

        var indexName = ResolveIndexName(attr.IndexName, tableName);
        if (string.IsNullOrWhiteSpace(indexName) || !IdentRegex.IsMatch(indexName))
            throw new InvalidOperationException($"实体 {entityName} 的索引名非法：{attr.IndexName} → {indexName}");

        if (attr.Lists < 0)
            throw new InvalidOperationException($"实体 {entityName} 的向量索引 {indexName}：Lists 须 >= 0。");
        if (attr.M < 0)
            throw new InvalidOperationException($"实体 {entityName} 的向量索引 {indexName}：M 须 >= 0。");
        if (attr.EfConstruction < 0)
            throw new InvalidOperationException($"实体 {entityName} 的向量索引 {indexName}：EfConstruction 须 >= 0。");

        var methodSql = attr.Method switch
        {
            DatabaseVectorIndexMethod.Hnsw => "hnsw",
            DatabaseVectorIndexMethod.Ivfflat => "ivfflat",
            _ => throw new ArgumentOutOfRangeException(nameof(attr), attr.Method, null)
        };

        var opsSql = attr.Ops switch
        {
            DatabaseVectorIndexOps.L2 => "vector_l2_ops",
            DatabaseVectorIndexOps.InnerProduct => "vector_ip_ops",
            DatabaseVectorIndexOps.Cosine => "vector_cosine_ops",
            _ => throw new ArgumentOutOfRangeException(nameof(attr), attr.Ops, null)
        };

        var sb = new StringBuilder();
        sb.Append("CREATE INDEX IF NOT EXISTS ").Append(indexName)
            .Append(" ON ").Append(tableName)
            .Append(" USING ").Append(methodSql)
            .Append(" (").Append(columnName).Append(' ').Append(opsSql).Append(')');

        AppendWithClause(sb, attr);
        sb.Append(';');
        db.Ado.ExecuteCommand(sb.ToString());
    }

    private static void AppendWithClause(StringBuilder sb, DatabaseVectorIndexAttribute attr)
    {
        var parts = new List<string>();

        if (attr.Method == DatabaseVectorIndexMethod.Ivfflat && attr.Lists > 0)
            parts.Add($"lists = {attr.Lists}");

        if (attr.Method == DatabaseVectorIndexMethod.Hnsw)
        {
            if (attr.M > 0)
                parts.Add($"m = {attr.M}");
            if (attr.EfConstruction > 0)
                parts.Add($"ef_construction = {attr.EfConstruction}");
        }

        if (parts.Count == 0)
            return;

        sb.Append(" WITH (").Append(string.Join(", ", parts)).Append(')');
    }

    private static bool IsVectorColumn(Type entityType, string propertyName, EntityColumnInfo column)
    {
        var dt = (column.DataType ?? string.Empty).Trim();
        if (dt.StartsWith("vector", StringComparison.OrdinalIgnoreCase))
            return true;

        var prop = entityType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (prop == null)
            return false;

        return prop.GetCustomAttribute<DatabaseVectorAttribute>(inherit: true) != null
            || prop.GetCustomAttribute<DatabaseCoordinateAttribute>(inherit: true) != null;
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
