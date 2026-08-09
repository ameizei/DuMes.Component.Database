using DuMes.Component.Database.CodeFirst;
using DuMes.Component.Database.DependencyInjection;
using DuMes.Component.Serilog.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SqlSugar.IOC;
using TestConsole.Entities.Crud;
using TestConsole.Scenarios;

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
await host.EnsureComponentDatabaseAsync(); // 建库 / Schema / pgvector / log_audit（须在业务 InitTables 之前）

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TestConsole");

// CodeFirst：扫描 [CodeFirst]+[Tenant]+[SugarTable] 实体并建表（可在任意业务入口调用）
var codeFirstMap = DatabaseCodeFirst.InitTables(typeof(DemoProduct).Assembly);
foreach (var (group, types) in codeFirstMap)
    logger.LogInformation("CodeFirst Tenant/ConfigId={Group} Types={Count}：{Names}",
        group, types.Length, string.Join(", ", types.Select(t => t.Name)));

var systemDb = DbScoped.SugarScope.GetConnection("system");
var demoDb = DbScoped.SugarScope.GetConnection("demo");

await CrudScenario.RunAsync(systemDb, logger);
await MultiDbScenario.RunAsync(systemDb, demoDb, logger);
await NavigateScenario.RunAsync(systemDb, logger);
await PartitionScenario.RunAsync(systemDb, logger);
await InheritScenario.RunAsync(systemDb, logger);
await VectorScenario.RunAsync(systemDb, logger);
await CoordinateScenario.RunAsync(systemDb, logger);
await AuditScenario.RunAsync(systemDb, logger);

logger.LogInformation("TestConsole 全部场景完成。");
Log.CloseAndFlush();
return 0;
