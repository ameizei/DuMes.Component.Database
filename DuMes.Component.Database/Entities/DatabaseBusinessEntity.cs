using SqlSugar;

namespace DuMes.Component.Database.Entities;

/// <summary>
///     业务表实体基类：在 <see cref="DatabaseRecordEntity"/> 上增加修改人/时间、软删时间与排序。
///     <c>DeleteTime</c>：<c>NULL</c> = 未删除；有值 = 已删除（删除时刻）。查询未删行用 <c>DeleteTime == null</c>。
/// </summary>
public abstract class DatabaseBusinessEntity : DatabaseRecordEntity
{
    /// <summary>最近修改人 Id（可无）。</summary>
    [SugarColumn(ColumnName = "modifier_id", Length = 26, IsNullable = true)]
    public Ulid? ModifierId { get; set; }

    /// <summary>最近修改时间（可无；本地 <c>DateTime.Now</c>）。</summary>
    [SugarColumn(ColumnName = "modify_time", IsNullable = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>
    ///     删除时间（可空）。<c>NULL</c> = 未删除；有时间 = 已删除。
    /// </summary>
    [SugarColumn(ColumnName = "delete_time", IsNullable = true)]
    public DateTime? DeleteTime { get; set; }

    /// <summary>排序（升序；默认 <c>0</c>）。</summary>
    [SugarColumn(ColumnName = "sort")]
    public int Sort { get; set; }

    /// <summary>是否已删除（非库列）：<c>DeleteTime != null</c>。</summary>
    [SugarColumn(IsIgnore = true)]
    public bool IsDeleted => DeleteTime != null;
}
