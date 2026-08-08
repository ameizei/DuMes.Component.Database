namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     标记实体为 PostgreSQL 分区表（<c>PARTITION BY RANGE</c>）。
///     须配合 <see cref="DatabasePartitionFieldAttribute"/> 指定分区时间列；仅 PG 系在 CodeFirst 时生效。
/// </summary>
/// <remarks>
///     父表已存在时，<c>InitTables</c> 会对比实体与库列：缺则 <c>ADD COLUMN</c>，多则 <c>DROP COLUMN</c>
///     （有数据亦可；变更落在父表并级联子分区）。分区键列禁止删除。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DatabasePartitionAttribute : Attribute
{
    public DatabasePartitionAttribute(DatabasePartitionGrain grain)
    {
        if (!Enum.IsDefined(grain))
            throw new ArgumentOutOfRangeException(nameof(grain));

        Grain = grain;
    }

    /// <summary>分区粒度：年 / 季 / 月 / 日。</summary>
    public DatabasePartitionGrain Grain { get; }

    /// <summary>
    ///     从当前周期起（含）预创建的分区个数。默认 <c>3</c>。
    /// </summary>
    public int AheadCount { get; set; } = 3;

    /// <summary>
    ///     当前周期之前额外预创建的分区个数。默认 <c>1</c>。
    /// </summary>
    public int PastCount { get; set; } = 1;
}
