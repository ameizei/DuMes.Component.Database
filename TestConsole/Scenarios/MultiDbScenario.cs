using Microsoft.Extensions.Logging;
using SqlSugar;
using SqlSugar.IOC;
using TestConsole.Entities;

namespace TestConsole.Scenarios;

/// <summary>
///     多库 / 多架构导航：<c>GetConnection(ConfigId)</c> 查询、插入、修改、删除与跨库事务。
/// </summary>
internal static class MultiDbScenario
{
    public static async Task RunAsync(ISqlSugarClient systemDb, ISqlSugarClient demoDb, ILogger logger)
    {
        logger.LogInformation("======== [MultiDb] GetConnection 多库导航 ========");

        var productId = Ulid.NewUlid();
        var product = new DemoProduct
        {
            Id = productId,
            Name = "multidb-" + productId.ToString()[..8],
            Price = 1m,
            Status = DemoProductStatus.Draft,
            CreateTime = DateTime.Now,
            IsDelete = false
        };
        await systemDb.Insertable(product).ExecuteCommandAsync();

        logger.LogInformation("[MultiDb] 插入：GetConnection(demo) 写审计 + 跨库事务改主表");
        Ulid auditId;
        try
        {
            DbScoped.SugarScope.BeginTran();

            auditId = Ulid.NewUlid();
            var audit = new DemoAuditLog
            {
                Id = auditId,
                ProductId = productId,
                Action = "multidb_insert",
                Message = "from MultiDbScenario",
                CreateTime = DateTime.Now
            };
            await demoDb.Insertable(audit).ExecuteCommandAsync();

            product.Name = product.Name + "-tx";
            product.ModifyTime = DateTime.Now;
            await systemDb.Updateable(product)
                .UpdateColumns(x => new { x.Name, x.ModifyTime })
                .ExecuteCommandAsync();

            DbScoped.SugarScope.CommitTran();
            logger.LogInformation("[MultiDb] 事务提交成功 AuditId={AuditId}", auditId);
        }
        catch (Exception ex)
        {
            DbScoped.SugarScope.RollbackTran();
            logger.LogError(ex, "[MultiDb] 事务失败，已回滚");
            throw;
        }

        logger.LogInformation("[MultiDb] 查询：demo 审计 + 默认库 vs GetConnection(system)");
        var audits = await demoDb.Queryable<DemoAuditLog>()
            .Where(x => x.ProductId == productId)
            .ToListAsync();
        logger.LogInformation("[MultiDb] demo 架构审计条数={Count}", audits.Count);
        if (audits.Count != 1)
            throw new InvalidOperationException("多库插入审计条数不符合预期");

        var viaScope = await DbScoped.SugarScope.Queryable<DemoProduct>()
            .Where(x => x.Id == productId)
            .FirstAsync();
        var viaNav = await DbScoped.SugarScope.GetConnection("system").Queryable<DemoProduct>()
            .Where(x => x.Id == productId)
            .FirstAsync();
        logger.LogInformation("[MultiDb] 默认库 Name={DefaultName}；GetConnection(system) Name={NavName}",
            viaScope.Name, viaNav.Name);
        if (viaScope.Name != viaNav.Name || !viaNav.Name.EndsWith("-tx", StringComparison.Ordinal))
            throw new InvalidOperationException("多库查询 / 事务更新校验失败");

        logger.LogInformation("[MultiDb] 修改：GetConnection(demo) 更新审计 Message");
        var auditRow = audits[0];
        auditRow.Message = "updated";
        await demoDb.Updateable(auditRow)
            .UpdateColumns(x => new { x.Message })
            .ExecuteCommandAsync();
        var auditReloaded = await demoDb.Queryable<DemoAuditLog>()
            .Where(x => x.Id == auditId)
            .FirstAsync();
        if (auditReloaded.Message != "updated")
            throw new InvalidOperationException("多库更新校验失败");
        logger.LogInformation("[MultiDb] 审计 Message={Message}", auditReloaded.Message);

        logger.LogInformation("[MultiDb] 删除：GetConnection(demo/system) 物理删");
        var deletedAudits = await demoDb.Deleteable<DemoAuditLog>()
            .Where(x => x.ProductId == productId)
            .ExecuteCommandAsync();
        var deletedProducts = await systemDb.Deleteable<DemoProduct>()
            .Where(x => x.Id == productId)
            .ExecuteCommandAsync();
        logger.LogInformation("[MultiDb] 删除 audit={AuditRows} product={ProductRows}", deletedAudits, deletedProducts);

        logger.LogInformation("======== [MultiDb] 完成 ========");
    }
}
