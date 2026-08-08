using System.Globalization;

namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     二维 / 三维坐标（仓库货位、设备位姿等）。
///     配合 <see cref="DatabaseCoordinateAttribute"/> 落库为 PostgreSQL <c>vector(2)</c> / <c>vector(3)</c>，
///     可用 SQL <c>&lt;-&gt;</c> 做欧氏距离排序，或用 <see cref="Distance"/> 在内存中计算。
/// </summary>
public sealed class DatabaseCoordinate : IEquatable<DatabaseCoordinate>
{
    public DatabaseCoordinate(double x, double y)
    {
        X = x;
        Y = y;
        Z = null;
        Dimensions = 2;
    }

    public DatabaseCoordinate(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
        Dimensions = 3;
    }

    public double X { get; }

    public double Y { get; }

    /// <summary>三维时有值；二维为 <c>null</c>。</summary>
    public double? Z { get; }

    /// <summary>2 或 3。</summary>
    public int Dimensions { get; }

    public static DatabaseCoordinate From2D(double x, double y) => new(x, y);

    public static DatabaseCoordinate From3D(double x, double y, double z) => new(x, y, z);

    /// <summary>
    ///     欧氏距离。二维只算 XY；若一方为二维另一方为三维，缺失的 Z 按 0。
    /// </summary>
    public static double Distance(DatabaseCoordinate a, DatabaseCoordinate b)
        => Math.Sqrt(DistanceSquared(a, b));

    /// <summary>欧氏距离平方（比远近时可避免开方）。</summary>
    public static double DistanceSquared(DatabaseCoordinate a, DatabaseCoordinate b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        if (a.Dimensions == 2 && b.Dimensions == 2)
            return dx * dx + dy * dy;

        var az = a.Z ?? 0d;
        var bz = b.Z ?? 0d;
        var dz = az - bz;
        return dx * dx + dy * dy + dz * dz;
    }

    public float[] ToFloatArray()
    {
        return Dimensions == 2
            ? [(float)X, (float)Y]
            : [(float)X, (float)Y, (float)(Z ?? 0d)];
    }

    public override string ToString()
    {
        return Dimensions == 2
            ? string.Create(CultureInfo.InvariantCulture, $"({X},{Y})")
            : string.Create(CultureInfo.InvariantCulture, $"({X},{Y},{Z ?? 0d})");
    }

    public bool Equals(DatabaseCoordinate other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (Dimensions != other.Dimensions)
            return false;

        const double eps = 1e-9;
        if (Math.Abs(X - other.X) > eps || Math.Abs(Y - other.Y) > eps)
            return false;
        if (Dimensions == 2)
            return true;
        return Math.Abs((Z ?? 0d) - (other.Z ?? 0d)) <= eps;
    }

    public override bool Equals(object obj) => obj is DatabaseCoordinate other && Equals(other);

    public override int GetHashCode() => Dimensions == 2
        ? HashCode.Combine(X, Y)
        : HashCode.Combine(X, Y, Z);
}
