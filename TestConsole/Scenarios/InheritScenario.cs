using DuMes.Component.Database.CodeFirst;
using Microsoft.Extensions.Logging;
using SqlSugar;
using TestConsole.Entities.Inherit;

namespace TestConsole.Scenarios;

/// <summary>
///     PostgreSQL 继承表：多层 <c>ElectricCar : Car : Vehicle</c>、查父含子集、列同步。
/// </summary>
internal static class InheritScenario
{
    public static async Task RunAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("======== [Inherit] PostgreSQL INHERITS（三层） ========");

        var parentId = Ulid.NewUlid();
        var parent = new DemoVehicle
        {
            Id = parentId,
            Name = "bike-" + parentId.ToString()[..8],
            Remark = "parent-only"
        };
        await systemDb.Insertable(parent).ExecuteCommandAsync();
        logger.LogInformation("[Inherit] 插入 Vehicle Id={Id}", parentId);

        var carId = Ulid.NewUlid();
        var car = new DemoCar
        {
            Id = carId,
            Name = "car-" + carId.ToString()[..8],
            Remark = "from-car",
            Doors = 4
        };
        await systemDb.Insertable(car).ExecuteCommandAsync();
        logger.LogInformation("[Inherit] 插入 Car Id={Id} Doors={Doors}", carId, car.Doors);

        var evId = Ulid.NewUlid();
        var ev = new DemoElectricCar
        {
            Id = evId,
            Name = "ev-" + evId.ToString()[..8],
            Remark = "from-ev",
            Doors = 4,
            BatteryKwh = 75.5m
        };
        await systemDb.Insertable(ev).ExecuteCommandAsync();
        logger.LogInformation("[Inherit] 插入 ElectricCar Id={Id} BatteryKwh={Battery}", evId, ev.BatteryKwh);

        var loadedEv = await systemDb.Queryable<DemoElectricCar>()
            .Where(x => x.Id == evId)
            .FirstAsync();
        if (loadedEv.BatteryKwh != ev.BatteryKwh || loadedEv.Doors != 4 || loadedEv.Name != ev.Name)
            throw new InvalidOperationException("孙表 ElectricCar 查询往返失败");

        // 查中间表 / 根表默认包含孙表行
        var fromCar = await systemDb.Queryable<DemoCar>().Where(x => x.Id == evId).FirstAsync();
        var fromVehicle = await systemDb.Queryable<DemoVehicle>().Where(x => x.Id == evId).FirstAsync();
        logger.LogInformation(
            "[Inherit] 查 Car/Vehicle 可见 EV Name={Name} Doors={Doors}",
            fromCar.Name, fromCar.Doors);
        if (fromCar.Name != ev.Name || fromVehicle.Name != ev.Name)
            throw new InvalidOperationException("多层继承：查父/中间表未包含孙表行");

        var onlyVehicle = await systemDb.Ado.GetIntAsync(
            "SELECT COUNT(1) FROM ONLY demo_vehicle WHERE id = @id",
            new SugarParameter("@id", evId.ToString()));
        var onlyCar = await systemDb.Ado.GetIntAsync(
            "SELECT COUNT(1) FROM ONLY demo_car WHERE id = @id",
            new SugarParameter("@id", evId.ToString()));
        if (onlyVehicle != 0 || onlyCar != 0)
            throw new InvalidOperationException("孙行不应出现在 ONLY 父/中间表中");

        await EnsureInheritsAsync(systemDb, "demo_car", "demo_vehicle");
        await EnsureInheritsAsync(systemDb, "demo_electric_car", "demo_car");
        logger.LogInformation("[Inherit] pg_inherits：electric_car→car→vehicle 已登记");

        await systemDb.Ado.ExecuteCommandAsync(
            "ALTER TABLE demo_electric_car ADD COLUMN IF NOT EXISTS _ev_tmp varchar(10) NULL");
        DatabaseCodeFirst.InitTables(
            systemDb, typeof(DemoVehicle), typeof(DemoCar), typeof(DemoElectricCar));

        var batteryOk = await ColumnExistsAsync(systemDb, "demo_electric_car", "battery_kwh");
        var doorsOnEv = await ColumnExistsAsync(systemDb, "demo_electric_car", "doors");
        var remarkOnEv = await ColumnExistsAsync(systemDb, "demo_electric_car", "remark");
        var tmpAfter = await ColumnExistsAsync(systemDb, "demo_electric_car", "_ev_tmp");
        logger.LogInformation(
            "[Inherit] 同步后 battery={Battery} doors={Doors} remark={Remark} _ev_tmp已删={TmpGone}",
            batteryOk, doorsOnEv, remarkOnEv, !tmpAfter);

        if (!batteryOk || !doorsOnEv || !remarkOnEv)
            throw new InvalidOperationException("三层继承列同步异常（battery/doors/remark）");
        if (tmpAfter)
            throw new InvalidOperationException("孙表多余列 _ev_tmp 未被同步删除");

        if (!await systemDb.Queryable<DemoVehicle>().AnyAsync(x => x.Id == parentId)
            || !await systemDb.Queryable<DemoCar>().AnyAsync(x => x.Id == carId)
            || !await systemDb.Queryable<DemoElectricCar>().AnyAsync(x => x.Id == evId))
            throw new InvalidOperationException("列同步后原有数据丢失");

        await systemDb.Deleteable<DemoElectricCar>().Where(x => x.Id == evId).ExecuteCommandAsync();
        await systemDb.Deleteable<DemoCar>().Where(x => x.Id == carId).ExecuteCommandAsync();
        await systemDb.Deleteable<DemoVehicle>().Where(x => x.Id == parentId).ExecuteCommandAsync();

        logger.LogInformation("======== [Inherit] 完成 ========");
    }

    private static async Task EnsureInheritsAsync(ISqlSugarClient db, string child, string parent)
    {
        var n = await db.Ado.GetIntAsync(
            """
            SELECT COUNT(1)
            FROM pg_inherits i
            JOIN pg_class c ON c.oid = i.inhrelid
            JOIN pg_class p ON p.oid = i.inhparent
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = @child
              AND p.relname = @parent
              AND n.nspname = current_schema()
            """,
            new SugarParameter("@child", child),
            new SugarParameter("@parent", parent));
        if (n < 1)
            throw new InvalidOperationException($"pg_inherits 未登记 {child} → {parent}");
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
