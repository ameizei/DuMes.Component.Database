using DuMes.Component.Database.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;
using SqlSugar.IOC;
using TestConsole.Entities.Crud;

namespace TestConsole.Scenarios;

/// <summary>
///     基础增删改查：Insertable / Queryable / Updateable / Deleteable，含枚举与 IsJson。
/// </summary>
internal static class CrudScenario
{
    public static async Task RunAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("======== [Crud] 基础增删改查 ========");

        var now = DateTime.Now;
        var supplierId = Ulid.NewUlid();
        var tagId1 = Ulid.NewUlid();
        var tagId2 = Ulid.NewUlid();

        logger.LogInformation("[Crud] 增：Insertable（枚举 + IsJson 对象/List；实体链式赋值）");
        var product = new DemoProduct().NewId();
        product
            .Set(x => x.Name, "widget-" + product.Id.ToString()[..8])
            .Set(x => x.Price, 12.5m)
            .Set(x => x.Status, DemoProductStatus.Draft)
            .Set(x => x.Detail, new DemoProductDetail
            {
                Sku = "SKU-001",
                WeightGram = 250,
                SupplierId = supplierId,
                PreferredStatus = DemoProductStatus.OnSale
            })
            .Set(x => x.Tags,
            [
                new DemoProductTag
                {
                    Code = "hot",
                    Label = "热销",
                    TagId = tagId1,
                    RelatedStatus = DemoProductStatus.OnSale
                },
                new DemoProductTag
                {
                    Code = "new",
                    Label = "新品",
                    TagId = tagId2,
                    RelatedStatus = DemoProductStatus.Draft
                }
            ])
            .Set(x => x.CreateTime, now)
            .Set(x => x.IsDelete, false);
        var productId = product.Id;
        var insertRows = await systemDb.Insertable(product).ExecuteCommandAsync();
        logger.LogInformation("[Crud] 插入 demo_product 行数={Rows} Id={Id} Status={Status}", insertRows, productId, product.Status);

        logger.LogInformation("[Crud] 查：Queryable / 枚举 / IsJson 反序列化");
        var loaded = await systemDb.Queryable<DemoProduct>()
            .Where(x => x.Id == productId)
            .FirstAsync();
        logger.LogInformation(
            "[Crud] 单条 Name={Name} Price={Price} Status={Status} Detail.Sku={Sku} Tags.Count={TagCount}",
            loaded.Name, loaded.Price, loaded.Status, loaded.Detail?.Sku, loaded.Tags?.Count ?? 0);

        var statusInDb = await systemDb.Ado.GetStringAsync(
            "SELECT status FROM demo_product WHERE id = @id",
            new SugarParameter("@id", productId.ToString()));
        logger.LogInformation("[Crud] 库中 status={Raw}（应为 Draft）", statusInDb);

        var detailJson = await systemDb.Ado.GetStringAsync(
            "SELECT detail::text FROM demo_product WHERE id = @id",
            new SugarParameter("@id", productId.ToString()));
        var tagsJson = await systemDb.Ado.GetStringAsync(
            "SELECT tags::text FROM demo_product WHERE id = @id",
            new SugarParameter("@id", productId.ToString()));
        logger.LogInformation("[Crud] detail JSON={Json}", detailJson);
        logger.LogInformation("[Crud] tags JSON={Json}", tagsJson);

        var jsonUsesEnumNames = detailJson.Contains("\"preferredStatus\"", StringComparison.Ordinal)
                                && detailJson.Contains("\"OnSale\"", StringComparison.Ordinal)
                                && !detailJson.Contains("\"preferredStatus\": 1", StringComparison.Ordinal)
                                && tagsJson.Contains("\"OnSale\"", StringComparison.Ordinal)
                                && tagsJson.Contains("\"Draft\"", StringComparison.Ordinal);
        var jsonUsesCamelCase = detailJson.Contains("\"sku\"", StringComparison.Ordinal)
                                && detailJson.Contains("\"supplierId\"", StringComparison.Ordinal);
        logger.LogInformation("[Crud] STJ 枚举名 OK={EnumOk}；驼峰 OK={CamelOk}", jsonUsesEnumNames, jsonUsesCamelCase);
        if (!jsonUsesEnumNames || !jsonUsesCamelCase)
            throw new InvalidOperationException("IsJson 未按 System.Text.Json 约定序列化（枚举名/驼峰）");

