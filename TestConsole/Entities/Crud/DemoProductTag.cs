namespace TestConsole.Entities.Crud;

/// <summary>
///     IsJson 嵌套 List 元素。
/// </summary>
public class DemoProductTag
{
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public Ulid TagId { get; set; }

    public DemoProductStatus RelatedStatus { get; set; }
}
