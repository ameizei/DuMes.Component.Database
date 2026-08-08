using DuMes.Component.Database.CodeFirst;
using Microsoft.Extensions.Logging;
using SqlSugar;
using TestConsole.Entities.Coordinate;

namespace TestConsole.Scenarios;

/// <summary>
///     二维 / 三维坐标：往返、内存距离、SQL <c>&lt;-&gt;</c> 近邻。
/// </summary>
internal static class CoordinateScenario
{
    public static async Task RunAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("======== [Coordinate] 2D/3D 坐标 ========");

        var aId = Ulid.NewUlid();
        var bId = Ulid.NewUlid();
        var a = new DemoLocation
        {
            Id = aId,
            Code = "A-01",
            SlotXy = DatabaseCoordinate.From2D(0, 0),
            SlotXyz = DatabaseCoordinate.From3D(0, 0, 0)
        };
        var b = new DemoLocation
        {
            Id = bId,
            Code = "B-02",
            SlotXy = DatabaseCoordinate.From2D(3, 4),
            SlotXyz = DatabaseCoordinate.From3D(1, 2, 2)
        };

        await systemDb.Insertable(a).ExecuteCommandAsync();
        await systemDb.Insertable(b).ExecuteCommandAsync();
        logger.LogInformation("[Coordinate] 插入 A={A} B={B}", aId, bId);

        var loadedB = await systemDb.Queryable<DemoLocation>().Where(x => x.Id == bId).FirstAsync();
        if (loadedB.SlotXy is null || loadedB.SlotXyz is null
            || Math.Abs(loadedB.SlotXy.X - 3) > 1e-5
            || Math.Abs(loadedB.SlotXy.Y - 4) > 1e-5
            || Math.Abs(loadedB.SlotXyz.Z.GetValueOrDefault() - 2) > 1e-5)
            throw new InvalidOperationException("坐标往返失败：" + loadedB.SlotXy + " / " + loadedB.SlotXyz);

        var dist2 = DatabaseCoordinate.Distance(a.SlotXy, loadedB.SlotXy);
        var dist3 = DatabaseCoordinate.Distance(a.SlotXyz, loadedB.SlotXyz);
        logger.LogInformation("[Coordinate] 内存距离 2D={D2} (期望5) 3D={D3} (期望3)", dist2, dist3);
        if (Math.Abs(dist2 - 5d) > 1e-6)
            throw new InvalidOperationException($"2D 距离应为 5，实际 {dist2}");
        if (Math.Abs(dist3 - 3d) > 1e-6)
            throw new InvalidOperationException($"3D 距离应为 3，实际 {dist3}");

        var nearest2d = await systemDb.Ado.GetStringAsync(
            """
            SELECT id
            FROM demo_location
            ORDER BY slot_xy <-> @q::vector
            LIMIT 1
            """,
            new SugarParameter("@q", "[0,0]"));
        logger.LogInformation("[Coordinate] SQL 2D 近邻 Id={Id} expect={Expect}", nearest2d, aId);
        if (!string.Equals(nearest2d, aId.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException("2D SQL 近邻失败");

        var nearest3d = await systemDb.Ado.GetStringAsync(
            """
            SELECT id
            FROM demo_location
            ORDER BY slot_xyz <-> @q::vector
            LIMIT 1
            """,
            new SugarParameter("@q", "[0,0,0]"));
        logger.LogInformation("[Coordinate] SQL 3D 近邻 Id={Id} expect={Expect}", nearest3d, aId);
        if (!string.Equals(nearest3d, aId.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException("3D SQL 近邻失败");

        await systemDb.Deleteable<DemoLocation>()
            .Where(x => x.Id == aId || x.Id == bId)
            .ExecuteCommandAsync();

        logger.LogInformation("======== [Coordinate] 完成 ========");
    }
}
