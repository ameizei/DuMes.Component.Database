namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     标记属性为 PostgreSQL <c>pgvector</c> 列（<c>vector(n)</c>）。
///     属性类型须为 <c>float[]</c> 或 <c>Pgvector.Vector</c>；维度 <see cref="Dimensions"/> 写入 CodeFirst 列类型。
/// </summary>
/// <remarks>
///     依赖扩展 <c>CREATE EXTENSION vector</c>（组件启动时对 PG 连接自动尝试；库须已安装 pgvector）。
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DatabaseVectorAttribute : Attribute
{
    /// <param name="dimensions">向量维度，须 &gt; 0（写入列类型 <c>vector(dimensions)</c>）。</param>
    public DatabaseVectorAttribute(int dimensions)
    {
        if (dimensions <= 0)
            throw new ArgumentOutOfRangeException(nameof(dimensions), "向量维度须 > 0。");

        Dimensions = dimensions;
    }

    /// <summary>向量维度。</summary>
    public int Dimensions { get; }
}
