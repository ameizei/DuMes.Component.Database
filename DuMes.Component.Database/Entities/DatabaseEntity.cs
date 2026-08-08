using System.Runtime.CompilerServices;
using SqlSugar;

namespace DuMes.Component.Database.Entities;

/// <summary>
///     DDD 实体基类：仅承载身份（<see cref="Id"/>）。
///     相等性按 Id；未赋主键的瞬时实例按引用比较。
///     派生类自行声明 <c>[SugarTable]</c> / <c>[CodeFirst]</c> / <c>[Tenant]</c> 与业务列。
/// </summary>
public abstract class DatabaseEntity : IEquatable<DatabaseEntity>
{
    /// <summary>主键；列名固定 <c>id</c>。推荐用 <see cref="DatabaseEntityExtensions.NewId{T}"/> 生成。</summary>
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    [DatabaseAuditIgnore]
    public Ulid Id { get; set; }

    /// <summary>尚未分配主键（瞬时实体）。</summary>
    [SugarColumn(IsIgnore = true)]
    public bool IsTransient => Id == default;

    public bool Equals(DatabaseEntity other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (GetType() != other.GetType())
            return false;
        if (IsTransient || other.IsTransient)
            return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object obj) => Equals(obj as DatabaseEntity);

    public override int GetHashCode() => IsTransient ? RuntimeHelpers.GetHashCode(this) : Id.GetHashCode();

    public static bool operator ==(DatabaseEntity left, DatabaseEntity right)
    {
        if (left is null)
            return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(DatabaseEntity left, DatabaseEntity right) => !(left == right);
}
