using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.Inherit;

/// <summary>
///     PostgreSQL 继承表示例：孙表（<c>DemoElectricCar : DemoCar : DemoVehicle</c>）。
/// </summary>
[SugarTable("demo_electric_car")]
[CodeFirst]
[Tenant("system")]
[DatabaseInherit]
public class DemoElectricCar : DemoCar
{
    [SugarColumn(ColumnName = "battery_kwh", DecimalDigits = 2, Length = 10)]
    public decimal BatteryKwh { get; set; }
}
