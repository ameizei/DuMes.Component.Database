# DuMes.Component.Database

多脉数据库组件：基于 [SqlSugar](https://www.donet5.com/Home/Doc) + [SqlSugar.IOC](https://www.donet5.com/Doc/1/2247) 封装数据库访问，提供统一 DI 注册、多库 / 读写分离、**ULID 主键**约定、SQL AOP（慢查询与错误落盘）。**已依赖** [DuMes.Component.Serilog](https://github.com/ameizei/DuMes.Component.Serilog)，AOP 走同一 `ILogger` 管道（`LogDebug` / `Write*`）。

> **`DbType` 可配置**（`IocDbType`），**当前默认 `PostgreSQL`**。默认驱动：`Npgsql` + `SqlSugarCoreNoDrive`。主键使用 [Ulid](https://www.nuget.org/packages/Ulid)（`Ulid.NewUlid()`）。改用其它库类型时需自行补充对应驱动包。

## 项目结构

```text
DuMes.Component.Database/
├── DependencyInjection/     # AddComponentDatabase
├── Options/                 # DatabaseComponentOptions、DatabaseConnectionOptions
├── Entities/                # DatabaseEntity / Record / Business、NewId / Set / By / At / Touch / SoftDelete
├── Audit/                   # 统一表 log_audit、DatabaseAuditBuilder / DatabaseAuditRef
├── CodeFirst/               # [CodeFirst]/Tenant/Group/Partition/Inherit/Jsonb/Vector/Coordinate、GetEntityTypes、InitTables
├── Serialization/           # System.Text.Json（IsJson / ISerializeService / DatabaseJsonOptions）
├── Converters/              # Ulid / Vector / Coordinate 表列转换（命名空间 SqlSugar.DbConvert）
└── Internal/
    ├── Aop/                 # 序列化挂载 / SQL AOP / 启动建库与架构 / Warmup
    ├── Config/              # ConfigId 注册与忽略大小写解析
    └── Postgres/            # PG 系判定、pgvector、分区/继承 DDL、B-tree/jsonb/向量索引补建
```

## 分工

| 组件 | 职责 |
|------|------|
| SqlSugar / SqlSugar.IOC | ORM、多库 `ConfigId`、`DbScoped.SugarScope` |
| Npgsql | 默认 `PostgreSQL` 时的 ADO.NET 驱动（NoDrive 包需显式引用） |
| Pgvector | `vector` 类型与 Npgsql 映射（embedding / 坐标列） |
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
| 启动建库 / 架构 | `IHostedService`：`CreateDatabase` + PG `searchpath` schema（固定开启） |
| CodeFirst | 实体须标 `[CodeFirst]`（表明非 DbFirst）+ `[Tenant]`/`[DatabaseGroup]` + `[SugarTable]`；业务侧调用 `InitTables` / `GetEntityTypes`（Warmup **不**扫业务程序集） |
| 自动关连接 | 注册时固定 `IsAutoCloseConnection=true`；特殊场景可在业务侧自建 `SqlSugarClient` |
| AOP | 经 `ILogger` + Serilog：`LogDebug` / `WriteWarning` / `WriteError`（见「组件内置行为」） |

主键由业务在插入前赋值：推荐继承 `DatabaseEntity` 后 `.NewId()`，或手写 `entity.Id = Ulid.NewUlid()`（不依赖雪花 `DatacenterId` / `WorkId`）。

## 配置说明

### 配置项一览

| 配置项 | 类型 | 默认值 | 必填 | 说明 |
|--------|------|--------|------|------|
| `Connections` | array | — | 是 | 至少一个连接；见下表 |
| `SlowSqlSeconds` | double | `1` | 否 | 超过该秒数记慢 SQL；须 `> 0` |
| `AuditConfigIds` | string[] | `[]` | 否 | 统一审计表 `log_audit` 建在哪些 **主库** `ConfigId` 上；空则默认第一连接。SaaS 建议如 `["record"]` |

#### `Connections[]`

| 配置项 | 类型 | 默认值 | 必填 | 说明 |
|--------|------|--------|------|------|
| `ConfigId` | string | — | 是 | 多库标识；同一列表内忽略大小写唯一 |
| `ConnectionString` | string | — | 是 | 连接串（格式随 `DbType`）；空则启动报错 |
| `DbType` | `IocDbType` | `PostgreSQL` | 否 | 数据库类型，见 [IocDbType](https://www.donet5.com/Doc/1/2247)；**当前默认 PostgreSQL**；从库未写时继承主库 |
| `Slaves` | array | `[]` | 否 | 从库列表；项结构同连接（须有 `ConfigId` + `ConnectionString`）。仅读写分离；**不参与** Warmup 建库/建表、`AuditConfigIds`、`[Tenant]` 路由 |

### 组件内置行为（不可配置项）

日志一律走 [DuMes.Component.Serilog](https://github.com/ameizei/DuMes.Component.Serilog)（宿主已 `UseComponentSerilog`）。无 `EnableSqlDebugLog`；分流由 Serilog 环境与 `Write*` 约定决定：

| 行为 | 说明 |
|------|------|
| 配置校验 | 启动时 `Validate()`：节存在、连接非空、`ConfigId` 唯一（含从库）、`DbType` 合法；`AuditConfigIds` 须为主库 ConfigId |
| 自动建库 / 架构 | 固定开启：仅对**主库** `CreateDatabase()`；建架构按 `DbType` 选 SQL——PG 系（含人大金仓/OpenGauss 等）`CREATE SCHEMA IF NOT EXISTS`（读 `searchpath`）；SQL Server 查 `sys.schemas` 后建（架构名取 `ConfigId`）；MySQL / Sqlite / Oracle 等**不支持独立架构**则跳过。达梦 `Dm` 仅尝试 PG 方言建 SCHEMA，**不**纳入分区/向量/小写 MoreSettings。见 [库表管理](https://www.donet5.com/Home/Doc?typeId=1203) |
| 统一审计表 | Warmup 仅在 `AuditConfigIds`（默认第一连接）上建 `log_audit`（直接 `InitTables(db, typeof(DatabaseAuditRecord))`，审计实体无 Tenant，**不会**被业务 `InitTables(assembly)` 扫到）；写入请 `GetConnection(auditConfigId)` |
| CodeFirst | 扫描建表须同时具备：`[CodeFirst]`（非 DbFirst）+ `[SugarTable]` + `[Tenant("configId")]`（优先；兼容 `[DatabaseGroup]`）。无 `[CodeFirst]` 的表实体不参与 InitTables。另有 `GetEntityTypes` / `GetEntityTypesByGroup` / `InitTables(groupName, assembly)`。`QueryableWithAttr` 仍只依赖 Tenant。见 [CodeFirst](https://www.donet5.com/Home/Doc?typeId=1206)、[多租户](https://www.donet5.com/Doc/1/2246) |
| PG 分区表 | `[DatabasePartition(Grain)]` + `[DatabasePartitionField]`；`Grain`：`Year` / `Quarter` / `Month` / `Day`；默认 `AheadCount=3`、`PastCount=1`（以 `DateTime.Now` 为锚，只建该窗口内子分区，**不**自动补历史窗外）。InitTables 建父表/`PARTITION BY RANGE`/子分区。DDL 走连接当前 `search_path`。**已存在时对比实体增删列**（有数据亦可：对父表 `ADD`/`DROP COLUMN` 级联子分区；分区键不可删；新增非空列尽量带 DEFAULT，部分类型可能无法生成）。`[SugarIndex]` 建在**父表**。非 SqlSugar SplitTable |
| PG 继承表 | 子实体 C# 继承父实体并标 `[DatabaseInherit]`；InitTables 建父表后 `CREATE TABLE child (本地列…) INHERITS (parent)`（子表**不**单独写 PRIMARY KEY，身份列在父表）。子类只声明本地列；父列变更由父实体同步。`[SugarIndex]` / `[DatabaseJsonbIndex]` / `[DatabaseVectorIndex]` **不随 INHERITS 传播**（索引特性 `Inherited=false`，子类须各自声明）。与分区表互斥。见 [表继承](https://www.postgresql.org/docs/current/ddl-inherit.html) |
| PG jsonb GIN | `[DatabaseJsonbIndex("ix_{table}_x", nameof(Prop))]` → `USING GIN (col jsonb_path_ops)`（默认）；`Ops` 可选 `jsonb_ops`。普通/分区/继承表 InitTables 时补建。`[SugarIndex]` 仅 B-tree，不能代替。见 [JSON Indexing](https://www.postgresql.org/docs/current/datatype-json.html#JSON-INDEXING) |
| PG 向量列 | `[DatabaseVector(n)]` 标在 `float[]` / `Pgvector.Vector` 上 → 列类型 `vector(n)`；启动时 `CREATE EXTENSION IF NOT EXISTS vector` + Npgsql `UseVector`（库须已支持 pgvector；扩展失败仅 Warning）。**非 PG 系**实体若标了向量/坐标特性，配置阶段即抛错。见 [pgvector](https://github.com/pgvector/pgvector) |
| PG 坐标列 | `[DatabaseCoordinate(2|3)]` + 属性类型 `DatabaseCoordinate` → `vector(2)` / `vector(3)`（与 embedding 共用 pgvector，语义为货位/位姿）。内存距离 `DatabaseCoordinate.Distance`；SQL 近邻 `ORDER BY col <-> @q::vector` |
| PG 向量索引 | `[DatabaseVectorIndex("ix_{table}_x", nameof(Prop))]` → 默认 `USING hnsw (col vector_l2_ops)`；可选 IVFFlat、`Cosine`/`InnerProduct`，以及属性 `M`/`EfConstruction`/`Lists`（`0`=不写 WITH）。embedding 与坐标列通用。见 [Indexing](https://github.com/pgvector/pgvector#indexing) |
| `IsAutoCloseConnection` | 固定 `true`。若业务必须手动管连接，请在业务逻辑中单独 `new SqlSugarClient(...)` |
| `PgSqlIsAutoToLower` | PostgreSQL 时固定 `true`（含 CodeFirst）；实体列须显式 `ColumnName`（见「命名约定」） |
| 全量 SQL | `OnLogExecuting` → `ILogger.LogDebug`（赋值后 SQL）。**仅 Development** 会进**调试窗口**（Serilog：Development 最低 Debug；其它环境最低 Information，故 Production **不会**打全量 SQL） |
| 慢 SQL | `OnLogExecuted`：耗时 ≥ `SlowSqlSeconds` → `WriteWarning("sql_slow", …)` → `logs/sql_slow.log`（各环境均落盘） |
| SQL 错误 | `OnError` → `WriteError("sql_error", …)`（可带异常）→ `logs/sql_error.log`（含赋值后 SQL / 参数摘要，**不含连接串**；各环境均落盘） |
| 与 MEL 区别 | `LogDebug` **不落盘**；只有 `WriteWarning` / `WriteError` 写文件（见 Serilog README） |
| Ulid 映射 | `EntityService` 全局挂载 `UlidTypeConverter` → `varchar(26)`；`OnExecutingChangeSql` 将参数中的 `Ulid` 转字符串（覆盖 InsertNav 等绕过转换器的路径） |
| 枚举映射 | `EntityService` 全局挂载 SqlSugar `EnumToStringConvert` → 库中存**枚举名**（仅**表列**） |
| IsJson | 表列 `IsJson=true` + `ColumnDataType=jsonb`；经 `DatabaseSerializeService` / `DatabaseJsonOptions`（System.Text.Json）序列化：驼峰、枚举名、Ulid 字符串、`DateTime`=`yyyy-MM-dd HH:mm:ss` |
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
    "SlowSqlSeconds": 1,

    // 审计表落点（须为下方 Connections 的主库 ConfigId）
    // SaaS / PG 多架构示例：main + system + record → 审计只建在 record
    // 无独立架构时：两个物理库也写成两个 ConfigId，此处填审计库
    "AuditConfigIds": [ "record" ],

    "Connections": [
      {
        "ConfigId": "main",
        "DbType": "PostgreSQL",
        "ConnectionString": "Host=127.0.0.1;Port=5432;Database=dumes;SearchPath=main;Username=postgres;Password=your-password"
      },
      {
        "ConfigId": "system",
        "ConnectionString": "Host=127.0.0.1;Port=5432;Database=dumes;SearchPath=system;Username=postgres;Password=your-password"
      },
      {
        "ConfigId": "record",
        "ConnectionString": "Host=127.0.0.1;Port=5432;Database=dumes;SearchPath=record;Username=postgres;Password=your-password"
        // 无架构时改为独立库，例如 Database=dumes_record
      }
    ]
  }
}
```

### 代码配置

```csharp
builder.Services.AddComponentDatabase(o =>
{
    o.SlowSqlSeconds = 1;
    o.AuditConfigIds = ["record"]; // 审计表只建在 record
    o.Connections =
    [
        new DatabaseConnectionOptions
        {
            ConfigId = "main",
            DbType = IocDbType.PostgreSQL,
            ConnectionString = "Host=127.0.0.1;Port=5432;Database=dumes;SearchPath=main;Username=postgres;Password=your-password"
        },
        new DatabaseConnectionOptions
        {
            ConfigId = "record",
            ConnectionString = "Host=127.0.0.1;Port=5432;Database=dumes;SearchPath=record;Username=postgres;Password=your-password"
        }
    ];
});
```

CodeFirst（实体特性 + 业务侧调用）：

```csharp
using DuMes.Component.Database.CodeFirst;
using SqlSugar;

[SugarTable("product")]
[CodeFirst] // 声明为 CodeFirst 表；无此标记不参与扫描建表（DbFirst 表勿标）
[Tenant("main")] // = ConfigId；InitTables + QueryableWithAttr
public class Product { /* ... */ }

[SugarTable("audit_log")]
[CodeFirst]
[Tenant("demo")]
public class AuditLog { /* ... */ }

// PostgreSQL 分区表（Grain: Year/Quarter/Month/Day；[SugarIndex] 建在父表）
[SugarTable("order_log")]
[CodeFirst]
[Tenant("main")]
[DatabasePartition(DatabasePartitionGrain.Month, AheadCount = 3, PastCount = 1)]
[SugarIndex("ix_{table}_ctime", nameof(CreateTime), OrderByType.Desc)]
public class OrderLog
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "create_time")]
    [DatabasePartitionField]
    public DateTime CreateTime { get; set; }
}

// PostgreSQL 继承表（C# 继承镜像 INHERITS；与分区表互斥；子表无独立 PK；索引各自声明）
[SugarTable("vehicle")]
[CodeFirst]
[Tenant("main")]
[SugarIndex("ix_{table}_name", nameof(Name), OrderByType.Asc)]
public class Vehicle
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "name", Length = 64)]
    public string Name { get; set; } = "";
}

[SugarTable("car")]
[CodeFirst]
[Tenant("main")]
[DatabaseInherit]
[SugarIndex("ix_{table}_doors", nameof(Doors), OrderByType.Asc)] // INHERITS 不传播；须在子实体重声明
public class Car : Vehicle
{
    [SugarColumn(ColumnName = "doors")]
    public int Doors { get; set; }
}

// 多层：ElectricCar → Car → Vehicle
[SugarTable("electric_car")]
[CodeFirst]
[Tenant("main")]
[DatabaseInherit]
public class ElectricCar : Car
{
    [SugarColumn(ColumnName = "battery_kwh", Length = 10, DecimalDigits = 2)]
    public decimal BatteryKwh { get; set; }
}

// jsonb GIN（默认 jsonb_path_ops，适合 @>；产线 material_barcode_list 同类）
[SugarTable("product_info")]
[CodeFirst]
[Tenant("main")]
[DatabaseJsonbIndex("ix_{table}_mblist", nameof(MaterialBarcodeList))]
// → CREATE INDEX … USING GIN (material_barcode_list jsonb_path_ops)
public class ProductInfo
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "material_barcode_list", IsJson = true, ColumnDataType = "jsonb", IsNullable = true)]
    public List<object> MaterialBarcodeList { get; set; } // 业务可换成强类型 DTO
}

