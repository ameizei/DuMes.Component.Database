using System.Data;
using DuMes.Component.Database.CodeFirst;
using Pgvector;
using SqlSugar;

namespace SqlSugar.DbConvert;

/// <summary>
///     <see cref="DatabaseCoordinate"/> ↔ PostgreSQL <c>vector(2|3)</c>。
/// </summary>
public sealed class CoordinateTypeConverter : ISugarDataConverter
{
    public SugarParameter ParameterConverter<T>(object columnValue, int columnIndex)
    {
        var name = "@coord" + columnIndex;
        if (columnValue == null)
            return new SugarParameter(name, null);

        if (columnValue is not DatabaseCoordinate coordinate)
            throw new InvalidOperationException(
                $"坐标列须为 {nameof(DatabaseCoordinate)}，实际为 {columnValue.GetType().FullName}。");

        var vector = new Vector(coordinate.ToFloatArray());
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

        var coordinate = FromStorage(raw);
        return (T)(object)coordinate;
    }

    internal static DatabaseCoordinate FromStorage(object raw)
    {
        var arr = VectorTypeConverter.ToArray(raw);
        return arr.Length switch
        {
            2 => new DatabaseCoordinate(arr[0], arr[1]),
            3 => new DatabaseCoordinate(arr[0], arr[1], arr[2]),
            _ => throw new InvalidOperationException(
                $"坐标 vector 维度须为 2 或 3，实际 Length={arr.Length}。")
        };
    }
}
