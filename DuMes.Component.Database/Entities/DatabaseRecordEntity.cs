using SqlSugar;

namespace DuMes.Component.Database.Entities;

/// <summary>
///     记录 / 日志表实体基类：在 <see cref="DatabaseEntity"/> 上增加操作人与操作时间。
///     适用于 audit、record、log 等只追加、按创建信息追溯的表；派生类自行声明表映射与业务列。
///     <c>CreatorId</c> / <c>CreationTime</c> 标 <c>IsOnlyIgnoreUpdate</c>：插入可写，<c>Updateable</c> 不更新这两列。
/// </summary>
public abstract class DatabaseRecordEntity : DatabaseEntity
{
    /// <summary>操作人 Id（可无）。插入后不可经 Updateable 修改。</summary>
    [SugarColumn(ColumnName = "creator_id", Length = 26, IsNullable = true, IsOnlyIgnoreUpdate = true)]
    public Ulid? CreatorId { get; set; }

    /// <summary>操作时间（本地 <c>DateTime.Now</c>）。插入后不可经 Updateable 修改。</summary>
    [SugarColumn(ColumnName = "creation_time", IsOnlyIgnoreUpdate = true)]
    public DateTime CreationTime { get; set; }
}
