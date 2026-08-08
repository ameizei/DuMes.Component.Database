using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.Inherit;

/// <summary>
///     PostgreSQL 继承表示例：父表。
/// </summary>
[SugarTable("demo_vehicle")]
[CodeFirst]
[Tenant("system")]
[SugarIndex("ix_{table}_name", nameof(Name), OrderByType.Asc)]
public class DemoVehicle
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "name", Length = 64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>演示父表增列会传播到继承子表。</summary>
    [SugarColumn(ColumnName = "remark", Length = 128, IsNullable = true)]
    public string Remark { get; set; }
}