// PostgreSQL pgvector（embedding）+ HNSW 近邻索引
[SugarTable("doc_embedding")]
[CodeFirst]
[Tenant("main")]
[DatabaseVectorIndex("ix_{table}_embedding", nameof(Embedding))]
// → CREATE INDEX … USING hnsw (embedding vector_l2_ops)
// 余弦 / 调参示例：
// [DatabaseVectorIndex("ix_{table}_embedding", nameof(Embedding),
//     DatabaseVectorIndexMethod.Hnsw, DatabaseVectorIndexOps.Cosine,
//     M = 16, EfConstruction = 64)]
// IVFFlat：Method=Ivfflat + Lists≈rows/1000；查询运算符须与 Ops 一致
public class DocEmbedding
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "embedding")]
    [DatabaseVector(1536)] // → vector(1536)
    public float[] Embedding { get; set; }
}

// 仓库坐标（2D / 3D）+ HNSW（与 embedding 同一套 [DatabaseVectorIndex]）
[SugarTable("wh_location")]
[CodeFirst]
[Tenant("main")]
[DatabaseVectorIndex("ix_{table}_slot_xy", nameof(SlotXy))]
public class WhLocation
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "slot_xy")]
    [DatabaseCoordinate(2)]
    public DatabaseCoordinate SlotXy { get; set; }

    [SugarColumn(ColumnName = "slot_xyz", IsNullable = true)]
    [DatabaseCoordinate(3)]
    public DatabaseCoordinate SlotXyz { get; set; }
}

