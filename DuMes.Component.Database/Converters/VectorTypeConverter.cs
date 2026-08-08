using System.Data;
using System.Globalization;
using Pgvector;
using SqlSugar;

namespace SqlSugar.DbConvert;

/// <summary>
///     <c>float[]</c> / <see cref="Vector"/> ↔ PostgreSQL <c>vector</c> 列转换。
///     由组件在 <c>EntityService</c> 中对带 <c>[DatabaseVector]</c> 的属性挂载。
/// </summary>
public sealed class VectorTypeConverter : ISugarDataConverter
{
    public SugarParameter ParameterConverter<T>(object columnValue, int columnIndex)
    {
        var name = "@vector" + columnIndex;
        if (columnValue == null)
            return new SugarParameter(name, null);

        var vector = ToVector(columnValue);
        // 使用 Pgvector.Vector；配合启动时 GlobalTypeMapper.UseVector + ReloadTypes
        return new SugarParameter(name, vector)
        {
            DbType = System.Data.DbType.Object
        };
    }

    public T QueryConverter<T>(IDataRecord dataRecord, int dataRecordIndex)
    {
        var raw = dataRecord.GetValue(dataRecordIndex);
        if (raw == null || raw == DBNull.Value)
            return default;

        if (typeof(T) == typeof(Vector))
            return (T)(object)ToVector(raw);

        return (T)(object)ToArray(raw);
    }

    internal static Vector ToVector(object value)
    {
        return value switch
        {
            Vector v => v,
            float[] f => new Vector(f),
            double[] d => new Vector(Array.ConvertAll(d, static x => (float)x)),
            string s => new Vector(ParseArray(s)),
            _ => throw new InvalidOperationException(
                $"无法将 {value.GetType().FullName} 转为 pgvector；请使用 float[] 或 Pgvector.Vector。")
        };
    }

    internal static float[] ToArray(object value)
    {
        return value switch
        {
            float[] f => f,
            Vector v => v.ToArray(),
            double[] d => Array.ConvertAll(d, static x => (float)x),
            string s => ParseArray(s),
            _ => throw new InvalidOperationException(
                $"无法将 {value.GetType().FullName} 转为 float[]（pgvector）。")
        };
    }

    private static float[] ParseArray(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var trimmed = text.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            trimmed = trimmed[1..^1];

        if (trimmed.Length == 0)
            return [];

        var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            result[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
        return result;
    }
}
