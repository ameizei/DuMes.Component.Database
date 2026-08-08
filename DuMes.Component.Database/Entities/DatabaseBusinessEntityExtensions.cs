namespace DuMes.Component.Database.Entities;

/// <summary>
///     <see cref="DatabaseBusinessEntity"/> 链式赋值扩展（<c>Touch</c> / <c>SoftDelete</c> / <c>Restore</c> / <c>WithSort</c>）。
/// </summary>
public static class DatabaseBusinessEntityExtensions
{
    /// <summary>写入修改人与修改时间（默认 <c>DateTime.Now</c>）。</summary>
    public static T Touch<T>(this T entity, Ulid? modifierId = null, DateTime? modifyTime = null)
        where T : DatabaseBusinessEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.ModifierId = modifierId;
        entity.ModifyTime = modifyTime ?? DateTime.Now;
        return entity;
    }

    /// <summary>
    ///     软删：写入 <c>DeleteTime</c>（默认 <c>DateTime.Now</c>），并同步修改人/时间。
    /// </summary>
    public static T SoftDelete<T>(this T entity, Ulid? modifierId = null, DateTime? deleteTime = null)
        where T : DatabaseBusinessEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        var at = deleteTime ?? DateTime.Now;
        entity.DeleteTime = at;
        return entity.Touch(modifierId, at);
    }

    /// <summary>恢复：清空 <c>DeleteTime</c>（视为未删除），并同步修改人/时间。</summary>
    public static T Restore<T>(this T entity, Ulid? modifierId = null, DateTime? modifyTime = null)
        where T : DatabaseBusinessEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.DeleteTime = null;
        return entity.Touch(modifierId, modifyTime);
    }

    /// <summary>排序值。</summary>
    public static T WithSort<T>(this T entity, int sort)
        where T : DatabaseBusinessEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.Sort = sort;
        return entity;
    }
}
