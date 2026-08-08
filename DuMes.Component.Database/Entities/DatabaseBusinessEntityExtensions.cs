using System.Reflection;
using DuMes.Component.Database.Audit;

namespace DuMes.Component.Database.Entities;

/// <summary>
///     <see cref="DatabaseBusinessEntity"/> 领域/生命周期行为（<c>Touch</c> / 软删 / <c>ChangeSort</c>）。
/// </summary>
public static class DatabaseBusinessEntityExtensions
{
    private static readonly string SortAuditName = ResolveAuditFieldName(
        typeof(DatabaseBusinessEntity).GetProperty(nameof(DatabaseBusinessEntity.Sort)));

    /// <summary>写入修改人与修改时间（生命周期；不写审计 <c>changes</c>）。</summary>
    public static T Touch<T>(this T entity, Ulid? modifierId = null, DateTime? modifyTime = null)
        where T : DatabaseBusinessEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.ModifierId = modifierId;
        entity.ModifyTime = modifyTime ?? DateTime.Now;
        return entity;
    }

    /// <summary>
    ///     软删：写 <c>DeleteTime</c> 并 <see cref="Touch{T}"/>。不写审计 <c>changes</c>；
    ///     审计行请 <c>For(..., "Delete")</c>，用行级创建戳表达操作人/时间。
    /// </summary>
    public static T SoftDelete<T>(this T entity, Ulid? modifierId = null, DateTime? deleteTime = null)
        where T : DatabaseBusinessEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        var at = deleteTime ?? DateTime.Now;
        entity.DeleteTime = at;
        return entity.Touch(modifierId, at);
    }

    /// <summary>恢复：清空 <c>DeleteTime</c> 并 <see cref="Touch{T}"/>。不写审计 <c>changes</c>。</summary>
    public static T Restore<T>(this T entity, Ulid? modifierId = null, DateTime? modifyTime = null)
        where T : DatabaseBusinessEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.DeleteTime = null;
        return entity.Touch(modifierId, modifyTime);
    }

    /// <summary>仅赋值排序（如插入初始化）；无差异审计。</summary>
    public static T WithSort<T>(this T entity, int sort)
        where T : DatabaseBusinessEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.Sort = sort;
        return entity;
    }

    /// <summary>
    ///     变更排序（领域行为）：值变化时写入实体，并可选写入审计 <c>changes</c>。
    ///     <c>changes[].label</c> 默认取 <see cref="DatabaseAuditFieldNames.Sort"/>（I18N 键）；可用 <paramref name="name"/> 覆盖。
    /// </summary>
    public static T ChangeSort<T>(
        this T entity,
        int before,
        int after,
        DatabaseAuditBuilder<DatabaseAuditRecord> audit = null,
        string name = null)
        where T : DatabaseBusinessEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (before == after)
            return entity;

        audit?.Scalar(nameof(DatabaseBusinessEntity.Sort), before, after, name ?? SortAuditName);
        entity.Sort = after;
        return entity;
    }

    /// <summary>泛型审计记录重载。</summary>
    public static T ChangeSort<T, TRecord>(
        this T entity,
        int before,
        int after,
        DatabaseAuditBuilder<TRecord> audit,
        string name = null)
        where T : DatabaseBusinessEntity
        where TRecord : DatabaseAuditRecord, new()
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (before == after)
            return entity;

        audit?.Scalar(nameof(DatabaseBusinessEntity.Sort), before, after, name ?? SortAuditName);
        entity.Sort = after;
        return entity;
    }

    /// <summary>读取属性上 <see cref="DatabaseAuditFieldAttribute.Name"/>；无则属性名。</summary>
    public static string ResolveAuditFieldName(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        var attr = property.GetCustomAttribute<DatabaseAuditFieldAttribute>();
        if (attr != null && !string.IsNullOrWhiteSpace(attr.Name))
            return attr.Name.Trim();
        return property.Name;
    }
}
