using System.Globalization;
using System.Text;
using DuMes.Component.Database.CodeFirst;
using Pgvector;
using SqlSugar;

namespace DuMes.Component.Database.Internal.Postgres;

/// <summary>
///     分区 / 继承自定义 DDL 的列类型与 DEFAULT 字面量。
/// </summary>
internal static class PostgresColumnSql
{
    public static string ResolveDefaultLiteral(EntityColumnInfo col)
    {
        if (TryBuildVectorDefault(col, out var vectorDefault))
            return vectorDefault;

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

    public static string ResolvePgType(EntityColumnInfo col)
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

    private static bool TryBuildVectorDefault(EntityColumnInfo col, out string literal)
    {
        literal = null;
        var dims = TryGetVectorDimensions(col);
        if (dims is null or <= 0)
            return false;

        literal = BuildZeroVectorLiteral(dims.Value);
        return true;
    }

    private static int? TryGetVectorDimensions(EntityColumnInfo col)
    {
        var raw = (col.DataType ?? string.Empty).Trim();
        if (raw.StartsWith("vector", StringComparison.OrdinalIgnoreCase))
        {
            var start = raw.IndexOf('(');
            var end = raw.IndexOf(')');
            if (start > 0 && end > start
                          && int.TryParse(raw.AsSpan(start + 1, end - start - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                          && n > 0)
                return n;

            return null;
        }

        var under = col.UnderType != null
            ? Nullable.GetUnderlyingType(col.UnderType) ?? col.UnderType
            : null;

        if (under == typeof(DatabaseCoordinate))
            return 2;

        // float[] / Vector 无 DataType 时无法推断维度，不冒充 vector DEFAULT
        if (under == typeof(float[]) || under == typeof(Vector))
        {
        }

        return null;
    }

    private static string BuildZeroVectorLiteral(int dims)
    {
        var sb = new StringBuilder(dims * 2 + 16);
        sb.Append("'[");
        for (var i = 0; i < dims; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append('0');
        }

        sb.Append("]'::vector");
        return sb.ToString();
    }

    private static string FormatNumeric(EntityColumnInfo col)
    {
        var precision = col.Length > 0 ? col.Length : 18;
        var scale = col.DecimalDigits > 0 ? col.DecimalDigits : 2;
        return $"numeric({precision},{scale})";
    }
}