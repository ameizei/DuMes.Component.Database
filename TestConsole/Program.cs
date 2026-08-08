using DuMes.Component.Database.DependencyInjection;
using DuMes.Component.Serilog.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SqlSugar;
using SqlSugar.IOC;
using TestConsole.Entities;

Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.UseComponentSerilog();
builder.Services.AddComponentDatabase(builder.Configuration);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TestConsole");

// 触发 ISqlSugarClient 解析，注入 AOP 日志
_ = host.Services.GetRequiredService<ISqlSugarClient>();

var systemDb = DbScoped.SugarScope.GetConnection("system");
var demoDb = DbScoped.SugarScope.GetConnection("demo");

logger.LogInformation("准备架构 system / demo …");
EnsureSchema(systemDb, "system");
EnsureSchema(demoDb, "demo");

logger.LogInformation("CodeFirst 建表 …");
systemDb.CodeFirst.InitTables<DemoProduct>();
demoDb.CodeFirst.InitTables<DemoAuditLog>();

var productId = Ulid.NewUlid();
var now = DateTime.Now;

var supplierId = Ulid.NewUlid();
var tagId1 = Ulid.NewUlid();
var tagId2 = Ulid.NewUlid();

logger.LogInformation("=== 1) 增：Insertable（枚举 + IsJson 对象/List） ===");
var product = new DemoProduct
{
    Id = productId,
    Name = "widget-" + productId.ToString()[..8],
    Price = 12.5m,
    Status = DemoProductStatus.Draft,
    Detail = new DemoProductDetail
    {
        Sku = "SKU-001",
        WeightGram = 250,
        SupplierId = supplierId,
        PreferredStatus = DemoProductStatus.OnSale
    },
    Tags =
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
    ],
    CreateTime = now,
    IsDelete = false
};
var insertRows = await systemDb.Insertable(product).ExecuteCommandAsync();
logger.LogInformation("插入 demo_product 行数={Rows} Id={Id} Status={Status}", insertRows, productId, product.Status);

logger.LogInformation("=== 2) 查：Queryable / 枚举 / IsJson 反序列化 ===");
var loaded = await systemDb.Queryable<DemoProduct>()
    .Where(x => x.Id == productId)
    .FirstAsync();
logger.LogInformation(
    "查询单条 Name={Name} Price={Price} Status={Status} Detail.Sku={Sku} Tags.Count={TagCount}",
    loaded.Name, loaded.Price, loaded.Status, loaded.Detail?.Sku, loaded.Tags?.Count ?? 0);

var statusInDb = await systemDb.Ado.GetStringAsync(
    "SELECT status FROM demo_product WHERE id = @id",
    new SugarParameter("@id", productId.ToString()));
logger.LogInformation("库中 status 原始字符串={Raw}（列映射，应为枚举名 Draft）", statusInDb);

var detailJson = await systemDb.Ado.GetStringAsync(
    "SELECT detail::text FROM demo_product WHERE id = @id",
    new SugarParameter("@id", productId.ToString()));
var tagsJson = await systemDb.Ado.GetStringAsync(
    "SELECT tags::text FROM demo_product WHERE id = @id",
    new SugarParameter("@id", productId.ToString()));
logger.LogInformation("库中 detail JSON={Json}", detailJson);
logger.LogInformation("库中 tags JSON={Json}", tagsJson);

// System.Text.Json：驼峰 + 枚举名（PG jsonb::text 可能带空格，分开断言）
var jsonUsesEnumNames = detailJson.Contains("\"preferredStatus\"", StringComparison.Ordinal)
                        && detailJson.Contains("\"OnSale\"", StringComparison.Ordinal)
                        && !detailJson.Contains("\"preferredStatus\": 1", StringComparison.Ordinal)
                        && tagsJson.Contains("\"OnSale\"", StringComparison.Ordinal)
                        && tagsJson.Contains("\"Draft\"", StringComparison.Ordinal);
var jsonUsesCamelCase = detailJson.Contains("\"sku\"", StringComparison.Ordinal)
                        && detailJson.Contains("\"supplierId\"", StringComparison.Ordinal);
logger.LogInformation("STJ 枚举名 OK={EnumOk}；驼峰 OK={CamelOk}", jsonUsesEnumNames, jsonUsesCamelCase);
if (!jsonUsesEnumNames || !jsonUsesCamelCase)
    throw new InvalidOperationException("IsJson 未按 System.Text.Json 约定序列化（枚举名/驼峰）");

// 校验嵌套 Ulid / 枚举是否能正确往返
var detailOk = loaded.Detail != null
               && loaded.Detail.Sku == "SKU-001"
               && loaded.Detail.SupplierId == supplierId
               && loaded.Detail.PreferredStatus == DemoProductStatus.OnSale;
