namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     标记分区键字段（须为 <see cref="DateTime"/> / <c>DateTime?</c>），用于 PostgreSQL <c>PARTITION BY RANGE</c>。
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DatabasePartitionFieldAttribute : Attribute
{
}
