namespace DuMes.Component.Database.Entities;

/// <summary>
///     <see cref="DatabaseRecordEntity"/> 链式赋值扩展（<c>By</c> / <c>At</c>）。
/// </summary>
public static class DatabaseRecordEntityExtensions
{
    /// <summary>操作人 Id。</summary>
    public static T By<T>(this T entity, Ulid? creatorId)
        where T : DatabaseRecordEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.CreatorId = creatorId;
        return entity;
    }

    /// <summary>
    ///     操作时间；省略时写 <c>DateTime.Now</c>。
    /// </summary>
    public static T At<T>(this T entity, DateTime? creationTime = null)
        where T : DatabaseRecordEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.CreationTime = creationTime ?? DateTime.Now;
        return entity;
    }
}