var tagsOk = loaded.Tags is { Count: 2 }
             && loaded.Tags[0].TagId == tagId1
             && loaded.Tags[0].RelatedStatus == DemoProductStatus.OnSale
             && loaded.Tags[1].TagId == tagId2
             && loaded.Tags[1].RelatedStatus == DemoProductStatus.Draft;
logger.LogInformation("IsJson 对象往返 OK={DetailOk}；IsJson List 往返 OK={TagsOk}", detailOk, tagsOk);
if (!detailOk || !tagsOk)
    throw new InvalidOperationException("IsJson 嵌套对象/List 往返校验失败");
var page = await systemDb.Queryable<DemoProduct>()
    .Where(x => x.IsDelete == false && x.Status == DemoProductStatus.Draft)
    .OrderBy(x => x.CreateTime, OrderByType.Desc)
    .ToPageListAsync(1, 10);
logger.LogInformation("分页第1页条数={Count}", page.Count);

logger.LogInformation("=== 3) 改：Updateable（枚举 + 更新 IsJson） ===");
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
logger.LogInformation("更新行数={Rows} Price={Price} Status={Status} Tags.Count={TagCount}",
    updateRows, loaded.Price, loaded.Status, loaded.Tags.Count);

var reloaded = await systemDb.Queryable<DemoProduct>()
    .Where(x => x.Id == productId)
    .FirstAsync();
logger.LogInformation(
    "更新后 Detail.WeightGram={Weight} PreferredStatus={Preferred} Tags.Count={TagCount}",
    reloaded.Detail?.WeightGram, reloaded.Detail?.PreferredStatus, reloaded.Tags?.Count ?? 0);

var statusAfterUpdate = await systemDb.Ado.GetStringAsync(
    "SELECT status FROM demo_product WHERE id = @id",
    new SugarParameter("@id", productId.ToString()));
logger.LogInformation("更新后库中 status={Raw}（应为 OnSale）", statusAfterUpdate);

logger.LogInformation("=== 4) 导航：GetConnection(demo) 写审计 + 多库事务 ===");
try
{
    DbScoped.SugarScope.BeginTran();

    var audit = new DemoAuditLog
    {
        Id = Ulid.NewUlid(),
        ProductId = productId,
        Action = "update_price",
        Message = "price -> 19.9",
        CreateTime = DateTime.Now
    };
    await demoDb.Insertable(audit).ExecuteCommandAsync();

    loaded.Name = loaded.Name + "-tx";
    loaded.ModifyTime = DateTime.Now;
    await systemDb.Updateable(loaded)
        .UpdateColumns(x => new { x.Name, x.ModifyTime })
        .ExecuteCommandAsync();

    DbScoped.SugarScope.CommitTran();
    logger.LogInformation("多库事务提交成功 AuditId={AuditId}", audit.Id);
}
catch (Exception ex)
{
    DbScoped.SugarScope.RollbackTran();
    logger.LogError(ex, "多库事务失败，已回滚");
    throw;
}

var audits = await demoDb.Queryable<DemoAuditLog>()
    .Where(x => x.ProductId == productId)
    .ToListAsync();
logger.LogInformation("demo 架构审计条数={Count}", audits.Count);

logger.LogInformation("=== 5) 导航：默认库 vs 指定 ConfigId ===");
var viaScope = await DbScoped.SugarScope.Queryable<DemoProduct>()
    .Where(x => x.Id == productId)
    .FirstAsync();
var viaNav = await DbScoped.SugarScope.GetConnection("system").Queryable<DemoProduct>()
    .Where(x => x.Id == productId)
    .FirstAsync();
logger.LogInformation("默认库 Name={DefaultName}；GetConnection(system) Name={NavName}", viaScope.Name, viaNav.Name);

logger.LogInformation("=== 6) 删：软删 + 物理删审计 ===");
loaded.IsDelete = true;
loaded.ModifyTime = DateTime.Now;
await systemDb.Updateable(loaded)
    .UpdateColumns(x => new { x.IsDelete, x.ModifyTime })
    .ExecuteCommandAsync();

var softDeleted = await systemDb.Queryable<DemoProduct>()
    .Where(x => x.Id == productId && x.IsDelete == true)
    .AnyAsync();
logger.LogInformation("软删校验 IsDelete={Ok}", softDeleted);

var deletedAudits = await demoDb.Deleteable<DemoAuditLog>()
    .Where(x => x.ProductId == productId)
    .ExecuteCommandAsync();
var deletedProducts = await systemDb.Deleteable<DemoProduct>()
    .Where(x => x.Id == productId)
    .ExecuteCommandAsync();
logger.LogInformation("物理删除 audit={AuditRows} product={ProductRows}", deletedAudits, deletedProducts);

logger.LogInformation("TestConsole 场景完成。");
Log.CloseAndFlush();
return 0;

static void EnsureSchema(ISqlSugarClient db, string schema)
{
    // PostgreSQL 标识符小写；勿拼接不可信输入
    db.Ado.ExecuteCommand($"CREATE SCHEMA IF NOT EXISTS {schema}");
}
