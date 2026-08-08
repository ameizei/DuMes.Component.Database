using DuMes.Component.Database.CodeFirst;
using DuMes.Component.Database.Entities;
using SqlSugar;

namespace DuMes.Component.Database.Audit;

/// <summary>
///     统一字段审计表 <c>log_audit</c>（命名：<c>log_</c>=日志域，与 <c>sys_</c>/<c>mes_</c>/<c>wms_</c> 等前缀并列）。
///     一次业务操作一行：被改行 Id、改前改后、操作人与操作时间。
///     Warmup 按 <c>Database:AuditConfigIds</c> 建表（默认第一连接；SaaS 建议 <c>record</c>）。
///     操作人/时间见 <see cref="DatabaseRecordEntity"/>。
/// </summary>
[CodeFirst]
[SugarTable("log_audit")]
[SugarIndex("ix_{table}_entity_id_ctime", nameof(EntityId), OrderByType.Asc, nameof(CreationTime), OrderByType.Desc)]
[SugarIndex("ix_{table}_ctime", nameof(CreationTime), OrderByType.Desc)]
public class DatabaseAuditRecord : DatabaseRecordEntity
{
    /// <summary>被修改实体类型名，如 <c>DemoProduct</c>、<c>Station</c>。</summary>
    [SugarColumn(ColumnName = "entity_name", Length = 128)]
    public string EntityName { get; set; } = string.Empty;

    /// <summary>被修改行的主键 Id。</summary>
    [SugarColumn(ColumnName = "entity_id", Length = 26)]
    public Ulid EntityId { get; set; }

    /// <summary>操作：建议 <c>Create</c> / <c>Update</c> / <c>Delete</c>。</summary>
    [SugarColumn(ColumnName = "action", Length = 32)]
    public string Action { get; set; } = string.Empty;

    /// <summary>字段级改前/改后列表（jsonb）。</summary>
    [SugarColumn(ColumnName = "changes", IsJson = true, ColumnDataType = "jsonb", IsNullable = true)]
    public List<DatabaseAuditFieldChange> Changes { get; set; } = [];
}
