using SqlSugar;

namespace DuMes.Component.Database.Entities;

/// <summary>
///     SqlSugar 链式扩展：未软删过滤 <c>DeleteTime == null</c>（<see cref="DatabaseBusinessEntity"/>）。
/// </summary>
public static class DatabaseBusinessSugarExtensions
{
    /// <summary>仅未删除行：<c>delete_time IS NULL</c>。</summary>
    public static ISugarQueryable<T> NotDeleted<T>(this ISugarQueryable<T> queryable)
        where T : DatabaseBusinessEntity, new()
    {
        ArgumentNullException.ThrowIfNull(queryable);
        return queryable.Where(x => x.DeleteTime == null);
    }

    /// <summary>仅更新未删除行：<c>delete_time IS NULL</c>。</summary>
    public static IUpdateable<T> NotDeleted<T>(this IUpdateable<T> updateable)
        where T : DatabaseBusinessEntity, new()
    {
        ArgumentNullException.ThrowIfNull(updateable);
        return updateable.Where(x => x.DeleteTime == null);
    }

    /// <summary>仅物理删除未软删行：<c>delete_time IS NULL</c>（避免误删已软删数据时可加此条件）。</summary>
    public static IDeleteable<T> NotDeleted<T>(this IDeleteable<T> deleteable)
        where T : DatabaseBusinessEntity, new()
    {
        ArgumentNullException.ThrowIfNull(deleteable);
        return deleteable.Where(x => x.DeleteTime == null);
    }
}
