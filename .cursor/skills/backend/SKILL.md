---
name: backend
description: >-
  DuMes 后端通用约定（本仓库为 DuMes.Component.Database / .NET /
  dotnet build/restore/test/run）。
  Use when building, compiling, restoring, testing, or running this component,
  .csproj, .sln/.slnx, ASP.NET Core host samples, or any backend C# work that
  needs project-wide rules (sandbox, nullable, DateTime.Now, config, docs). For
  FusionCache / Redis / L1-L2 read backend-cache. Database 细节以本仓库 README
  为准（组件写完后再补 backend-database）。When unsure how a DuMes component
  works, prefer its GitHub README / SqlSugar official docs before guessing.
---

# DuMes 后端 Skill（通用）

本仓库是 **数据库组件**（[DuMes.Component.Database](https://github.com/ameizei/DuMes.Component.Database)）。组件写完前，**Database 专用约定以本仓库 README 为准**；本 skill 只保留通用规则。

| Skill | 路径 | 覆盖 |
|-------|------|------|
| **backend-cache** | `.cursor/skills/backend-cache/SKILL.md` | FusionCache L1/L2 选型、何时直接 Redis |
| ~~backend-database~~ | （暂无） | 组件实现后再补 |

## 编译与命令执行（强制）

**编译时不要用沙箱编译，会卡住。**

执行以下命令时，Shell 必须申请 `required_permissions: ["all"]`（关闭沙箱），禁止在默认沙箱里跑：

- `dotnet restore` / `dotnet build` / `dotnet publish`
- `dotnet test` / `dotnet run`
- 任何会触发 MSBuild / NuGet 还原的命令

```text
正确：Shell + required_permissions: ["all"]
错误：默认沙箱执行 dotnet build（会卡住或极慢）
```

## 范围

- 主要代码：`DuMes.Component.Database/`
- 示例 / 测试：`TestConsole`、`TestWebApi`、`TestWorkerService`（若已添加）
- 兄弟组件：`DuMes.Component.*`（Serilog、FusionCache、I18N 等）

## 禁止项（强制）

- **不要**在 `.csproj` 开启可空引用（勿加 `<Nullable>enable</Nullable>` / `annotations`；与现有模块一致，可空相关已删掉）。
- **不要**给 `string` 或其它引用类型加 `?`（禁止 `string?`、`List<string>?` 等）。需要表示「可无」时仍写 `string` / 引用类型，用 `null`、空串或默认值表达，不要用可空引用标注。
- 可空**值类型**（如 `int?`、`DateTime?`、`Ulid?`）按业务需要仍可使用，与上条无关。

```csharp
// 正确
public string ConnectionString { get; set; }
public string ConfigId { get; set; } = "main";

// 错误
public string? ConnectionString { get; set; }
```

## 组件用法不明时优先查文档（强制）

**不要凭记忆或猜测组件 API。** 对本 skill / 子 skill 未写清、或本地代码不足以判断的用法，先查对应仓库 README / 官方文档，再写代码。

| 组件 | 文档入口 |
|------|----------|
| **Database**（本仓库） | https://github.com/ameizei/DuMes.Component.Database （README） |
| **FusionCache**（L1/L2 缓存） | https://github.com/ameizei/DuMes.Component.FusionCache |
| **I18N**（多语言） | https://github.com/ameizei/DuMes.Component.I18N |
| **Serilog**（日志管道） | https://github.com/ameizei/DuMes.Component.Serilog |
| **SqlSugar**（ORM 官方能力） | https://www.donet5.com/Home/Doc |

约定：

1. 查 GitHub：用浏览器或 `gh`/WebFetch 读 README 与示例项目，对齐注册顺序、配置节。
2. SqlSugar 官方能力以 [果糖文档](https://www.donet5.com/Home/Doc) 为准；本仓库封装以 **README** 为准（`backend-database` 待组件写完后补）。
3. 文档与 skill 冲突时：通用规则以本 skill 为准；组件安装/配置细节以对应 GitHub README 为准。

## 配置约定（简要）

- 环境相关配置（如 `Database`、`SerilogComponent`、`FusionCache`）写在 `appsettings.Development.json` / `appsettings.Production.json`
- 不要把这类环境配置堆进 `appsettings.json`，避免只改主文件却不生效
- 连接串等密钥优先 User Secrets / 环境变量，勿提交真实密码

## 时间约定：使用本地 `DateTime.Now`（当前）

**当前不按 UTC**：存储、比较、审计字段、查询条件、API 出参一律用服务器本地时间（`DateTime.Now` / `DateTimeOffset.Now`）。暂无异地多时区部署需求。

| 场景 | 写法 |
|------|------|
| 取当前时刻 | `DateTime.Now` |
| 写入创建/修改时间 | `DateTime.Now` |
| 查询区间 | 直接用业务传入的本地起止，或 `DateTime.Now.Date` 等，**不必**再转 UTC |
| API 出参 | 原样返回库中的本地时间即可 |

```csharp
// 写入
entity.CreateTime = DateTime.Now;
entity.ModifyTime = DateTime.Now;

// 查询（本地日历日）
var start = new DateTime(2026, 8, 8);
var end = start.AddDays(1);
var list = await DbScoped.SugarScope.Queryable<Product>()
    .Where(x => x.CreateTime >= start && x.CreateTime < end)
    .ToListAsync();
```

> 若以后要异地 / 多时区部署，再统一改为 UTC 存储与查询。

## I18N / 日志（简要）

细节以组件 README 与源码为准。

- **I18N**：https://github.com/ameizei/DuMes.Component.I18N
- **日志**：本组件**已依赖** [DuMes.Component.Serilog](https://github.com/ameizei/DuMes.Component.Serilog)；宿主须 `UseComponentSerilog()`。AOP 行为见本仓库 README。
