using System.Data;
using SqlSugar;

namespace DuMes.Component.Database.Converters;

/// <summary>
///     Ulid ↔ 库中字符串列转换（<see cref="ISugarDataConverter" />）。
///     由组件在 <c>EntityService</c> 中对 <see cref="Ulid" /> / <c>Ulid?</c> 属性全局挂载，实体列不必再写
///     <c>SqlParameterDbType</c>。
/// </summary>
/// <remarks>
///     勿与 Ulid 包的 <c>System.UlidTypeConverter</c>（<c>TypeConverter</c>）混淆。
/// </remarks>
public sealed class UlidTypeConverter : ISugarDataConverter
{
    public SugarParameter ParameterConverter<T>(object columnValue, int columnIndex)
    {
        var name = "@ulid" + columnIndex;
        if (columnValue == null)
            return new SugarParameter(name, null);

        return new SugarParameter(name, columnValue.ToString());
    }

    public T QueryConverter<T>(IDataRecord dataRecord, int dataRecordIndex)
    {
        var raw = dataRecord.GetValue(dataRecordIndex);
        if (raw == DBNull.Value)
            return default;

        var text = raw.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return default;

        object ulid = Ulid.Parse(text);
        return (T)ulid;
    }
}