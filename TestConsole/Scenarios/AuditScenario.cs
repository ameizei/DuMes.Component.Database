using System.Text.Json;
using DuMes.Component.Database.Audit;
using DuMes.Component.Database.Entities;
using DuMes.Component.Database.Serialization;
using Microsoft.Extensions.Logging;
using SqlSugar;
using TestConsole.Entities.Audit;
using TestConsole.Entities.Audit.Plc;
using TestConsole.Entities.Crud;

namespace TestConsole.Scenarios;

/// <summary>
///     统一表 <see cref="DatabaseAuditRecord"/>：被改行 Id + 改前改后 + 操作人/时间。
/// </summary>
internal static class AuditScenario
{
    public static async Task RunAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("======== [Audit] 统一审计表 log_audit ========");

        await RunBusinessFieldUpdateAsync(systemDb, logger);
        await RunNoChangeShortCircuitAsync(systemDb, logger);
        await RunJsonDocumentManualAsync(systemDb, logger);
        await RunUserRolesAndProfileAsync(systemDb, logger);

        logger.LogInformation("======== [Audit] 完成 ========");
    }

    private static async Task RunBusinessFieldUpdateAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("[Audit] —— WithAudit.SetXxx → log_audit ——");

        var product = new DemoProduct()
            .NewId()
            .Set(x => x.Name, "widget-audit")
            .Set(x => x.Price, 10m)
            .Set(x => x.Status, DemoProductStatus.Draft)
            .Set(x => x.Detail, new DemoProductDetail
            {
                Sku = "SKU-OLD",
                WeightGram = 100,
                SupplierId = Ulid.NewUlid(),
                PreferredStatus = DemoProductStatus.OnSale
            })
            .Set(x => x.Tags,
            [
                new DemoProductTag { Code = "hot", Label = "热销", TagId = Ulid.NewUlid(), RelatedStatus = DemoProductStatus.OnSale }
            ])
            .Set(x => x.CreateTime, DateTime.Now)
            .Set(x => x.IsDelete, false);

        await systemDb.Insertable(product).ExecuteCommandAsync();

        var fromDb = await systemDb.Queryable<DemoProduct>()
            .Where(x => x.Id == product.Id)
            .FirstAsync();

        var newTags = new List<DemoProductTag>
        {
            fromDb.Tags[0],
            new DemoProductTag
            {
                Code = "new",
                Label = "新品",
                TagId = Ulid.NewUlid(),
                RelatedStatus = DemoProductStatus.Draft
            }
        };

        var builder = DatabaseAuditBuilder.For(nameof(DemoProduct), fromDb.Id, "Update")
            .By(Ulid.NewUlid(), "李四");

        fromDb.WithAudit(builder)
            .SetName(fromDb.Name, "widget-audit-v2")
            .SetPrice(fromDb.Price, 15.5m)
            .SetStatus(fromDb.Status, DemoProductStatus.OnSale)
            .SetDetailSku(fromDb.Detail.Sku, "SKU-NEW")
            .SetDetailPreferredStatus(fromDb.Detail.PreferredStatus, DemoProductStatus.OffSale)
            .SetTags(fromDb.Tags, newTags);

        if (!builder.HasChanges)
        {
            await systemDb.Deleteable<DemoProduct>().Where(x => x.Id == product.Id).ExecuteCommandAsync();
            return;
        }

        var audit = builder.Build();
        fromDb.ModifyTime = DateTime.Now;

        await systemDb.Updateable(fromDb)
            .UpdateColumns(x => new { x.Name, x.Price, x.Status, x.Detail, x.Tags, x.ModifyTime })
            .ExecuteCommandAsync();
        await systemDb.Insertable(audit).ExecuteCommandAsync();

        var loaded = await systemDb.Queryable<DatabaseAuditRecord>()
            .Where(x => x.Id == audit.Id)
            .FirstAsync();
        logger.LogInformation(
            "[Audit] 统一表回读 EntityId={EntityId} User={User} Changes={Count}",
            loaded.EntityId, loaded.CreateUserName, loaded.Changes?.Count ?? 0);

        var json = JsonSerializer.Serialize(loaded, DatabaseJsonOptions.JsonStringOptions);
        logger.LogInformation("[Audit] JSON={Json}", json);

        if (loaded.EntityId != fromDb.Id || loaded.Changes.Count < 4)
            throw new InvalidOperationException("log_audit 回读不符合预期");

        await systemDb.Deleteable<DatabaseAuditRecord>().Where(x => x.Id == audit.Id).ExecuteCommandAsync();
        await systemDb.Deleteable<DemoProduct>().Where(x => x.Id == product.Id).ExecuteCommandAsync();
    }

    private static async Task RunNoChangeShortCircuitAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("[Audit] —— 无变更短路 ——");

        var product = new DemoProduct()
            .NewId()
            .Set(x => x.Name, "same")
            .Set(x => x.Price, 1m)
            .Set(x => x.Status, DemoProductStatus.Draft)
            .Set(x => x.Detail, new DemoProductDetail { Sku = "S", SupplierId = Ulid.NewUlid() })
            .Set(x => x.Tags, [])
            .Set(x => x.CreateTime, DateTime.Now)
            .Set(x => x.IsDelete, false);
        await systemDb.Insertable(product).ExecuteCommandAsync();

        var fromDb = await systemDb.Queryable<DemoProduct>().InSingleAsync(product.Id);
        var builder = DatabaseAuditBuilder.For(nameof(DemoProduct), fromDb.Id, "Update")
            .By(Ulid.NewUlid(), "李四");

        fromDb.WithAudit(builder)
            .SetName(fromDb.Name, fromDb.Name)
            .SetPrice(fromDb.Price, fromDb.Price)
            .SetTags(fromDb.Tags, fromDb.Tags);

        if (builder.HasChanges)
            throw new InvalidOperationException("无变更场景不应产生审计");

        logger.LogInformation("[Audit] 审计列表为空 → 不写 log_audit、不 Update");
        await systemDb.Deleteable<DemoProduct>().Where(x => x.Id == product.Id).ExecuteCommandAsync();
    }

    /// <summary>
    ///     JsonDocument 按品牌 → 西门子/三菱实体 → SetXxx → 写回 JsonDocument。
    /// </summary>
    private static async Task RunJsonDocumentManualAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("[Audit] —— JsonDocument 按品牌转 PLC 实体再 Set ——");

        var siemens = new SiemensPlcConfig
        {
            Name = "S7-1200",
            Ip = "192.168.1.10",
            Rack = 0,
            Slot = 1
        };

        var station = new DemoStation()
            .NewId()
            .Set(x => x.Name, "ST-01")
            .Set(x => x.PlcBrand, DemoPlcBrand.Siemens)
            .Set(x => x.LoginMethods, ["Web", "Mobile"])
            .Set(x => x.CreateTime, DateTime.Now);
        station.SetPlcConfig(siemens);
        await systemDb.Insertable(station).ExecuteCommandAsync();

        var fromDb = await systemDb.Queryable<DemoStation>().InSingleAsync(station.Id);
        var builder = DatabaseAuditBuilder.For(nameof(DemoStation), fromDb.Id, "Update")
            .By(Ulid.NewUlid(), "王五");

        // 1) 工站自身字段
        fromDb.WithAudit(builder)
            .SetName(fromDb.Name, "ST-02")
            .SetLoginMethods(fromDb.LoginMethods, ["Web", "Api"]);

        // 2) JsonDocument → 按品牌转成西门子配置 → 在配置对象上 Set
        var plc = (SiemensPlcConfig)fromDb.GetPlcConfig();
        plc.WithAudit(builder)
            .SetName(plc.Name, "S7-1500")
            .SetIp(plc.Ip, "192.168.1.20")
            .SetRack(plc.Rack, 0)
            .SetSlot(plc.Slot, 2);

        // 3) 写回 jsonb
        fromDb.SetPlcConfig(plc);

        if (!builder.HasChanges)
            throw new InvalidOperationException("PLC 品牌转换场景应有变更");

        var audit = builder.Build();
        await systemDb.Updateable(fromDb)
            .UpdateColumns(x => new { x.Name, x.PlcBrand, x.Plc, x.LoginMethods })
            .ExecuteCommandAsync();
        await systemDb.Insertable(audit).ExecuteCommandAsync();

        var paths = audit.Changes.Select(x => x.Path).ToHashSet(StringComparer.Ordinal);
        if (!paths.Contains("Plc.Name") || !paths.Contains("Plc.Slot"))
            throw new InvalidOperationException("西门子 PLC Set 未写入 Nested 路径");

        // 三菱品牌：同样流程，字段不同
        var mitsuStation = new DemoStation()
            .NewId()
            .Set(x => x.Name, "ST-M1")
            .Set(x => x.PlcBrand, DemoPlcBrand.Mitsubishi)
            .Set(x => x.LoginMethods, ["Web"])
            .Set(x => x.CreateTime, DateTime.Now);
        mitsuStation.SetPlcConfig(new MitsubishiPlcConfig
        {
            Name = "FX5U",
            Ip = "192.168.2.10",
            NetworkNumber = 0,
            StationNumber = 1
        });
        await systemDb.Insertable(mitsuStation).ExecuteCommandAsync();

        var mitsuDb = await systemDb.Queryable<DemoStation>().InSingleAsync(mitsuStation.Id);
        var mitsuBuilder = DatabaseAuditBuilder.For(nameof(DemoStation), mitsuDb.Id, "Update")
            .By(Ulid.NewUlid(), "王五");
        var mitsuPlc = (MitsubishiPlcConfig)mitsuDb.GetPlcConfig();
        mitsuPlc.WithAudit(mitsuBuilder)
            .SetName(mitsuPlc.Name, "R08")
            .SetStationNumber(mitsuPlc.StationNumber, 2);
        mitsuDb.SetPlcConfig(mitsuPlc);

        var mitsuAudit = mitsuBuilder.Build();
        await systemDb.Updateable(mitsuDb)
            .UpdateColumns(x => new { x.Plc })
            .ExecuteCommandAsync();
        await systemDb.Insertable(mitsuAudit).ExecuteCommandAsync();

        logger.LogInformation(
            "[Audit] 西门子 Changes={SiemensCount}；三菱 Changes={MitsuCount} paths={Paths}",
            audit.Changes.Count, mitsuAudit.Changes.Count,
            string.Join(",", mitsuAudit.Changes.Select(x => x.Path)));

        fromDb.Plc?.Dispose();
        station.Plc?.Dispose();
        mitsuDb.Plc?.Dispose();
        mitsuStation.Plc?.Dispose();
        await systemDb.Deleteable<DatabaseAuditRecord>()
            .Where(x => x.Id == audit.Id || x.Id == mitsuAudit.Id)
            .ExecuteCommandAsync();
        await systemDb.Deleteable<DemoStation>()
            .Where(x => x.Id == station.Id || x.Id == mitsuStation.Id)
            .ExecuteCommandAsync();
    }

    /// <summary>
    ///     用户多角色 / 一对一外键：业务列只存 Id，审计写 Id+当时名称快照。
    /// </summary>
    private static async Task RunUserRolesAndProfileAsync(ISqlSugarClient systemDb, ILogger logger)
    {
        logger.LogInformation("[Audit] —— 角色/外键：写时快照名称 ——");

        var roleA = Ulid.NewUlid();
        var roleB = Ulid.NewUlid();
        var roleC = Ulid.NewUlid();
        var roleD = Ulid.NewUlid();
        var profileOld = Ulid.NewUlid();
        var profileNew = Ulid.NewUlid();

        // 模拟角色字典（写审计时查；不是读审计时再查）
        var roleNames = new Dictionary<Ulid, string>
        {
            [roleA] = "管理员",
            [roleB] = "操作员",
            [roleC] = "访客",
            [roleD] = "审计员"
        };
        string RoleName(Ulid id) => roleNames.TryGetValue(id, out var n) ? n : id.ToString();
        string ProfileName(Ulid? id) => id == profileOld ? "旧资料卡" : id == profileNew ? "新资料卡" : id?.ToString();

        var user = new DemoUser()
            .NewId()
            .Set(x => x.Name, "alice")
            .Set(x => x.RoleIds, [roleA, roleB, roleC])
            .Set(x => x.ProfileId, profileOld)
            .Set(x => x.CreateTime, DateTime.Now);
        await systemDb.Insertable(user).ExecuteCommandAsync();

        var fromDb = await systemDb.Queryable<DemoUser>().InSingleAsync(user.Id);

        var builderShrink = DatabaseAuditBuilder.For(nameof(DemoUser), fromDb.Id, "Update")
            .By(Ulid.NewUlid(), "管理员");
        fromDb.WithAudit(builderShrink)
            .SetRoleIds(fromDb.RoleIds, [roleA, roleB], RoleName)
            .SetProfileId(fromDb.ProfileId, profileNew, ProfileName);

        var auditShrink = builderShrink.Build();
        await systemDb.Updateable(fromDb)
            .UpdateColumns(x => new { x.RoleIds, x.ProfileId })
            .ExecuteCommandAsync();
        await systemDb.Insertable(auditShrink).ExecuteCommandAsync();

        var rolesChange = auditShrink.Changes.First(x => x.Path == "RoleIds");
        var removedRef = rolesChange.Removed?.OfType<DatabaseAuditRef>().FirstOrDefault();
        if (removedRef == null || removedRef.Id != roleC.ToString() || removedRef.Name != "访客")
            throw new InvalidOperationException("角色减少：removed 应含快照 Id+名称「访客」");

        var profileChange = auditShrink.Changes.First(x => x.Path == "ProfileId");
        if (profileChange.After is not DatabaseAuditRef afterProfile || afterProfile.Name != "新资料卡")
            throw new InvalidOperationException("外键审计应快照资料名称");

        logger.LogInformation(
            "[Audit] 3→2 removed={Id}/{Name}；Profile → {ProfileName}",
            removedRef.Id, removedRef.Name, afterProfile.Name);

        // 更换：去掉操作员、加上审计员（快照名称给前台直接展示）
        fromDb = await systemDb.Queryable<DemoUser>().InSingleAsync(user.Id);
        var builderReplace = DatabaseAuditBuilder.For(nameof(DemoUser), fromDb.Id, "Update")
            .By(Ulid.NewUlid(), "管理员");
        fromDb.WithAudit(builderReplace)
            .SetRoleIds(fromDb.RoleIds, [roleA, roleD], RoleName);

        var auditReplace = builderReplace.Build();
        await systemDb.Updateable(fromDb)
            .UpdateColumns(x => new { x.RoleIds })
            .ExecuteCommandAsync();
        await systemDb.Insertable(auditReplace).ExecuteCommandAsync();

        var replace = auditReplace.Changes.First(x => x.Path == "RoleIds");
        var addedNames = replace.Added?.OfType<DatabaseAuditRef>().Select(x => x.Name).ToHashSet() ?? [];
        var removedNames = replace.Removed?.OfType<DatabaseAuditRef>().Select(x => x.Name).ToHashSet() ?? [];
        logger.LogInformation(
            "[Audit] 更换角色 addedNames={Added} removedNames={Removed}",
            string.Join(",", addedNames), string.Join(",", removedNames));

        if (!addedNames.Contains("审计员") || !removedNames.Contains("操作员"))
            throw new InvalidOperationException("角色更换快照名称不符合预期");

        var json = JsonSerializer.Serialize(auditReplace, DatabaseJsonOptions.JsonStringOptions);
        logger.LogInformation("[Audit] 前台可读 JSON（含 name 快照）={Json}", json);

        logger.LogInformation("[Audit] 用户角色/外键快照场景 OK");

        await systemDb.Deleteable<DatabaseAuditRecord>()
            .Where(x => x.EntityId == user.Id)
            .ExecuteCommandAsync();
        await systemDb.Deleteable<DemoUser>().Where(x => x.Id == user.Id).ExecuteCommandAsync();
    }
}
