using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities;

/// <summary>
///     演示实体（落在 ConfigId=<c>system</c> / searchpath=system）。
/// </summary>
[SugarTable("demo_product")]
[DatabaseGroup("system")]
public class DemoProduct
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "name", Length = 100)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "price", DecimalDigits = 2, Length = 18)]
    public decimal Price { get; set; }

    /// <summary>枚举由组件全局映射为 varchar 枚举名，无需写 SqlParameterDbType。</summary>
    [SugarColumn(ColumnName = "status", Length = 32)]
    public DemoProductStatus Status { get; set; }

    /// <summary>嵌套对象 → jsonb。</summary>
    [SugarColumn(ColumnName = "detail", IsJson = true, IsNullable = true, ColumnDataType = "jsonb")]
    public DemoProductDetail Detail { get; set; }

    /// <summary>嵌套 List → jsonb。</summary>
    [SugarColumn(ColumnName = "tags", IsJson = true, IsNullable = true, ColumnDataType = "jsonb")]
    public List<DemoProductTag> Tags { get; set; }

    [SugarColumn(ColumnName = "create_time")]
    public DateTime CreateTime { get; set; }

    [SugarColumn(ColumnName = "modify_time", IsNullable = true)]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(ColumnName = "is_delete")]
    public bool IsDelete { get; set; }
}
