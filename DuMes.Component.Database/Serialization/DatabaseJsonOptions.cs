using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cysharp.Serialization.Json;
using SqlSugar;

namespace DuMes.Component.Database.Serialization;

/// <summary>
///     数据库组件共用的 System.Text.Json 选项（IsJson 列、自定义 <see cref="ISerializeService" />）。
/// </summary>
public static class DatabaseJsonOptions
{
    /// <summary>
    ///     Json 字符串设置：驼峰命名、枚举写名称、Ulid、中文不转义、数字可读字符串、可空时间支持 null/空串。
    /// </summary>
    public static JsonSerializerOptions JsonStringOptions { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new UlidJsonConverter());
        options.Converters.Add(new DateTimeConverter());
        options.Converters.Add(new NullDateTimeConverter());

        return options;
    }
}