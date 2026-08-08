using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace DuMes.Component.Database.Internal.Partition;

/// <summary>
///     PostgreSQL 原生分区表：创建父表（PARTITION BY RANGE）、预建子分区，
///     并在父表已存在时按实体对比增删列（有数据亦可；ADD/DROP 会级联到子分区）。
/// </summary>
/// <remarks>
///     依据 PostgreSQL 文档：对分区父表执行 ADD COLUMN / DROP COLUMN 会传播到所有分区，
///     且不能只改某个子分区的列结构。见
///     <see href="https://www.postgresql.org/docs/current/ddl-partitioning.html"/> 、
///     <see href="https://www.postgresql.org/docs/current/sql-altertable.html"/>。
/// </remarks>
internal static class PostgresPartitionBootstrapper
{
    private static readonly Regex IdentRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<SqlSugar.DbType> SupportedDbTypes =
    [
        SqlSugar.DbType.PostgreSQL,
        SqlSugar.DbType.OpenGauss,
        SqlSugar.DbType.GaussDB,
        SqlSugar.DbType.HG,
        SqlSugar.DbType.Kdbndp,
        SqlSugar.DbType.Vastbase,
        SqlSugar.DbType.PolarDB,
        SqlSugar.DbType.TDSQLForPGODBC
    ];

