namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     标记属性为二维 / 三维坐标列（PostgreSQL <c>vector(2)</c> / <c>vector(3)</c>）。
///     属性类型须为 <see cref="DatabaseCoordinate"/>。
/// </summary>
/// <remarks>
///     与 <see cref="DatabaseVectorAttribute"/>（embedding）共用 pgvector 存储，但业务类型不同。
///     SQL 近邻：<c>ORDER BY location &lt;-&gt; @q::vector</c>；内存距离：<see cref="DatabaseCoordinate.Distance"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DatabaseCoordinateAttribute : Attribute
{
    /// <param name="dimensions">坐标维度，仅允许 <c>2</c> 或 <c>3</c>。</param>
    public DatabaseCoordinateAttribute(int dimensions)
    {
        if (dimensions is not (2 or 3))
            throw new ArgumentOutOfRangeException(nameof(dimensions), "坐标维度仅支持 2 或 3。");

        Dimensions = dimensions;
    }

    /// <summary>坐标维度（2 或 3）。</summary>
    public int Dimensions { get; }
}