// var d = DatabaseCoordinate.Distance(a.SlotXy, b.SlotXy);
// SQL：ORDER BY slot_xy <-> '[0,0]'::vector

// 扫描 / 建表（按 Tenant → ConfigId）
var types = DatabaseCodeFirst.GetEntityTypes(typeof(Product).Assembly);
var byGroup = DatabaseCodeFirst.GetEntityTypesByGroup(typeof(Product).Assembly);
var map = DatabaseCodeFirst.InitTables(typeof(Product).Assembly);
// 只建某一 ConfigId：DatabaseCodeFirst.InitTables("main", typeof(Product).Assembly);

// 按特性切库 CRUD（SqlSugar 多租户）
var list = await DbScoped.SugarScope.QueryableWithAttr<Product>().ToListAsync();
await DbScoped.SugarScope.InsertableWithAttr(row).ExecuteCommandAsync();
await DbScoped.SugarScope.UpdateableWithAttr(row).ExecuteCommandAsync();
await DbScoped.SugarScope.DeleteableWithAttr<Product>().Where(...).ExecuteCommandAsync();
var db = DbScoped.SugarScope.GetConnectionWithAttr<Product>();
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
using DuMes.Component.Database.CodeFirst;
using DuMes.Component.Database.Entities;
using SqlSugar;

