using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.Nav;

/// <summary>
///     导航演示：订单主表（一对一客户 + 一对多明细）。
/// </summary>
[SugarTable("demo_nav_order")]
[CodeFirst]
[Tenant("system")]
public class DemoNavOrder
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "customer_id", Length = 26)]
    public Ulid CustomerId { get; set; }

    [SugarColumn(ColumnName = "title", Length = 100)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "create_time")]
    public DateTime CreateTime { get; set; }

    /// <summary>一对一：本表 <see cref="CustomerId"/> → 客户主键。</summary>
    [Navigate(NavigateType.OneToOne, nameof(CustomerId))]
    public DemoNavCustomer Customer { get; set; }

    /// <summary>一对多：明细表 <see cref="DemoNavOrderItem.OrderId"/> → 本表主键。</summary>
    [Navigate(NavigateType.OneToMany, nameof(DemoNavOrderItem.OrderId))]
    public List<DemoNavOrderItem> Items { get; set; }
}
