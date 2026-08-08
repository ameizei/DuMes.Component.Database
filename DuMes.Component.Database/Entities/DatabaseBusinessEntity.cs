using SqlSugar;

namespace DuMes.Component.Database.Entities;

/// <summary>
///     DDD 业务聚合根基类（可持久化主数据）：在记录型创建戳之上增加生命周期与可审计领域属性。
///     <list type="bullet">
///         <item>生命周期（不进审计 <c>changes</c>）：<see cref="ModifierId"/> / <see cref="ModifyTime"/> / <see cref="DeleteTime"/>——谁改/何时改由审计行 <c>creator_id</c>/<c>creation_time</c> 表达。</item>
///         <item>领域属性（进审计 <c>changes</c>）：<see cref="Sort"/>（标 <see cref="DatabaseAuditFieldAttribute"/>）。</item>
///     </list>
///     软删：<c>DeleteTime == null</c> 未删除；有值已删除。查询未删：<c>DeleteTime == null</c>。
/// </summary>
public abstract class DatabaseBusinessEntity : DatabaseRecordEntity
{
    // ----- 生命周期（基础设施，不记字段差异）-----

    /// <summary>最近修改人 Id（可无）。不写入审计 <c>changes</c>。</summary>
    [SugarColumn(ColumnName = "modifier_id", Length = 26, IsNullable = true)]
    [DatabaseAuditIgnore]
    public Ulid? ModifierId { get; set; }

    /// <summary>最近修改时间（可无）。不写入审计 <c>changes</c>。</summary>
    [SugarColumn(ColumnName = "modify_time", IsNullable = true)]
    [DatabaseAuditIgnore]
    public DateTime? ModifyTime { get; set; }

    /// <summary>
    ///     删除时间（可空）。<c>NULL</c> = 未删除；有时间 = 已删除。
    ///     软删/恢复不记字段差异；审计行用 <c>action = Delete</c>（或业务约定）+ 行级创建戳即可。
    /// </summary>
    [SugarColumn(ColumnName = "delete_time", IsNullable = true)]
    [DatabaseAuditIgnore]
    public DateTime? DeleteTime { get; set; }

    /// <summary>是否已删除（非库列）：<c>DeleteTime != null</c>。</summary>
    [SugarColumn(IsIgnore = true)]
    [DatabaseAuditIgnore]
    public bool IsDeleted => DeleteTime != null;

    // ----- 领域属性（变更记字段差异）-----

    /// <summary>排序（升序；默认 <c>0</c>）。变更应写入审计 <c>changes</c>，见 <see cref="DatabaseBusinessEntityExtensions.ChangeSort{T}"/>。</summary>
    [SugarColumn(ColumnName = "sort")]
    [DatabaseAuditField(DatabaseAuditFieldNames.Sort)]
    public int Sort { get; set; }
}
