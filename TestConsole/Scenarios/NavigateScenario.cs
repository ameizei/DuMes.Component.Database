using Microsoft.Extensions.Logging;
using SqlSugar;
using TestConsole.Entities;

namespace TestConsole.Scenarios;

/// <summary>
///     SqlSugar 导航 CRUD：InsertNav / Includes 查询 / UpdateNav / DeleteNav（一对一 + 一对多）。
///     文档：导航查询 https://www.donet5.com/Home/Doc?typeId=1188 、
///     导航插入 https://www.donet5.com/Home/Doc?typeId=2430 、
///     导航更新 https://www.donet5.com/Home/Doc?typeId=2432 、
///     导航删除 https://www.donet5.com/Home/Doc?typeId=2431 。
/// </summary>
internal static class NavigateScenario
{
    public static async Task RunAsync(ISqlSugarClient db, ILogger logger)
    {
        logger.LogInformation("======== [Navigate] 导航查询/插入/修改/删除 ========");

        var orderId = Ulid.NewUlid();
        var customerId = Ulid.NewUlid();
        var itemId1 = Ulid.NewUlid();
        var itemId2 = Ulid.NewUlid();

        logger.LogInformation("[Navigate] 插入：InsertNav（一对一 Customer + 一对多 Items）");
        var order = new DemoNavOrder
        {
            Id = orderId,
            CustomerId = customerId,
            Title = "nav-order-" + orderId.ToString()[..8],
            CreateTime = DateTime.Now,
            Customer = new DemoNavCustomer
            {
                Id = customerId,
                Name = "customer-" + customerId.ToString()[..8]
            },
            Items =
            [
                new DemoNavOrderItem { Id = itemId1, OrderId = orderId, Sku = "NAV-A", Qty = 2 },
                new DemoNavOrderItem { Id = itemId2, OrderId = orderId, Sku = "NAV-B", Qty = 5 }
            ]
        };

        await db.InsertNav(order)
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .ExecuteCommandAsync();
        logger.LogInformation("[Navigate] InsertNav 完成 OrderId={OrderId} CustomerId={CustomerId}", orderId, customerId);

        logger.LogInformation("[Navigate] 查询：Includes 填充 Customer / Items");
        var loaded = await db.Queryable<DemoNavOrder>()
            .Includes(x => x.Customer)
            .Includes(x => x.Items)
            .Where(x => x.Id == orderId)
            .FirstAsync();

        if (loaded.Customer == null || loaded.Customer.Id != customerId)
            throw new InvalidOperationException("导航查询一对一 Customer 失败");
        if (loaded.Items is not { Count: 2 })
            throw new InvalidOperationException("导航查询一对多 Items 条数失败");
        logger.LogInformation(
            "[Navigate] 查询 OK Title={Title} Customer={Customer} Items={ItemCount}",
            loaded.Title, loaded.Customer.Name, loaded.Items.Count);

        logger.LogInformation("[Navigate] 修改：UpdateNav（改标题、客户名、明细数量 + 增删明细）");
        loaded.Title = loaded.Title + "-upd";
        loaded.Customer.Name = loaded.Customer.Name + "-upd";
        loaded.Items[0].Qty = 9;
        // 删掉第二行，新增一行
        loaded.Items.RemoveAll(x => x.Id == itemId2);
        var itemId3 = Ulid.NewUlid();
        loaded.Items.Add(new DemoNavOrderItem
        {
            Id = itemId3,
            OrderId = orderId,
            Sku = "NAV-C",
            Qty = 1
        });

        await db.UpdateNav(loaded)
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .ExecuteCommandAsync();

        var afterUpdate = await db.Queryable<DemoNavOrder>()
            .Includes(x => x.Customer)
            .Includes(x => x.Items)
            .Where(x => x.Id == orderId)
            .FirstAsync();

        if (!afterUpdate.Title.EndsWith("-upd", StringComparison.Ordinal)
            || afterUpdate.Customer == null
            || !afterUpdate.Customer.Name.EndsWith("-upd", StringComparison.Ordinal))
            throw new InvalidOperationException("导航更新主表/一对一失败");

        if (afterUpdate.Items is not { Count: 2 }
            || afterUpdate.Items.All(x => x.Id != itemId1)
            || afterUpdate.Items.Any(x => x.Id == itemId2)
            || afterUpdate.Items.All(x => x.Id != itemId3))
            throw new InvalidOperationException("导航更新一对多（增删改明细）失败");

        var qtyOk = afterUpdate.Items.First(x => x.Id == itemId1).Qty == 9;
        if (!qtyOk)
            throw new InvalidOperationException("导航更新明细 Qty 失败");

        logger.LogInformation(
            "[Navigate] UpdateNav OK Title={Title} Customer={Customer} Items={ItemCount} Qty1={Qty}",
            afterUpdate.Title, afterUpdate.Customer.Name, afterUpdate.Items.Count,
            afterUpdate.Items.First(x => x.Id == itemId1).Qty);

        logger.LogInformation("[Navigate] 删除：DeleteNav（主表 + Customer + Items）");
        await db.DeleteNav<DemoNavOrder>(x => x.Id == orderId)
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .ExecuteCommandAsync();

        var orderGone = !await db.Queryable<DemoNavOrder>().AnyAsync(x => x.Id == orderId);
        var customerGone = !await db.Queryable<DemoNavCustomer>().AnyAsync(x => x.Id == customerId);
        var itemsGone = !await db.Queryable<DemoNavOrderItem>().AnyAsync(x => x.OrderId == orderId);
        logger.LogInformation(
            "[Navigate] 删除后 OrderGone={Order} CustomerGone={Customer} ItemsGone={Items}",
            orderGone, customerGone, itemsGone);
        if (!orderGone || !customerGone || !itemsGone)
            throw new InvalidOperationException("导航删除未清理干净");

        logger.LogInformation("======== [Navigate] 完成 ========");
    }
}
