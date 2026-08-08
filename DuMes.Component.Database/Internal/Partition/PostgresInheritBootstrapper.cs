using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace DuMes.Component.Database.Internal.Partition;

/// <summary>
///     PostgreSQL 表继承：确保父表存在、子表 <c>INHERITS</c>，并按实体同步列。
/// </summary>
/// <remarks>
///     见 <see href="https://www.postgresql.org/docs/current/ddl-inherit.html"/>。
///     父表 <c>ADD/DROP COLUMN</c> 会传播到继承子表；子表本地列仅在子表上增删。
/// </remarks>
internal static class PostgresInheritBootstrapper
{
    private static readonly Regex IdentRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void Ensure(ISqlSugarClient db, Type childType)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(childType);

        if (childType.GetCustomAttribute<DatabaseInheritAttribute>(inherit: false) == null)
            return;

        if (childType.GetCustomAttribute<DatabasePartitionAttribute>(inherit: true) != null)
            throw new InvalidOperationException(
                $"实体 {childType.Name} 不能同时标注 {nameof(DatabaseInheritAttribute)} 与 {nameof(DatabasePartitionAttribute)}。");

        if (!PostgresFamily.IsPostgresFamily(db.CurrentConnectionConfig.DbType))
            throw new InvalidOperationException(
                $"实体 {childType.Name} 标注了 {nameof(DatabaseInheritAttribute)}，但当前 DbType={db.CurrentConnectionConfig.DbType} 不支持 PostgreSQL 继承表。");

