using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.Partition;

/// <summary>
///     PostgreSQL 按月分区表示例（父表 + 子分区；插入走父表即可）。
///     <c>source</c> 用于验证已有分区表时实体增列会自动同步。
/// </summary>
[SugarTable("demo_part_event")]
[CodeFirst]
[Tenant("system")]
[DatabasePartition(DatabasePartitionGrain.Month, AheadCount = 3, PastCount = 1)]
public class DemoPartEvent
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "message", Length = 200)]
    public string Message { get; set; } = string.Empty;

    /// <summary>演示增列同步（InitTables 对已存在分区父表 ADD COLUMN）。</summary>
    [SugarColumn(ColumnName = "source", Length = 64, IsNullable = true)]
    public string Source { get; set; }

    [SugarColumn(ColumnName = "create_time")]
    [DatabasePartitionField]
    public DateTime CreateTime { get; set; }
}
