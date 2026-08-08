using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.Inherit;

/// <summary>
///     PostgreSQL 继承表示例：子表（C# 继承父实体 + <c>INHERITS</c>）。
/// </summary>
[SugarTable("demo_car")]
[CodeFirst]
[Tenant("system")]
[DatabaseInherit]
[SugarIndex("ix_{table}_doors", nameof(Doors), OrderByType.Asc)]
public class DemoCar : DemoVehicle
{
    [SugarColumn(ColumnName = "doors")]
    public int Doors { get; set; }
}
