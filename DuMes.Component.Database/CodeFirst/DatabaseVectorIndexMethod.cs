namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     pgvector 近似近邻索引方法（见
///     <see href="https://github.com/pgvector/pgvector#indexing"/>）。
/// </summary>
public enum DatabaseVectorIndexMethod
{
    /// <summary><c>hnsw</c>：默认推荐，构建较慢、查询快，无需先灌满数据。</summary>
    Hnsw = 0,

    /// <summary>
    ///     <c>ivfflat</c>：需合理 <see cref="DatabaseVectorIndexAttribute.Lists"/>；
    ///     空表/极少数据时召回差，适合已有批量数据后再建。
    /// </summary>
    Ivfflat = 1
}
