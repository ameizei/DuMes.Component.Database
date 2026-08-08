using System.Linq.Expressions;
using System.Reflection;

namespace DuMes.Component.Database.Entities;

/// <summary>
///     <see cref="DatabaseEntity"/> 链式赋值扩展（lambda <c>Set</c> / <c>NewId</c>）。
/// </summary>
public static class DatabaseEntityExtensions
{
    /// <summary>为实体生成新的 <see cref="Ulid"/> 主键并返回同一实例。</summary>
    public static T NewId<T>(this T entity) where T : DatabaseEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.Id = Ulid.NewUlid();
        return entity;
    }

    /// <summary>
    ///     按属性表达式赋值并返回同一实例，支持链式调用：
    ///     <c>entity.Set(x =&gt; x.Name, "a").Set(x =&gt; x.Price, 1m)</c>。
    /// </summary>
    public static T Set<T, TProp>(this T entity, Expression<Func<T, TProp>> property, TProp value)
        where T : DatabaseEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(property);

        var member = UnwrapMember(property.Body);
        if (member == null)
            throw new ArgumentException("表达式须为属性或字段访问，例如 x => x.Name。", nameof(property));

        switch (member.Member)
        {
            case PropertyInfo prop:
                if (!prop.CanWrite)
                    throw new ArgumentException($"属性 {prop.Name} 不可写。", nameof(property));
                prop.SetValue(entity, value);
                break;
            case FieldInfo field:
                field.SetValue(entity, value);
                break;
            default:
                throw new ArgumentException("表达式须为属性或字段访问，例如 x => x.Name。", nameof(property));
        }

        return entity;
    }

    private static MemberExpression UnwrapMember(Expression body)
    {
        if (body is MemberExpression member)
            return member;

        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            && unary.Operand is MemberExpression converted)
            return converted;

        return null;
    }
}
