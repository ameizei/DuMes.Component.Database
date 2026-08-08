namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     为 <c>vector</c> 列声明近似近邻索引（embedding <see cref="DatabaseVectorAttribute"/>
///     与坐标 <see cref="DatabaseCoordinateAttribute"/> 均可）。
///     InitTables 执行
///     <c>CREATE INDEX IF NOT EXISTS … USING hnsw|ivfflat (col vector_*_ops) [WITH (…)]</c>。
/// </summary>
/// <remarks>
///     默认 HNSW + L2，对齐坐标 <c>ORDER BY col &lt;-&gt; @q</c> 与多数 embedding 欧氏近邻。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DatabaseVectorIndexAttribute : Attribute
{
    /// <param name="indexName">索引名；可用 <c>{table}</c> 占位表名。</param>
    /// <param name="propertyName">实体属性名（非列名），须为 vector 列。</param>
    /// <param name="method">索引方法；默认 <see cref="DatabaseVectorIndexMethod.Hnsw"/>。</param>
    /// <param name="ops">距离操作符类；默认 <see cref="DatabaseVectorIndexOps.L2"/>。</param>
    public DatabaseVectorIndexAttribute(
        string indexName,
        string propertyName,
        DatabaseVectorIndexMethod method = DatabaseVectorIndexMethod.Hnsw,
        DatabaseVectorIndexOps ops = DatabaseVectorIndexOps.L2)
    {
        if (string.IsNullOrWhiteSpace(indexName))
            throw new ArgumentException("索引名不能为空。", nameof(indexName));
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("属性名不能为空。", nameof(propertyName));

        IndexName = indexName.Trim();
        PropertyName = propertyName.Trim();
        Method = method;
        Ops = ops;
    }

    /// <summary>索引名（支持 <c>{table}</c>）。</summary>
    public string IndexName { get; }

    /// <summary>实体属性名。</summary>
    public string PropertyName { get; }

    /// <summary>HNSW / IVFFlat。</summary>
    public DatabaseVectorIndexMethod Method { get; }

    /// <summary>距离操作符类。</summary>
    public DatabaseVectorIndexOps Ops { get; }

    /// <summary>
    ///     IVFFlat <c>lists</c>；仅 <see cref="DatabaseVectorIndexMethod.Ivfflat"/> 有效。
    ///     <c>0</c> 表示不写 WITH（用扩展默认）。经验值约 <c>rows/1000</c>。
    /// </summary>
    public int Lists { get; set; }

    /// <summary>
    ///     HNSW <c>m</c>；仅 <see cref="DatabaseVectorIndexMethod.Hnsw"/> 有效。
    ///     <c>0</c> 表示不指定（扩展默认，通常 16）。
    /// </summary>
    public int M { get; set; }

    /// <summary>
    ///     HNSW <c>ef_construction</c>；仅 HNSW 有效。
    ///     <c>0</c> 表示不指定（扩展默认，通常 64）。
    /// </summary>
    public int EfConstruction { get; set; }
}
