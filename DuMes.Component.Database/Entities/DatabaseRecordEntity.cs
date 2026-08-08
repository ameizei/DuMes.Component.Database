using SqlSugar;

namespace DuMes.Component.Database.Entities;

/// <summary>
///     DDD 记录型实体：只追加、不改领域状态的行（审计 / 日志 / record）。
///     在身份上增加创建戳；<c>IsOnlyIgnoreUpdate</c> 保证插入后不可经 Updateable 改写。
///     创建人/时间本身不进字段级 <c>changes</c>（见 <see cref="DatabaseAuditIgnoreAttribute"/>）。
/// </summary>
public abstract class DatabaseRecordEntity : DatabaseEntity
{
    /// <summary>创建人 Id（可无）。插入后不可经 Updateable 修改。</summary>
    [SugarColumn(ColumnName = "creator_id", Length = 26, IsNullable = true, IsOnlyIgnoreUpdate = true)]
    [DatabaseAuditIgnore]
    public Ulid? CreatorId { get; set; }

    /// <summary>创建时间（本地 <c>DateTime.Now</c>）。插入后不可经 Updateable 修改。</summary>
    [SugarColumn(ColumnName = "creation_time", IsOnlyIgnoreUpdate = true)]
    [DatabaseAuditIgnore]
    public DateTime CreationTime { get; set; }
}
