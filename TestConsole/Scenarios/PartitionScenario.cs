using DuMes.Component.Database.CodeFirst;
using Microsoft.Extensions.Logging;
using SqlSugar;
using TestConsole.Entities.Partition;

namespace TestConsole.Scenarios;

/// <summary>
///     PostgreSQL 分区表：插入/查询、子分区路由，以及有数据时增删列同步。
/// </summary>
internal static class PartitionScenario
{
    public static async Task RunAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("======== [Partition] PostgreSQL 按月分区 ========");

        var id = Ulid.NewUlid();
        var createTime = DateTime.Now;
        var row = new DemoPartEvent
        {
            Id = id,
            Message = "part-" + id.ToString()[..8],
            Source = "PartitionScenario",
            CreateTime = createTime
        };

        var inserted = await systemDb.Insertable(row).ExecuteCommandAsync();
        logger.LogInformation("[Partition] 插入父表行数={Rows} Id={Id} Source={Source}", inserted, id, row.Source);

        var loaded = await systemDb.Queryable<DemoPartEvent>()
            .Where(x => x.Id == id)
            .FirstAsync();
        if (loaded.Message != row.Message || loaded.Source != row.Source)
            throw new InvalidOperationException("分区表查询往返失败（含增列 source）");

        var childHint = await systemDb.Ado.GetStringAsync(
            """
            SELECT tableoid::regclass::text
            FROM demo_part_event
            WHERE id = @id
            LIMIT 1
            """,
            new SugarParameter("@id", id.ToString()));
        logger.LogInformation("[Partition] 实际子分区={Child} Message={Message}", childHint, loaded.Message);
        if (string.IsNullOrWhiteSpace(childHint) || !childHint.Contains("demo_part_event_", StringComparison.Ordinal))
            throw new InvalidOperationException($"未落到子分区：{childHint}");

        // 有数据时：手工多加一列，再 InitTables，应被 DROP 掉
        await systemDb.Ado.ExecuteCommandAsync(
            "ALTER TABLE demo_part_event ADD COLUMN IF NOT EXISTS _sync_tmp varchar(10) NULL");
        var tmpBefore = await ColumnExistsAsync(systemDb, "demo_part_event", "_sync_tmp");
        logger.LogInformation("[Partition] 制造多余列 _sync_tmp 存在={Exists}", tmpBefore);

        DatabaseCodeFirst.InitTables(systemDb, typeof(DemoPartEvent));

        var sourceExists = await ColumnExistsAsync(systemDb, "demo_part_event", "source");
        var tmpAfter = await ColumnExistsAsync(systemDb, "demo_part_event", "_sync_tmp");
        logger.LogInformation("[Partition] 同步后 source={SourceOk} _sync_tmp={TmpGone}", sourceExists, !tmpAfter);
        if (!sourceExists)
            throw new InvalidOperationException("实体增列 source 未同步到分区表");
        if (tmpAfter)
            throw new InvalidOperationException("库多余列 _sync_tmp 未被同步删除");

        // 有数据行仍在
        var stillThere = await systemDb.Queryable<DemoPartEvent>().AnyAsync(x => x.Id == id);
        if (!stillThere)
            throw new InvalidOperationException("列同步后原有数据丢失");

        var deleted = await systemDb.Deleteable<DemoPartEvent>()
            .Where(x => x.Id == id)
            .ExecuteCommandAsync();
        logger.LogInformation("[Partition] 删除行数={Rows}", deleted);

        logger.LogInformation("======== [Partition] 完成 ========");
    }

    private static async Task<bool> ColumnExistsAsync(ISqlSugarClient db, string table, string column)
    {
        var n = await db.Ado.GetIntAsync(
            """
            SELECT COUNT(1)
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = @table
              AND column_name = @column
            """,
            new SugarParameter("@table", table),
            new SugarParameter("@column", column));
        return n > 0;
    }
}