        var parentType = ResolveParentEntity(childType);
        EnsureParentTable(db, parentType);
        EnsureChildTable(db, childType, parentType);
    }

    /// <summary>
    ///     从基类链解析父实体（第一个同时具备 SugarTable + CodeFirst 的类型）。
    /// </summary>
    public static Type ResolveParentEntity(Type childType)
    {
        ArgumentNullException.ThrowIfNull(childType);

        var current = childType.BaseType;
        while (current != null && current != typeof(object))
        {
            if (current.GetCustomAttribute<SugarTable>(inherit: true) != null
                && current.GetCustomAttribute<CodeFirstAttribute>(inherit: true) != null)
                return current;

            current = current.BaseType;
        }

        throw new InvalidOperationException(
            $"实体 {childType.Name} 标注了 {nameof(DatabaseInheritAttribute)}，但基类链上未找到同时具备 [SugarTable] 与 [CodeFirst] 的父实体（须 C# 继承父实体）。");
    }

    private static void EnsureParentTable(ISqlSugarClient db, Type parentType)
    {
        var entity = db.EntityMaintenance.GetEntityInfo(parentType);
        var tableName = entity.DbTableName;
        if (string.IsNullOrWhiteSpace(tableName) || !IdentRegex.IsMatch(tableName))
            throw new InvalidOperationException($"父实体 {parentType.Name} 的表名非法：{tableName}");

        var columns = entity.Columns.Where(static c => !c.IsIgnore).ToList();
        if (columns.Count == 0)
            throw new InvalidOperationException($"父实体 {parentType.Name} 无可用列。");

        if (db.DbMaintenance.IsAnyTable(tableName, false))
        {
            var kind = GetRelKind(db, tableName);
            if (!string.Equals(kind, "r", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"父表 {tableName} 已存在但不是普通表（relkind={kind}），无法作为继承父表。");

            SyncColumns(db, tableName, columns, protectedNames: null, dropOnlyOutside: null);
            return;
        }

        CreateTable(db, tableName, columns, inheritsParent: null);
    }

    private static void EnsureChildTable(ISqlSugarClient db, Type childType, Type parentType)
    {
        var childEntity = db.EntityMaintenance.GetEntityInfo(childType);
        var childTable = childEntity.DbTableName;
        if (string.IsNullOrWhiteSpace(childTable) || !IdentRegex.IsMatch(childTable))
            throw new InvalidOperationException($"子实体 {childType.Name} 的表名非法：{childTable}");

        var parentEntity = db.EntityMaintenance.GetEntityInfo(parentType);
        var parentTable = parentEntity.DbTableName;
        if (string.IsNullOrWhiteSpace(parentTable) || !IdentRegex.IsMatch(parentTable))
            throw new InvalidOperationException($"父实体 {parentType.Name} 的表名非法：{parentTable}");

        var parentColumnNames = new HashSet<string>(
            parentEntity.Columns
                .Where(static c => !c.IsIgnore && !string.IsNullOrWhiteSpace(c.DbColumnName))
                .Select(static c => c.DbColumnName),
            StringComparer.OrdinalIgnoreCase);

        var localColumns = ResolveLocalColumns(childType, childEntity);
        foreach (var col in localColumns)
        {
            if (parentColumnNames.Contains(col.DbColumnName))
                throw new InvalidOperationException(
                    $"子实体 {childType.Name} 本地列 {col.DbColumnName} 与父表列重名；父列应只定义在父实体上。");
        }

        if (db.DbMaintenance.IsAnyTable(childTable, false))
        {
            EnsureInherits(db, childTable, parentTable);
            // 子表 information_schema 会列出继承列：DROP 时不得动父列
            SyncColumns(db, childTable, localColumns, protectedNames: parentColumnNames, dropOnlyOutside: parentColumnNames);
            return;
        }

        CreateTable(db, childTable, localColumns, inheritsParent: parentTable);
    }

    private static List<EntityColumnInfo> ResolveLocalColumns(Type childType, EntityInfo childEntity)
    {
        var declared = childType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(static p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        return childEntity.Columns
            .Where(static c => !c.IsIgnore && !string.IsNullOrWhiteSpace(c.DbColumnName))
            .Where(c => declared.Contains(c.PropertyName))
            .GroupBy(static c => c.DbColumnName, StringComparer.OrdinalIgnoreCase)
            .Select(static g => g.First())
            .ToList();
    }

    private static void EnsureInherits(ISqlSugarClient db, string childTable, string parentTable)
    {
        var actualParent = db.Ado.GetString(
            """
            SELECT p.relname
            FROM pg_inherits i
            JOIN pg_class c ON c.oid = i.inhrelid
            JOIN pg_class p ON p.oid = i.inhparent
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = @child
              AND n.nspname = current_schema()
            LIMIT 1
            """,
            new SugarParameter("@child", childTable));

        if (string.IsNullOrWhiteSpace(actualParent))
            throw new InvalidOperationException(
                $"表 {childTable} 已存在但不是继承子表（无 pg_inherits）。请手工迁移或删表后重建。");

        if (!string.Equals(actualParent, parentTable, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"表 {childTable} 已继承自 {actualParent}，与实体父表 {parentTable} 不一致。请手工迁移或删表后重建。");
    }

    private static string GetRelKind(ISqlSugarClient db, string tableName)
    {
        return db.Ado.GetString(
            """
            SELECT c.relkind::text
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = @name
              AND n.nspname = current_schema()
            LIMIT 1
            """,
            new SugarParameter("@name", tableName));
    }

    private static void CreateTable(
        ISqlSugarClient db,
        string tableName,
        List<EntityColumnInfo> columns,
        string inheritsParent)
    {
        var sb = new StringBuilder();
        sb.Append("CREATE TABLE ").Append(tableName).Append(" (");

        if (columns.Count > 0)
        {
            sb.AppendLine();
            for (var i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!IdentRegex.IsMatch(col.DbColumnName))
                    throw new InvalidOperationException($"列名非法：{col.DbColumnName}");

                if (i > 0)
                    sb.AppendLine(",");

                sb.Append("  ").Append(col.DbColumnName).Append(' ').Append(PostgresColumnSql.ResolvePgType(col));
                sb.Append(col.IsNullable ? " NULL" : " NOT NULL");
            }

            if (inheritsParent == null)
            {
                var pkNames = columns
                    .Where(static c => c.IsPrimarykey)
                    .Select(static c => c.DbColumnName)
                    .Where(static n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (pkNames.Count > 0)
                {
                    sb.AppendLine(",");
                    sb.Append("  PRIMARY KEY (").Append(string.Join(", ", pkNames)).Append(')');
                }
            }

            sb.AppendLine();
        }

        sb.Append(')');

        if (!string.IsNullOrWhiteSpace(inheritsParent))
            sb.Append(" INHERITS (").Append(inheritsParent).Append(')');

        sb.Append(';');
        db.Ado.ExecuteCommand(sb.ToString());
    }

    /// <summary>
    ///     对比实体列与库列：实体有库无则 ADD；库有实体无则 DROP（跳过 protected / dropOnlyOutside）。
    /// </summary>
    private static void SyncColumns(
        ISqlSugarClient db,
        string tableName,
        List<EntityColumnInfo> entityColumns,
        HashSet<string> protectedNames,
        HashSet<string> dropOnlyOutside)
    {
        var dbColumns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false) ?? [];
        var dbNames = new HashSet<string>(
            dbColumns.Select(static c => c.DbColumnName).Where(static n => !string.IsNullOrWhiteSpace(n)),
            StringComparer.OrdinalIgnoreCase);
        var entityByName = entityColumns
            .Where(static c => !string.IsNullOrWhiteSpace(c.DbColumnName))
            .GroupBy(static c => c.DbColumnName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var col in entityByName.Values)
        {
            if (!IdentRegex.IsMatch(col.DbColumnName))
                throw new InvalidOperationException($"列名非法：{col.DbColumnName}");

            if (dbNames.Contains(col.DbColumnName))
                continue;

            var typeSql = PostgresColumnSql.ResolvePgType(col);
            string sql;
            if (col.IsNullable)
            {
                sql = $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS {col.DbColumnName} {typeSql} NULL;";
            }
            else
            {
                sql =
                    $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS {col.DbColumnName} {typeSql} NOT NULL DEFAULT {PostgresColumnSql.ResolveDefaultLiteral(col)};";
            }

            db.Ado.ExecuteCommand(sql);
        }

        foreach (var dbName in dbNames)
        {
            if (entityByName.ContainsKey(dbName))
                continue;

            if (dropOnlyOutside != null && dropOnlyOutside.Contains(dbName))
                continue;

            if (protectedNames != null && protectedNames.Contains(dbName))
                continue;

            if (!IdentRegex.IsMatch(dbName))
                continue;

            db.Ado.ExecuteCommand($"ALTER TABLE {tableName} DROP COLUMN IF EXISTS {dbName};");
        }
    }
}
