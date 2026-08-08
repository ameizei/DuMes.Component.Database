using System.Text.Json;
using DuMes.Component.Database.Serialization;

namespace TestConsole.Entities.Audit.Plc;

/// <summary>
///     PLC 配置基类。库内统一存 <see cref="JsonDocument"/>，按工站品牌转成具体类型后再 <c>WithAudit().SetXxx</c>。
/// </summary>
public abstract class DemoPlcConfig
{
    public string Name { get; set; } = string.Empty;

    public string Ip { get; set; } = string.Empty;

    public abstract DemoPlcBrand Brand { get; }

    /// <summary>按品牌把 jsonb 转成西门子 / 三菱等配置对象。</summary>
    public static DemoPlcConfig FromDocument(DemoPlcBrand brand, JsonDocument document)
    {
        return brand switch
        {
            DemoPlcBrand.Siemens => SiemensPlcConfig.FromDocument(document),
            DemoPlcBrand.Mitsubishi => MitsubishiPlcConfig.FromDocument(document),
            _ => throw new ArgumentOutOfRangeException(nameof(brand), brand, "未知 PLC 品牌")
        };
    }

    public JsonDocument ToDocument()
    {
        var json = JsonSerializer.Serialize(this, GetType(), DatabaseJsonOptions.JsonStringOptions);
        return JsonDocument.Parse(json);
    }

    protected static T DeserializeOrNew<T>(JsonDocument document) where T : DemoPlcConfig, new()
    {
        if (document == null)
            return new T();

        return JsonSerializer.Deserialize<T>(document.RootElement.GetRawText(), DatabaseJsonOptions.JsonStringOptions)
               ?? new T();
    }
}
