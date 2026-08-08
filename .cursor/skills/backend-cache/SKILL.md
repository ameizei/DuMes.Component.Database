---
name: backend-cache
description: >-
  DuMes FusionCache / Redis 缓存约定（L1/L2 选型、Backplane、何时不用 FusionCache）。
  Use when adding or changing FusionCache, IFusionCache, Redis, L1/L2, GetOrSet,
  EnableBackplane, EnableDistributedCache, or deciding between cache and direct
  CSRedisClient. For SqlSugar / Database persistence see this repo README
  and backend skill; project-wide rules in backend.
---

# DuMes 后端 Skill（Cache）

通用规则见 `.cursor/skills/backend/SKILL.md`。持久化 / SqlSugar 见本仓库 README（`backend-database` 待组件写完后补）。

组件文档：https://github.com/ameizei/DuMes.Component.FusionCache

## 先识别场景是否适用 L1 / L2（强制）

使用 `DuMes.Component.FusionCache` 前，**必须先判断业务场景**，不要默认套 L1+L2。

| 场景特征 | 怎么用 |
|----------|--------|
| 读多写少；读侧会回源（DB/远程）；可多实例共享 | `IFusionCache`（L1 内存；需要跨进程再开 L2 Redis） |
| 多实例且改后要立刻一致 | L2 + `EnableBackplane`；否则其它节点 L1 最多等到过期才刷新 |
| 仅本进程加速、不连 Redis | `EnableDistributedCache=false`（仅 L1） |
| 一端只写、一端只读；写端才是数据源（设备状态、采集推送、队列） | **不要硬套 FusionCache**；用 `CSRedisClient` / Hash、List、Pub/Sub |

决策要点：

1. **谁回源？** 读请求会自己拉 DB/远程并回填 → 才适合 `GetOrSet` + L1（± L2）。
2. **写端是否独立？** 采集/其它服务写 Redis、本服务只读 → 直接 Redis，不要 L1/L2 混合缓存。
3. **要不要 L2？** 单实例可只用 L1；多实例共享或进程重启仍要数据 → 开 L2。
4. **同一批业务 key** 统一走 `IFusionCache`，不要再直接改 L2，以免 L1/L2 不一致；Hash/队列用独立 key。

一句话：FusionCache 留给「读的人也会回源」的数据；采集写 / 网页读类状态走 Redis。

## 与其它能力的边界

| 能力 | 放哪 | 不要混淆 |
|------|------|----------|
| FusionCache L1/L2 | 本 skill | 服务端内存 + 可选 Redis 回源缓存 |
| SqlSugar / Database | 本仓库 README + `backend` | 持久化、事务、复杂查询；回源工厂里查库 |
| CSRedis Hash/队列/PubSub | 本 skill（直接 Redis） | 写端是数据源时不要硬套 FusionCache |

## 配置

- `FusionCache` 等环境相关配置写在 `appsettings.Development.json` / `Production.json`，不要堆进主 `appsettings.json`。
- 时间与 TTL：当前用本地时 / `DateTime.Now`（见 `backend`）；缓存过期可用 `TimeSpan`，不必强行 UTC。
