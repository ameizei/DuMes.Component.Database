using DuMes.Component.Database.CodeFirst;
using DuMes.Component.Database.DependencyInjection;
using DuMes.Component.Serilog.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SqlSugar.IOC;
using TestConsole.Entities;
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
await host.StartAsync(); // Warmup：建库 / 架构

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TestConsole");

// CodeFirst：按实体 [DatabaseGroup]→ConfigId 扫描建表（可在任意业务入口调用）
var codeFirstMap = DatabaseCodeFirst.InitTables(typeof(DemoProduct).Assembly);
foreach (var (group, types) in codeFirstMap)
    logger.LogInformation("CodeFirst GroupName/ConfigId={Group} Types={Count}：{Names}",
        group, types.Length, string.Join(", ", types.Select(t => t.Name)));

var systemDb = DbScoped.SugarScope.GetConnection("system");
var demoDb = DbScoped.SugarScope.GetConnection("demo");

await CrudScenario.RunAsync(systemDb, logger);
await MultiDbScenario.RunAsync(systemDb, demoDb, logger);
await NavigateScenario.RunAsync(systemDb, logger);

logger.LogInformation("TestConsole 全部场景完成。");
await host.StopAsync();
Log.CloseAndFlush();
return 0;