[SugarTable("product")]
[CodeFirst]
[Tenant("main")]
public class Product : DatabaseEntity // 基类已含 Id（列 id）；也可不继承、自行声明主键
{
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

## 字段审计（统一表 `log_audit`）

组件内置统一审计表实体 [`DatabaseAuditRecord`](DuMes.Component.Database/Audit/DatabaseAuditRecord.cs)（继承 [`DatabaseRecordEntity`](DuMes.Component.Database/Entities/DatabaseRecordEntity.cs)）与构造器 [`DatabaseAuditBuilder`](DuMes.Component.Database/Audit/DatabaseAuditBuilder.cs)。Warmup 按 **`AuditConfigIds`** 建表（默认第一连接；SaaS 请显式指向 `record` 等）。写入须用对应连接，例如 `GetConnection("record")`。

| 列 | 说明 |
|------|------|
| `id` | 本条审计主键（`DatabaseEntity`） |
| `creator_id` / `creation_time` | 操作人与时间（`DatabaseRecordEntity`） |
| `entity_name` / `entity_id` | 被修改的业务行（类型名 + 行 Id） |
| `action` | `Create` / `Update` / `Delete` |
| `changes` | jsonb：字段改前/改后列表 |

`changes[].kind`：`Scalar` / `Nested` / `Image` / `Icon`（后两者供前台按图/图标展示）/ `List`（含 `added`/`removed`）。

组件 API（推荐直接用）：`For` → `By` / `At` → `Scalar` / `Nested` / `Image` / `Icon` / `List` / `Change` → `Build`；`HasChanges` 为空则不写库。

```csharp
var fromDb = await db.Queryable<Product>().InSingleAsync(id);
var builder = DatabaseAuditBuilder.For(nameof(Product), fromDb.Id, "Update")
    .By(userId);

if (fromDb.Name != fromUi.Name)
{
    builder.Scalar("Name", fromDb.Name, fromUi.Name, label: "名称");
    fromDb.Name = fromUi.Name;
}

if (!builder.HasChanges)
    return; // 未改任何字段：不 Update、不插审计

await db.Updateable(fromDb).ExecuteCommandAsync();

// 审计写入 AuditConfigIds 对应连接（勿写到业务库）
var auditDb = DbScoped.SugarScope.GetConnection("record");
await auditDb.Insertable(builder.Build()).ExecuteCommandAsync();

var logs = await auditDb.Queryable<DatabaseAuditRecord>()
    .Where(x => x.EntityId == fromDb.Id)
    .ToListAsync();
```

> **`WithAudit(...).SetXxx(前,后)` 不是组件 API**：由业务实体自建包装类（见 `TestConsole` 的 `DemoProduct` / `DemoUser` / `DemoStation`），内部仍调用 `builder.Scalar` / `List`。组件**不提供**整对象万能 Diff。

前台 JSON 形态示例（驼峰，与组件 `DatabaseJsonOptions` 一致；`creationTime` 格式为 `yyyy-MM-dd HH:mm:ss`）：

```json
{
  "id": "...",
  "entityName": "Station",
  "entityId": "...",
  "action": "Update",
  "creatorId": "...",
  "creationTime": "2026-08-08 21:00:00",
  "changes": [
    { "path": "Name", "label": "工站名称", "kind": "Scalar", "before": "ST-01", "after": "ST-02" },
    { "path": "PLC.Name", "label": "PLC名称", "kind": "Nested", "before": "Siemens-S7", "after": "Omron-NJ" },
    {
      "path": "RoleIds",
      "label": "角色",
      "kind": "List",
      "before": [
        { "id": "…", "name": "管理员" },
        { "id": "…", "name": "操作员" },
        { "id": "…", "name": "访客" }
      ],
      "after": [
        { "id": "…", "name": "管理员" },
        { "id": "…", "name": "操作员" }
      ],
      "added": [],
      "removed": [{ "id": "…", "name": "访客" }]
    }
  ]
}
```

说明：

1. 统一表，不必再为每个业务建审计子类；按 `entity_id` 查某行的全部变更历史。
2. 字段级变更用 `DatabaseAuditBuilder`；需要链式 `SetXxx` 时在业务实体上自建包装（见上）。
3. `builder.HasChanges == false` → 直接成功，不写业务表、不写审计表。
4. `JsonDocument` 多态配置：库列统一 `JsonDocument`，按品牌转实体后再记 Nested/Scalar（见 `DemoStation` / `SiemensPlcConfig`）。
5. **多对多 / 外键只存 Id 时**：业务列仍只存 Id；**审计建议写时快照** `DatabaseAuditRef`（`Of` / `FromIds`：`id` + 当时 `name`，可选 `extra`）。前台直接展示，不必再查；角色改名/删除后历史仍正确。后期按 Id 反查只能看到「当前」名称，且可能已查不到。见 `DemoUser`。

```csharp
// 业务列写 Id；审计写带名称的 DatabaseAuditRef 快照（查字典/缓存即可）
var beforeRoles = DatabaseAuditRef.FromIds(user.RoleIds, id => roleNameMap[id]);
var afterRoles = DatabaseAuditRef.FromIds([roleA, roleB], id => roleNameMap[id]);
builder.List("RoleIds", beforeRoles, afterRoles, label: "角色");
user.RoleIds = [roleA, roleB];

if (user.ProfileId != newProfileId)
{
    builder.Scalar(
        "ProfileId",
        user.ProfileId is null ? null : DatabaseAuditRef.Of(user.ProfileId.Value, profileNameMap[user.ProfileId.Value]),
        newProfileId is null ? null : DatabaseAuditRef.Of(newProfileId.Value, profileNameMap[newProfileId.Value]),
        label: "资料卡");
    user.ProfileId = newProfileId;
}

// changes 形态（前台可读）
// "removed": [{ "id":"…", "name":"访客" }]
// "after": { "id":"…", "name":"新资料卡" }
```

## 主键：ULID 与实体基类

约定使用 `Ulid` 作为实体主键类型（Crockford Base32，26 字符；组件映射为 `varchar(26)`，列名 `id`）。

| 基类 | 列 | 适用 |
|------|-----|------|
| [`DatabaseEntity`](DuMes.Component.Database/Entities/DatabaseEntity.cs) | `id` | 仅需主键的轻量实体 |
| [`DatabaseRecordEntity`](DuMes.Component.Database/Entities/DatabaseRecordEntity.cs) | + `creator_id` / `creation_time`（`IsOnlyIgnoreUpdate`） | record / log / 审计等只追加表 |
| [`DatabaseBusinessEntity`](DuMes.Component.Database/Entities/DatabaseBusinessEntity.cs) | + `modifier_id` / `modify_time` / `delete_time` / `sort` | 可改可软删的业务主数据表 |

继承链：`DatabaseEntity` → `DatabaseRecordEntity` → `DatabaseBusinessEntity`。`DatabaseAuditRecord` 继承 `DatabaseRecordEntity`（审计行只追加）。

软删约定：`delete_time IS NULL` = 未删除；有时间 = 已删除（值为删除时刻）。未删查询：`.Where(x => x.DeleteTime == null)`（或内存侧 `!x.IsDeleted`，`IsDeleted` 为忽略列）。

推荐表实体继承基类，并用链式赋值：

```csharp
using DuMes.Component.Database.Entities;

// 业务表
var row = new Product()
    .NewId()
    .By(userId)
    .At()
    .WithSort(10)
    .Set(x => x.Name, "widget");
await DbScoped.SugarScope.Insertable(row).ExecuteCommandAsync();

row.Set(x => x.Name, "widget-v2").Touch(userId);
await DbScoped.SugarScope.Updateable(row).ExecuteCommandAsync();

row.SoftDelete(userId); // 写 delete_time；Updateable 落库
await DbScoped.SugarScope.Updateable(row).ExecuteCommandAsync();

// record / log 表
var log = new OpLog().NewId().By(userId).At();
await DbScoped.SugarScope.Insertable(log).ExecuteCommandAsync();
```

仍可手写主键、不继承基类：

```csharp
var row = new Product
{
    Id = Ulid.NewUlid(),
    Name = "widget",
    CreationTime = DateTime.Now
};
```

说明：

1. **生成**：`.NewId()` 或 `Ulid.NewUlid()`（可排序；与审计字段用 `DateTime.Now` 无关）。
2. **勿用雪花**：本组件不配置 `SnowFlakeSingle` / `DatacenterId` / `WorkId`。
3. **全局映射（Ulid）**：`EntityService` 为 `Ulid` / `Ulid?` 挂载 `UlidTypeConverter`（`varchar(26)`）；实体列不必写 `SqlParameterDbType`。
4. **全局映射（枚举）**：`EntityService` 为枚举 / 可空枚举挂载 SqlSugar 自带 `EnumToStringConvert`，库中存枚举**名称**（如 `OnSale`），非数值；列上已显式指定 `SqlParameterDbType` 时不覆盖。
5. **可空值类型**：业务上「可无」的外键可用 `Ulid?`；主键仍写 `Ulid`，插入前必须赋值。
6. **基类范围**：`DatabaseEntity` 身份；`DatabaseRecordEntity` 创建人/时间；`DatabaseBusinessEntity` 修改人/时间、软删时间、排序。`[SugarTable]` / `[CodeFirst]` / `[Tenant]` 与业务列由派生类声明。

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
2. **主键 ULID**：插入前 `.NewId()` 或 `Ulid.NewUlid()`；不要混用雪花 `long` 主键策略。推荐继承 `DatabaseEntity`。
3. **先 Serilog 再 Database（强制）**：本组件已依赖 `DuMes.Component.Serilog`；宿主须 `UseComponentSerilog()`，否则 `LogDebug` / `Write*` 无法按约定输出。
4. **ConfigId 唯一**：同一 `Connections`（含从库）内忽略大小写重复则启动失败。从库只做读写分离，勿把从库 ConfigId 用于 `[Tenant]` / `AuditConfigIds`。
5. **密钥**：连接串勿提交真实密码；用 Development / Production 分文件 + Secrets。
6. **时间**：审计字段写入 `DateTime.Now`（本地时；暂无异地部署）。JSON 时间为 `yyyy-MM-dd HH:mm:ss`。
7. **命名（PostgreSQL）**：库内表/列必须小写；组合词必须 `snake_case`（`xxx_xxx`）。写 `[SugarColumn]` 时 **`ColumnName` 必填**。
8. **SQL 日志**：无 `EnableSqlDebugLog`；Development → `LogDebug` 进调试窗口；各环境慢 SQL / 错误用 `Write*` 落盘；Production 无全量 SQL。
9. **自动建库 / 架构**：固定开启（仅主库）；建架构随 `DbType` 分支（不支持则跳过）。账户须有建库 / 建 schema 权限。
10. **CodeFirst / Tenant**：扫描建表须 `[CodeFirst]` + `[SugarTable]` + `[Tenant]`（或 `[DatabaseGroup]`）；DbFirst 表不要标 `[CodeFirst]`。可用 `GetEntityTypes` / `InitTables(groupName, …)`。业务用 `QueryableWithAttr<T>()` 等切库。
11. **PG 分区表**：`[DatabasePartition(Grain)]` + `[DatabasePartitionField]`（`Year`/`Quarter`/`Month`/`Day`）；主键含分区列。窗口外子分区不会自动补建，长期服务请定期 `InitTables` 或自行扩窗。有数据时仍可对**父表**增删列（[官方分区说明](https://www.postgresql.org/docs/current/ddl-partitioning.html) / [ALTER TABLE](https://www.postgresql.org/docs/current/sql-altertable.html)），子分区自动对齐；勿在子分区上单独改列。分区键禁止删除。`[SugarIndex]` 建在父表；**UNIQUE 索引须包含分区键**（PG 约束）。
12. **PG 继承表**：子实体须 C# 继承父实体并标 `[DatabaseInherit]`（勿与分区表混用）。子类只写本地列；子表 DDL 不单独建 PK。查父表默认含子集（`ONLY parent` 可排除）。`[SugarIndex]` / jsonb·向量索引特性不继承，父/子各自声明。见 [表继承](https://www.postgresql.org/docs/current/ddl-inherit.html)。
13. **PG jsonb GIN**：`[DatabaseJsonbIndex]` 默认 `jsonb_path_ops`（`@>` 包含）；需 `?` 等键操作时用 `DatabaseJsonbIndexOps.Ops`。查询须用 jsonb 运算符才走索引。
14. **PG 向量（pgvector）**：列标 `[DatabaseVector(n)]`；数据库须已支持 pgvector。非 PG 系标向量/坐标特性会在配置阶段失败。近邻查询可用 SQL 运算符 `<->` / `<=>` / `<#>`。
15. **PG 坐标**：`[DatabaseCoordinate(2|3)]` + `DatabaseCoordinate`；落库 `vector(2|3)`。WMS 货位距离用 `DatabaseCoordinate.Distance` 或 SQL `<->`。
16. **PG 向量索引**：`[DatabaseVectorIndex]` 默认 HNSW + L2；查询运算符须与 `Ops` 一致（L2=`<->`，Cosine=`<=>`，IP=`<#>`）。IVFFlat 适合已有数据后再建，并设合理 `Lists`。
17. **SqlSugar 能力**：分表、仓储等以 [官方文档](https://www.donet5.com/Home/Doc) 为准；本组件负责注册、校验、AOP、建库/架构与 CodeFirst / 分区 / 继承 / jsonb·向量索引 / 向量 / 坐标封装。
18. **字段审计**：`AuditConfigIds` 指定建表/写入连接；`DatabaseAuditRecord` 继承 `DatabaseRecordEntity`（`creator_id` / `creation_time`）；组件用 `DatabaseAuditBuilder.Scalar/List/...`；`HasChanges` 空则不写库。`WithAudit.SetXxx` 为业务包装示例，见 `TestConsole`。
19. **实体基类分层**：日志/审计继承 `DatabaseRecordEntity`（`.By` / `.At`）；业务主数据继承 `DatabaseBusinessEntity`（再加 `.Touch` / `.SoftDelete` / `.Restore` / `.WithSort`）；`delete_time` 空=未删、有值=已删。

## 引用

项目引用或 NuGet 引用本组件即可。传递引入 `DuMes.Component.Serilog`、`SqlSugar.IOC`、`SqlSugarCoreNoDrive`、`Npgsql`、`Pgvector`、`Ulid`。

宿主须配置 `builder.Host.UseComponentSerilog()`（见 [Serilog README](https://github.com/ameizei/DuMes.Component.Serilog)）。

```csharp
using DuMes.Component.Database.Audit; // DatabaseAuditRecord / DatabaseAuditBuilder / DatabaseAuditRef
using DuMes.Component.Database.CodeFirst; // InitTables / GetEntityTypes / 分区·向量等特性
using DuMes.Component.Database.DependencyInjection;
using DuMes.Component.Database.Entities; // DatabaseEntity / Record / Business / NewId / Set / By / At / Touch / SoftDelete
using DuMes.Component.Database.Options;
using DuMes.Component.Database.Serialization; // DatabaseJsonOptions（可选复用）
using DuMes.Component.Serilog; // Write*
using SqlSugar.IOC; // DbScoped
```

## 测试工程

| 工程 | 说明 |
|------|------|
| `TestConsole` | 场景：`Crud` / `MultiDb` / `Navigate` / `Partition` / `Inherit` / `Vector` / `Coordinate` / `Audit` |
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
