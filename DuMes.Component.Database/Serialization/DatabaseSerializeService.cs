using System.Text.Json;
using SqlSugar;

namespace DuMes.Component.Database.Serialization;

/// <summary>
///     SqlSugar <see cref="ISerializeService"/>：用 System.Text.Json 替代默认 Newtonsoft（IsJson 等）。
/// </summary>
public sealed class DatabaseSerializeService : ISerializeService
{
    /// <inheritdoc />
    public string SerializeObject(object value)
    {
        if (value == null)
            return null;

        return JsonSerializer.Serialize(value, value.GetType(), DatabaseJsonOptions.JsonStringOptions);
    }

    /// <inheritdoc />
    public string SugarSerializeObject(object value)
    {
        // 与 SerializeObject 共用选项；实体 NoSerialize 等特殊场景业务侧少用，需要时可再扩展
        return SerializeObject(value);
    }

    /// <inheritdoc />
    public T DeserializeObject<T>(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return default(T);

        return JsonSerializer.Deserialize<T>(value, DatabaseJsonOptions.JsonStringOptions);
    }
}
