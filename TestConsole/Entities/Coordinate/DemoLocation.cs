using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.Coordinate;

/// <summary>
///     仓库坐标示例：平面货位（2D）+ 立体货位（3D）。
/// </summary>
[SugarTable("demo_location")]
[CodeFirst]
[Tenant("system")]
public class DemoLocation
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "code", Length = 64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>平面坐标（X/Y）。</summary>
    [SugarColumn(ColumnName = "slot_xy")]
    [DatabaseCoordinate(2)]
    public DatabaseCoordinate SlotXy { get; set; }

    /// <summary>立体坐标（X/Y/Z，如巷道/排/层）。</summary>
    [SugarColumn(ColumnName = "slot_xyz", IsNullable = true)]
    [DatabaseCoordinate(3)]
    public DatabaseCoordinate SlotXyz { get; set; }
}
