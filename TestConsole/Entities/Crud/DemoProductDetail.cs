namespace TestConsole.Entities.Crud;

/// <summary>
///     IsJson 嵌套对象（非表实体；序列化进 JSON 列）。
/// </summary>
public class DemoProductDetail
{
    public string Sku { get; set; } = string.Empty;

    public int WeightGram { get; set; }

    /// <summary>嵌套 Ulid：走 JSON 序列化，不走 EntityService 列转换。</summary>
    public Ulid SupplierId { get; set; }

    /// <summary>嵌套枚举：走 JSON 序列化，不走 EnumToStringConvert 列转换。</summary>
    public DemoProductStatus PreferredStatus { get; set; }
}
