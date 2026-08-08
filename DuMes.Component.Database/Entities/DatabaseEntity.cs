using SqlSugar;

namespace DuMes.Component.Database.Entities;

/// <summary>
///     表实体基类：仅承载 ULID 主键身份（DDD 实体标识）。
///     派生类自行声明 <c>[SugarTable]</c> / <c>[CodeFirst]</c> / <c>[Tenant]</c> 与业务列。
/// </summary>
public abstract class DatabaseEntity
{
    /// <summary>主键；列名固定 <c>id</c>。推荐用 <see cref="DatabaseEntityExtensions.NewId{T}"/> 生成。</summary>
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }
}
