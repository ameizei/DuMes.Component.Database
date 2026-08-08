using Microsoft.Extensions.Logging;
using SqlSugar;
using TestConsole.Entities.Vector;

namespace TestConsole.Scenarios;

/// <summary>
///     PostgreSQL pgvector：插入 / 往返 / L2 近邻查询。
/// </summary>
internal static class VectorScenario
{
    public static async Task RunAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("======== [Vector] PostgreSQL pgvector ========");

        var id = Ulid.NewUlid();
        var row = new DemoEmbedding
        {
            Id = id,
            Title = "vec-" + id.ToString()[..8],
            Embedding = [0.1f, 0.2f, 0.3f]
        };

        var inserted = await systemDb.Insertable(row).ExecuteCommandAsync();
        logger.LogInformation("[Vector] 插入行数={Rows} Id={Id}", inserted, id);

        var loaded = await systemDb.Queryable<DemoEmbedding>()
            .Where(x => x.Id == id)
            .FirstAsync();
        if (loaded.Embedding is not { Length: 3 }
            || Math.Abs(loaded.Embedding[0] - 0.1f) > 1e-5
            || Math.Abs(loaded.Embedding[1] - 0.2f) > 1e-5
            || Math.Abs(loaded.Embedding[2] - 0.3f) > 1e-5)
            throw new InvalidOperationException("vector 列往返失败：" + Format(loaded.Embedding));

        logger.LogInformation("[Vector] 往返 OK Embedding=[{Emb}]", Format(loaded.Embedding));

        // 再插一条更远的，验证 <-> 近邻
        var farId = Ulid.NewUlid();
        await systemDb.Insertable(new DemoEmbedding
        {
            Id = farId,
            Title = "far",
            Embedding = [9f, 9f, 9f]
        }).ExecuteCommandAsync();

        var nearestId = await systemDb.Ado.GetStringAsync(
            """
            SELECT id
            FROM demo_embedding
            ORDER BY embedding <-> @q::vector
            LIMIT 1
            """,
            new SugarParameter("@q", "[0.1,0.2,0.3]"));
        logger.LogInformation("[Vector] L2 最近邻 Id={Nearest} expect={Expect}", nearestId, id);
        if (!string.Equals(nearestId, id.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException($"近邻查询失败：got={nearestId}");

        var colType = await systemDb.Ado.GetStringAsync(
            """
            SELECT format_type(a.atttypid, a.atttypmod)
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = 'demo_embedding'
              AND a.attname = 'embedding'
              AND n.nspname = current_schema()
              AND a.attnum > 0
              AND NOT a.attisdropped
            LIMIT 1
            """);
        logger.LogInformation("[Vector] 列类型={Type}", colType);
        if (colType == null || !colType.StartsWith("vector", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"embedding 列类型不是 vector：{colType}");

        await systemDb.Deleteable<DemoEmbedding>()
            .Where(x => x.Id == id || x.Id == farId)
            .ExecuteCommandAsync();

        logger.LogInformation("======== [Vector] 完成 ========");
    }

    private static string Format(float[] values)
    {
        if (values == null || values.Length == 0)
            return string.Empty;
        return string.Join(',', values);
    }
}