        var detailOk = loaded.Detail != null
                       && loaded.Detail.Sku == "SKU-001"
                       && loaded.Detail.SupplierId == supplierId
                       && loaded.Detail.PreferredStatus == DemoProductStatus.OnSale;
        var tagsOk = loaded.Tags is { Count: 2 }
                     && loaded.Tags[0].TagId == tagId1
                     && loaded.Tags[0].RelatedStatus == DemoProductStatus.OnSale
                     && loaded.Tags[1].TagId == tagId2
                     && loaded.Tags[1].RelatedStatus == DemoProductStatus.Draft;
        logger.LogInformation("[Crud] IsJson 对象往返 OK={DetailOk}；List 往返 OK={TagsOk}", detailOk, tagsOk);
        if (!detailOk || !tagsOk)
            throw new InvalidOperationException("IsJson 嵌套对象/List 往返校验失败");

        var page = await systemDb.Queryable<DemoProduct>()
            .Where(x => x.IsDelete == false && x.Status == DemoProductStatus.Draft)
            .OrderBy(x => x.CreateTime, OrderByType.Desc)
            .ToPageListAsync(1, 10);
        logger.LogInformation("[Crud] 分页第1页条数={Count}", page.Count);

        logger.LogInformation("[Crud] 改：Updateable（枚举 + IsJson）");
        loaded.Price = 19.9m;
        loaded.Status = DemoProductStatus.OnSale;
        loaded.Detail.WeightGram = 300;
        loaded.Detail.PreferredStatus = DemoProductStatus.OffSale;
        loaded.Tags.Add(new DemoProductTag
        {
            Code = "sale",
            Label = "促销",
            TagId = Ulid.NewUlid(),
            RelatedStatus = DemoProductStatus.OffSale
        });
        loaded.ModifyTime = DateTime.Now;
        var updateRows = await systemDb.Updateable(loaded)
            .UpdateColumns(x => new { x.Price, x.Status, x.Detail, x.Tags, x.ModifyTime })
            .ExecuteCommandAsync();
        logger.LogInformation("[Crud] 更新行数={Rows} Price={Price} Status={Status} Tags.Count={TagCount}",
            updateRows, loaded.Price, loaded.Status, loaded.Tags.Count);

        var reloaded = await systemDb.Queryable<DemoProduct>()
            .Where(x => x.Id == productId)
            .FirstAsync();
        logger.LogInformation(
            "[Crud] 更新后 WeightGram={Weight} PreferredStatus={Preferred} Tags.Count={TagCount}",
            reloaded.Detail?.WeightGram, reloaded.Detail?.PreferredStatus, reloaded.Tags?.Count ?? 0);

        logger.LogInformation("[Crud] 删：软删 + 物理删");
        loaded.IsDelete = true;
        loaded.ModifyTime = DateTime.Now;
        await systemDb.Updateable(loaded)
            .UpdateColumns(x => new { x.IsDelete, x.ModifyTime })
            .ExecuteCommandAsync();

        var softDeleted = await systemDb.Queryable<DemoProduct>()
            .Where(x => x.Id == productId && x.IsDelete == true)
            .AnyAsync();
        logger.LogInformation("[Crud] 软删校验 IsDelete={Ok}", softDeleted);

        var deletedProducts = await systemDb.Deleteable<DemoProduct>()
            .Where(x => x.Id == productId)
            .ExecuteCommandAsync();
        logger.LogInformation("[Crud] 物理删除 product={ProductRows}", deletedProducts);

        logger.LogInformation("======== [Crud] 完成 ========");
    }
}
