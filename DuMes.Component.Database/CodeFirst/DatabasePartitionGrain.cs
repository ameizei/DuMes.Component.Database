namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     PostgreSQL 分区粒度（RANGE）。
/// </summary>
public enum DatabasePartitionGrain
{
    /// <summary>按年。</summary>
    Year = 1,

    /// <summary>按季度。</summary>
    Quarter = 2,

    /// <summary>按月。</summary>
    Month = 3,

    /// <summary>按日。</summary>
    Day = 4
}