    public static void Ensure(ISqlSugarClient db, Type entityType)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entityType);

        var partition = entityType.GetCustomAttribute<DatabasePartitionAttribute>(inherit: true);
        if (partition == null)
            return;

        if (!SupportedDbTypes.Contains(db.CurrentConnectionConfig.DbType))
            throw new InvalidOperationException(
                $"实体 {entityType.Name} 标注了 {nameof(DatabasePartitionAttribute)}，但当前 DbType={db.CurrentConnectionConfig.DbType} 不支持 PostgreSQL 分区表。");

        if (partition.AheadCount < 1)
            throw new InvalidOperationException($"实体 {entityType.Name} 的 AheadCount 须 >= 1。");
        if (partition.PastCount < 0)
            throw new InvalidOperationException($"实体 {entityType.Name} 的 PastCount 须 >= 0。");

        var entity = db.EntityMaintenance.GetEntityInfo(entityType);
        var tableName = entity.DbTableName;
        if (string.IsNullOrWhiteSpace(tableName) || !IdentRegex.IsMatch(tableName))
            throw new InvalidOperationException($"实体 {entityType.Name} 的表名非法：{tableName}");

        var partColumn = ResolvePartitionColumn(entityType, entity);
        var columns = entity.Columns.Where(static c => !c.IsIgnore).ToList();
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {entityType.Name} 无可用列。");

        EnsureParentTable(db, tableName, columns, partColumn);
        EnsureChildPartitions(db, tableName, partColumn.DbColumnName, partition);
    }

    private static EntityColumnInfo ResolvePartitionColumn(Type entityType, EntityInfo entity)
    {
        var props = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static p => p.GetCustomAttribute<DatabasePartitionFieldAttribute>(inherit: true) != null)
            .ToList();

        if (props.Count == 0)
            throw new InvalidOperationException(
                $"实体 {entityType.Name} 标注了分区表，但未找到 {nameof(DatabasePartitionFieldAttribute)} 字段。");
        if (props.Count > 1)
            throw new InvalidOperationException($"实体 {entityType.Name} 只能有一个分区字段。");

        var prop = props[0];
        var under = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        if (under != typeof(DateTime))
            throw new InvalidOperationException(
                $"实体 {entityType.Name} 的分区字段 {prop.Name} 必须是 DateTime / DateTime?。");

        var column = entity.Columns.FirstOrDefault(c => c.PropertyName == prop.Name);
        if (column == null || string.IsNullOrWhiteSpace(column.DbColumnName))
            throw new InvalidOperationException($"实体 {entityType.Name} 分区字段 {prop.Name} 无列映射。");
        if (!IdentRegex.IsMatch(column.DbColumnName))
            throw new InvalidOperationException($"分区列名非法：{column.DbColumnName}");

        return column;
    }

    private static void EnsureParentTable(
        ISqlSugarClient db,
        string tableName,
        List<EntityColumnInfo> columns,
        EntityColumnInfo partColumn)
    {
        if (db.DbMaintenance.IsAnyTable(tableName, false))
        {
            var kind = db.Ado.GetString(
                """
                SELECT c.relkind::text
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relname = @name
                  AND n.nspname = current_schema()
                LIMIT 1
                """,
                new SugarParameter("@name", tableName));

            if (string.Equals(kind, "p", StringComparison.OrdinalIgnoreCase))
            {
                // 有数据也可：对父表 ADD/DROP COLUMN 会自动作用到全部子分区
                SyncColumns(db, tableName, columns, partColumn);
                return;
            }

            throw new InvalidOperationException(
                $"表 {tableName} 已存在且不是 PostgreSQL 分区表（relkind={kind}）。请手工迁移或删表后重建。");
        }

        var sb = new StringBuilder();
        sb.Append("CREATE TABLE ").Append(tableName).AppendLine(" (");

        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (i > 0)
                sb.AppendLine(",");

            var isPartKey = string.Equals(col.DbColumnName, partColumn.DbColumnName, StringComparison.OrdinalIgnoreCase);
            var nullable = col.IsNullable && !isPartKey;
            sb.Append("  ").Append(col.DbColumnName).Append(' ').Append(ResolvePgType(col));
            sb.Append(nullable ? " NULL" : " NOT NULL");
        }

        var pkNames = columns
            .Where(static c => c.IsPrimarykey)
            .Select(static c => c.DbColumnName)
            .Where(static n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (pkNames.Count > 0)
        {
            if (!pkNames.Contains(partColumn.DbColumnName, StringComparer.OrdinalIgnoreCase))
                pkNames.Add(partColumn.DbColumnName);

            sb.AppendLine(",");
            sb.Append("  PRIMARY KEY (").Append(string.Join(", ", pkNames)).Append(')');
        }

        sb.AppendLine();
        sb.Append(") PARTITION BY RANGE (").Append(partColumn.DbColumnName).Append(");");

        db.Ado.ExecuteCommand(sb.ToString());
    }

    /// <summary>
    ///     对比实体列与库列：实体有库无则 ADD；库有实体无则 DROP。
    ///     分区键列禁止删除。新增非空列在有数据时带 DEFAULT，避免 PG 报错。
    /// </summary>
    private static void SyncColumns(
        ISqlSugarClient db,
        string tableName,
        List<EntityColumnInfo> entityColumns,
        EntityColumnInfo partColumn)
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

            var typeSql = ResolvePgType(col);
            var isPartKey = string.Equals(col.DbColumnName, partColumn.DbColumnName, StringComparison.OrdinalIgnoreCase);
            // 有数据时：NOT NULL 必须带 DEFAULT，否则 ADD COLUMN 失败
            string sql;
            if (col.IsNullable || isPartKey)
            {
                sql = $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS {col.DbColumnName} {typeSql} NULL;";
            }
            else
            {
                sql =
                    $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS {col.DbColumnName} {typeSql} NOT NULL DEFAULT {ResolveDefaultLiteral(col)};";
            }

            db.Ado.ExecuteCommand(sql);
        }

        foreach (var dbName in dbNames)
        {
            if (entityByName.ContainsKey(dbName))
                continue;

            if (string.Equals(dbName, partColumn.DbColumnName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"拒绝删除分区键列 {tableName}.{dbName}（实体已移除该属性，但分区表必须保留分区键）。");

            if (!IdentRegex.IsMatch(dbName))
                continue;

            // 父表 DROP COLUMN 会级联到所有子分区；有数据时该列数据一并丢弃
            db.Ado.ExecuteCommand($"ALTER TABLE {tableName} DROP COLUMN IF EXISTS {dbName};");
        }
    }

    private static void EnsureChildPartitions(
        ISqlSugarClient db,
        string tableName,
        string partColumnName,
        DatabasePartitionAttribute partition)
    {
        var now = DateTime.Now;
        for (var offset = -partition.PastCount; offset < partition.AheadCount; offset++)
        {
            var anchor = AddPeriod(now, partition.Grain, offset);
            var (from, to, suffix) = GetPeriodBounds(anchor, partition.Grain);
            if (!IdentRegex.IsMatch(suffix))
                throw new InvalidOperationException($"分区后缀非法：{suffix}");

            var childName = $"{tableName}_{suffix}";
            if (db.DbMaintenance.IsAnyTable(childName, false))
                continue;

            var fromLiteral = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var toLiteral = to.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            var sql =
                $"""
                 CREATE TABLE IF NOT EXISTS {childName}
                     PARTITION OF {tableName}
                     FOR VALUES FROM ('{fromLiteral}') TO ('{toLiteral}');
                 """;
            db.Ado.ExecuteCommand(sql);
        }
    }

    private static DateTime AddPeriod(DateTime value, DatabasePartitionGrain grain, int offset)
    {
        return grain switch
        {
            DatabasePartitionGrain.Year => value.AddYears(offset),
            DatabasePartitionGrain.Quarter => value.AddMonths(offset * 3),
            DatabasePartitionGrain.Month => value.AddMonths(offset),
            DatabasePartitionGrain.Day => value.AddDays(offset),
            _ => throw new ArgumentOutOfRangeException(nameof(grain), grain, null)
        };
    }

    private static (DateTime From, DateTime To, string Suffix) GetPeriodBounds(DateTime value, DatabasePartitionGrain grain)
    {
        switch (grain)
        {
            case DatabasePartitionGrain.Year:
            {
                var from = new DateTime(value.Year, 1, 1);
                return (from, from.AddYears(1), $"y{value.Year}");
            }
            case DatabasePartitionGrain.Quarter:
            {
                var q = (value.Month - 1) / 3 + 1;
                var from = new DateTime(value.Year, (q - 1) * 3 + 1, 1);
                return (from, from.AddMonths(3), $"y{value.Year}q{q}");
            }
            case DatabasePartitionGrain.Month:
            {
                var from = new DateTime(value.Year, value.Month, 1);
                return (from, from.AddMonths(1), $"y{value.Year}m{value.Month:D2}");
            }
            case DatabasePartitionGrain.Day:
            {
                var from = value.Date;
                return (from, from.AddDays(1), $"y{value.Year}m{value.Month:D2}d{value.Day:D2}");
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(grain), grain, null);
        }
    }

    private static string ResolveDefaultLiteral(EntityColumnInfo col)
    {
        if (col.IsJson)
            return "'{}'::jsonb";

        var under = col.UnderType != null
            ? Nullable.GetUnderlyingType(col.UnderType) ?? col.UnderType
            : null;

        if (under == typeof(bool))
            return "false";
        if (under == typeof(DateTime))
            return "'1970-01-01'::timestamp";
        if (under == typeof(decimal) || under == typeof(int) || under == typeof(long) || under == typeof(double) || under == typeof(float))
            return "0";
        if (under == typeof(Ulid) || under == typeof(string) || under is { IsEnum: true })
            return "''";

        var dt = (col.DataType ?? string.Empty).Trim().ToLowerInvariant();
        if (dt is "bool" or "boolean" or "bit")
            return "false";
        if (dt.Contains("timestamp", StringComparison.Ordinal) || dt.Contains("datetime", StringComparison.Ordinal))
            return "'1970-01-01'::timestamp";
        if (dt is "int4" or "int8" or "integer" or "bigint" or "numeric" or "decimal" or "float8")
            return "0";
        if (dt is "jsonb")
            return "'{}'::jsonb";

        return "''";
    }

    private static string ResolvePgType(EntityColumnInfo col)
    {
        if (col.IsJson)
            return "jsonb";

        var under = col.UnderType != null
            ? Nullable.GetUnderlyingType(col.UnderType) ?? col.UnderType
            : null;

        if (!string.IsNullOrWhiteSpace(col.DataType))
        {
            var dt = col.DataType.Trim().ToLowerInvariant();
            if (dt.StartsWith("vector", StringComparison.Ordinal))
                return col.DataType.Trim();

            return dt switch
            {
                "varchar" or "character varying" => col.Length > 0 ? $"varchar({col.Length})" : "text",
                "nvarchar" => col.Length > 0 ? $"varchar({col.Length})" : "text",
                "decimal" or "numeric" => FormatNumeric(col),
                "datetime" or "datetime2" or "timestamp" or "timestamp without time zone" => "timestamp",
                "timestamptz" or "timestamp with time zone" => "timestamptz",
                "bit" or "boolean" or "bool" => "boolean",
                "int" or "int32" or "integer" => "int4",
                "long" or "int64" or "bigint" => "int8",
                "float" or "double" or "float8" => "float8",
                "uniqueidentifier" or "uuid" => "uuid",
                "text" => "text",
                "jsonb" => "jsonb",
                "json" => "json",
                _ => col.Length > 0 && dt is "varchar" ? $"varchar({col.Length})" : dt
            };
        }

        if (under == typeof(Ulid))
            return "varchar(26)";
        if (under == typeof(string))
            return col.Length > 0 ? $"varchar({col.Length})" : "text";
        if (under == typeof(DateTime))
            return "timestamp";
        if (under == typeof(bool))
            return "boolean";
        if (under == typeof(int))
            return "int4";
        if (under == typeof(long))
            return "int8";
        if (under == typeof(decimal))
            return FormatNumeric(col);
        if (under is { IsEnum: true })
            return col.Length > 0 ? $"varchar({col.Length})" : "varchar(64)";

        return "text";
    }

    private static string FormatNumeric(EntityColumnInfo col)
    {
        var precision = col.Length > 0 ? col.Length : 18;
        var scale = col.DecimalDigits > 0 ? col.DecimalDigits : 2;
        return $"numeric({precision},{scale})";
    }
}
