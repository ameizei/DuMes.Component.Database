using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.Nav;

/// <summary>
///     导航演示：订单明细（一对多子表）。
/// </summary>
[SugarTable("demo_nav_order_item")]
[CodeFirst]
[Tenant("system")]
public class DemoNavOrderItem
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "order_id", Length = 26)]
    public Ulid OrderId { get; set; }

    [SugarColumn(ColumnName = "sku", Length = 64)]
    public string Sku { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "qty")]
    public int Qty { get; set; }
}
