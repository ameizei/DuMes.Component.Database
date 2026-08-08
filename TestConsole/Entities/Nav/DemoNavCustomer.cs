using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.Nav;

/// <summary>
///     导航演示：客户（一对一子表，落在 ConfigId=<c>system</c>）。
/// </summary>
[SugarTable("demo_nav_customer")]
[CodeFirst]
[Tenant("system")]
public class DemoNavCustomer
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "name", Length = 100)]
    public string Name { get; set; } = string.Empty;
}
