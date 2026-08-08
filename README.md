# DuMes.Component.Database

多脉数据库组件：基于 [SqlSugar](https://www.donet5.com/Home/Doc) + [SqlSugar.IOC](https://www.donet5.com/Doc/1/2247) 封装数据库访问，提供统一 DI 注册、多库 / 读写分离、**ULID 主键**约定、SQL AOP（慢查询与错误落盘）。**已依赖** [DuMes.Component.Serilog](https://github.com/ameizei/DuMes.Component.Serilog)，AOP 走同一 `ILogger` 管道（`LogDebug` / `Write*`）。

> **`DbType` 可配置**（`IocDbType`），**当前默认 `PostgreSQL`**。默认驱动：`Npgsql` + `SqlSugarCoreNoDrive`。主键使用 [Ulid](https://www.nuget.org/packages/Ulid)（`Ulid.NewUlid()`）。改用其它库类型时需自行补充对应驱动包。

## 项目结构

```text
DuMes.Component.Database/
├── DependencyInjection/     # AddComponentDatabase
├── Options/                 # DatabaseComponentOptions、DatabaseConnectionOptions
├── Serialization/           # System.Text.Json（IsJson / ISerializeService）
├── Converters/              # UlidTypeConverter（表列 EntityService）
└── Internal/
    └── Aop/                 # 映射 / 序列化挂载 / SQL AOP
```

## 分工

| 组件 | 职责 |
|------|------|
| SqlSugar / SqlSugar.IOC | ORM、多库 `ConfigId`、`DbScoped.SugarScope` |
| Npgsql | 默认 `PostgreSQL` 时的 ADO.NET 驱动（NoDrive 包需显式引用） |
| Ulid | 主键生成（`Ulid.NewUlid()`）；与 SqlSugar 类型映射配合 |
| DuMes.Component.Serilog | **包依赖**；宿主须 `UseComponentSerilog`；全量 SQL / 慢 SQL / 错误均走此管道 |

```text
业务代码
  ├─ DbScoped.SugarScope / ISqlSugarClient
  │     ├─ 主库 / 多库 GetConnection(configId)
  │     ├─ 可选从库（读写分离）
  │     └─ AOP → ILogger（Serilog 组件）
  └─ Ulid.NewUlid()           → 主键
         ↑
   DbType（默认 PostgreSQL；单库或多库；从库仅读）
```

## 接入

宿主**必须**先接入 Serilog 管道（本组件已 PackageReference `DuMes.Component.Serilog`）：

```csharp
using DuMes.Component.Database.DependencyInjection;
using DuMes.Component.Serilog.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseComponentSerilog(); // 必接：Development 下 LogDebug→调试窗口；Write* 落盘
builder.Services.AddComponentDatabase(builder.Configuration);
```

配置节名固定为 **`Database`**（缺失则启动失败）。

也可纯代码配置（不读配置节）：

```csharp
builder.Services.AddComponentDatabase(o =>
{
    o.Connections =
    [
        new DatabaseConnectionOptions
        {
            ConfigId = "main",
            ConnectionString = "Host=127.0.0.1;Port=5432;Database=dumes;Username=postgres;Password=***"
        }
    ];
});
```

### 注册后得到什么

| 内容 | 说明 |
|------|------|
| SqlSugar.IOC 多库注册 | 每条 `Connections` → 一个 `IocConfig`（`DbType` 可配，默认 PostgreSQL） |
| `DbScoped.SugarScope` / `ISqlSugarClient` | 业务侧查询、事务入口 |
| AOP | 经 `ILogger` + Serilog：`LogDebug` / `WriteWarning` / `WriteError`（见「组件内置行为」） |

主键由业务在插入前赋值：`entity.Id = Ulid.NewUlid()`（不依赖雪花 `DatacenterId` / `WorkId`）。

## 配置说明

### 配置项一览

| 配置项 | 类型 | 默认值 | 必填 | 说明 |
|--------|------|--------|------|------|
| `Connections` | array | — | 是 | 至少一个连接；见下表 |
| `SlowSqlSeconds` | double | `1` | 否 | 超过该秒数记慢 SQL；须 `> 0` |
| `PgSqlIsAutoToLower` | bool | `true` | 否 | 自动将表/列名转小写（默认开启，与「库内全小写」约定一致） |

#### `Connections[]`

| 配置项 | 类型 | 默认值 | 必填 | 说明 |
|--------|------|--------|------|------|
| `ConfigId` | string | — | 是 | 多库标识；同一列表内忽略大小写唯一 |
| `ConnectionString` | string | — | 是 | 连接串（格式随 `DbType`）；空则启动报错 |
| `DbType` | `IocDbType` | `PostgreSQL` | 否 | 数据库类型，见 [IocDbType](https://www.donet5.com/Doc/1/2247)；**当前默认 PostgreSQL**；从库未写时继承主库 |
| `IsAutoCloseConnection` | bool | `true` | 否 | 是否自动关闭连接 |
| `Slaves` | array | `[]` | 否 | 从库列表；项结构同连接（须有 `ConfigId` + `ConnectionString`） |

### 组件内置行为（不可配置项）

日志一律走 [DuMes.Component.Serilog](https://github.com/ameizei/DuMes.Component.Serilog)（宿主已 `UseComponentSerilog`）。无 `EnableSqlDebugLog`；分流由 Serilog 环境与 `Write*` 约定决定：

| 行为 | 说明 |
|------|------|
| 配置校验 | 启动时 `Validate()`：节存在、连接非空、`ConfigId` 唯一、`DbType` 合法 |
| 全量 SQL | `OnLogExecuting` → `ILogger.LogDebug`（赋值后 SQL）。**仅 Development** 会进**调试窗口**（Serilog：Development 最低 Debug；其它环境最低 Information，故 Production **不会**打全量 SQL） |
| 慢 SQL | `OnLogExecuted`：耗时 ≥ `SlowSqlSeconds` → `WriteWarning("sql_slow", …)` → `logs/sql_slow.log`（各环境均落盘） |
| SQL 错误 | `OnError` → `WriteError("sql_error", …)`（可带异常）→ `logs/sql_error.log`（含赋值后 SQL / 参数摘要，**不含连接串**；各环境均落盘） |
| 与 MEL 区别 | `LogDebug` **不落盘**；只有 `WriteWarning` / `WriteError` 写文件（见 Serilog README） |
| Pg 命名 | `DbType=PostgreSQL` 时默认开启 `PgSqlIsAutoToLower`；实体列须显式 `ColumnName`（见「命名约定」） |
| Ulid 映射 | `EntityService` 全局挂载 `UlidTypeConverter` → `varchar(26)`（仅**表列**） |
| 枚举映射 | `EntityService` 全局挂载 SqlSugar `EnumToStringConvert` → 库中存**枚举名**（仅**表列**） |
| IsJson | 表列 `IsJson=true` + `ColumnDataType=jsonb`；经 `DatabaseSerializeService`（System.Text.Json）序列化：驼峰、枚举名、Ulid 字符串 |
| 环境 | 全量 SQL（`LogDebug`） | 慢 SQL / 错误（`Write*`） |
|------|------------------------|---------------------------|
| Development | ✓ 调试窗口 | ✓ `logs/sql_slow.log` / `logs/sql_error.log` |
| Production 等 | ✗（级别被抬到 Information） | ✓ 同上 |

### 完整示例（含注释）

> 下列为 **JSONC** 示意（`//` 注释便于阅读）；拷贝到 `appsettings.*.json` 时请去掉注释。  
> 环境相关配置请放在 `appsettings.Development.json` / `appsettings.Production.json`，不要堆进主 `appsettings.json`。

```jsonc
{
  "Database": {
    // 慢 SQL 阈值（秒）；超时与错误经 Write* 始终落盘（与环境无关）
    "SlowSqlSeconds": 1,

    // 表/列名自动转小写（默认 true）
    "PgSqlIsAutoToLower": true,

    "Connections": [
      {
        // 多库标识；业务 GetConnection("main")
        "ConfigId": "main",

        // 数据库类型（IocDbType）；可省略，默认 PostgreSQL
        "DbType": "PostgreSQL",

        // 连接串（可用 User Secrets / 环境变量覆盖）
        "ConnectionString": "Host=127.0.0.1;Port=5432;Database=dumes;Username=postgres;Password=your-password",

        "IsAutoCloseConnection": true,

        // 读写分离从库（可选）
        "Slaves": [
          // {
          //   "ConfigId": "main-slave-1",
          //   "ConnectionString": "Host=127.0.0.2;Port=5432;Database=dumes;Username=postgres;Password=your-password"
          // }
        ]
      }
      // 第二套库示例：
      // ,{
      //   "ConfigId": "log",
      //   "ConnectionString": "Host=127.0.0.1;Port=5432;Database=dumes_log;Username=postgres;Password=your-password"
      // }
    ]
  }
}
```

### 代码配置

```csharp
builder.Services.AddComponentDatabase(o =>
{
    o.SlowSqlSeconds = 1;
    o.Connections =
    [
        new DatabaseConnectionOptions
        {
            ConfigId = "main",
            DbType = IocDbType.PostgreSQL, // 可省略，默认 PostgreSQL
            ConnectionString = "Host=127.0.0.1;Port=5432;Database=dumes;Username=postgres;Password=your-password"
        }
    ];
});
```

也可在配置基础上再覆盖：

```csharp
builder.Services.AddComponentDatabase(
    builder.Configuration,
    configureOptions: o => o.SlowSqlSeconds = 2);
```

## 命名约定（强制）

当 `DbType` 为 **PostgreSQL**（当前默认）时：**表名、列名一律小写**；组合词用 **下划线** 分隔（`snake_case`），例如 `create_time`、`user_name`、`is_delete`。其它 `DbType` 的命名另议，但仍建议实体显式写 `ColumnName`。

| 规则 | 正确 | 错误 |
|------|------|------|
| 全小写 | `id`、`name`、`create_time` | `Id`、`CreateTime`、`CREATE_TIME` |
| 组合词用 `_` | `create_time`、`modify_user_id` | `createtime`、`createTime`、`ModifyUserId` |
| 单词语 | `id`、`name`、`status` | — |

写实体时：**每个映射列的 `[SugarColumn]` 都必须写 `ColumnName`**，且值符合上表（小写；组合词 `xxx_xxx`）。不要依赖属性名推断列名。`PgSqlIsAutoToLower` / CodeFirst 转小写是辅助；`CreateTime` 不会自动变成 `create_time`。

```csharp
using SqlSugar;

[SugarTable("product")]
public class Product
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "name")]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "create_time")]
    public DateTime CreateTime { get; set; }

    [SugarColumn(ColumnName = "is_delete")]
    public bool IsDelete { get; set; }
}
```

```csharp
// 错误：未写 ColumnName
[SugarColumn(IsPrimaryKey = true, Length = 26)]
public Ulid Id { get; set; }
```

手写 SQL / 迁移脚本同样遵守：`create table product (...);`，列用 `create_time` 而非 `"CreateTime"`。

## 主键：ULID

约定使用 `Ulid` 作为实体主键类型（Crockford Base32，26 字符；库中一般为 `char(26)` / `varchar(26)`，列名 `id`）。

```csharp
// 插入前赋值
var row = new Product
{
    Id = Ulid.NewUlid(),
    Name = "widget",
    CreateTime = DateTime.Now
};
await DbScoped.SugarScope.Insertable(row).ExecuteCommandAsync();
```

说明：

1. **生成**：`Ulid.NewUlid()`（可排序；与审计字段用 `DateTime.Now` 无关）。
2. **勿用雪花**：本组件不配置 `SnowFlakeSingle` / `DatacenterId` / `WorkId`。
3. **全局映射（Ulid）**：`EntityService` 为 `Ulid` / `Ulid?` 挂载 `UlidTypeConverter`（`varchar(26)`）；实体列不必写 `SqlParameterDbType`。
4. **全局映射（枚举）**：`EntityService` 为枚举 / 可空枚举挂载 SqlSugar 自带 `EnumToStringConvert`，库中存枚举**名称**（如 `OnSale`），非数值；列上已显式指定 `SqlParameterDbType` 时不覆盖。
5. **可空值类型**：业务上「可无」的外键可用 `Ulid?`；主键仍写 `Ulid`，插入前必须赋值。

## 使用

```csharp
using SqlSugar;
using SqlSugar.IOC;

// 默认库（列表第一项 / IOC 当前库）
var products = await DbScoped.SugarScope.Queryable<Product>()
    .Where(x => x.IsDelete == false)
    .ToListAsync();

// 指定库
var logDb = DbScoped.SugarScope.GetConnection("log");
await logDb.Insertable(logRow).ExecuteCommandAsync();

// 多库事务：事务挂在父级 Scope，操作走子连接
try
{
    var main = DbScoped.SugarScope.GetConnection("main");
    var log = DbScoped.SugarScope.GetConnection("log");
    DbScoped.SugarScope.BeginTran();
    await main.Insertable(order).ExecuteCommandAsync();
    await log.Insertable(audit).ExecuteCommandAsync();
    DbScoped.SugarScope.CommitTran();
}
catch
{
    DbScoped.SugarScope.RollbackTran();
    throw;
}
```

也可构造函数注入 `ISqlSugarClient`（单库场景更直观；多库请用 `AsTenant()` / `GetConnection`，见 [SqlSugar 多租户文档](https://www.donet5.com/Doc/1/2246)）。

> 同一批业务数据请统一走 SqlSugar；缓存热点另见 [DuMes.Component.FusionCache](https://github.com/ameizei/DuMes.Component.FusionCache)。改库后若已缓存，业务侧自行 `IFusionCache.RemoveAsync`。

## 适用场景

| 需求 | 建议 |
|------|------|
| 业务表 CRUD、事务、分页、复杂 SQL | 本组件 + SqlSugar |
| 多库（主数据 / 日志库等） | 多个 `Connections` + `GetConnection` |
| 读多写少热点（字典、组织树） | `IFusionCache`（L1 ± L2），回源仍用本组件查库 |
| 采集实时状态、队列 | `CSRedisClient`，不要硬套 DB 缓存层 |
| 换库类型 | 改 `Connections[].DbType`，并引用对应驱动（当前包默认带 `Npgsql`） |

**一句话**：持久化与事务走本组件；「读的人也会回源」的热点走 FusionCache；采集写 / 网页读类状态走 Redis。

## 注意事项

1. **`DbType` 可配，默认 PostgreSQL**：省略时按 `IocDbType.PostgreSQL`；改其它类型须补齐驱动包。
2. **主键 ULID**：插入前 `Ulid.NewUlid()`；不要混用雪花 `long` 主键策略。
3. **先 Serilog 再 Database（强制）**：本组件已依赖 `DuMes.Component.Serilog`；宿主须 `UseComponentSerilog()`，否则 `LogDebug` / `Write*` 无法按约定输出。
4. **ConfigId 唯一**：同一 `Connections`（含从库）内忽略大小写重复则启动失败。
5. **密钥**：连接串勿提交真实密码；用 Development / Production 分文件 + Secrets。
6. **时间**：审计字段写入 `DateTime.Now`（本地时；暂无异地部署）。
7. **命名（PostgreSQL）**：库内表/列必须小写；组合词必须 `snake_case`（`xxx_xxx`）。写 `[SugarColumn]` 时 **`ColumnName` 必填**。
8. **SQL 日志**：无 `EnableSqlDebugLog`；Development → `LogDebug` 进调试窗口；各环境慢 SQL / 错误用 `Write*` 落盘；Production 无全量 SQL。
9. **SqlSugar 能力**：CodeFirst、分表、仓储等以 [官方文档](https://www.donet5.com/Home/Doc) 为准；本组件负责注册、校验与 AOP。

## 引用

项目引用或 NuGet 引用本组件即可。传递引入 `DuMes.Component.Serilog`、`SqlSugar.IOC`、`SqlSugarCoreNoDrive`、`Npgsql`、`Ulid`。

宿主须配置 `builder.Host.UseComponentSerilog()`（见 [Serilog README](https://github.com/ameizei/DuMes.Component.Serilog)）。

```csharp
using DuMes.Component.Database.DependencyInjection;
using DuMes.Component.Database.Options;
using DuMes.Component.Serilog; // Write*
using SqlSugar.IOC; // DbScoped
```

## 测试工程

| 工程 | 说明 |
|------|------|
| `TestConsole` | 控制台：多架构 ConfigId（`system` / `demo`）导航、增删改查、分页、多库事务、ULID |
| `TestWebApi` | （待补）WebAPI：演示 CRUD 与多库 |
| `TestWorkerService` | （待补）Worker：后台任务写库 |

`TestConsole` 在 `appsettings.Development.json` 配置连接；`ConfigId` 可按架构名（如 `system`、`demo`）区分同一库下不同 `searchpath`。

```bash
dotnet run --project TestConsole
```

## 相关组件

| 组件 | 说明 |
|------|------|
| [DuMes.Component.Serilog](https://github.com/ameizei/DuMes.Component.Serilog) | 日志管道 |
| [DuMes.Component.FusionCache](https://github.com/ameizei/DuMes.Component.FusionCache) | L1/L2 缓存与业务 Redis |
| [DuMes.Component.I18N](https://github.com/ameizei/DuMes.Component.I18N) | 多语言 |
