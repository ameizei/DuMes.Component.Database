namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     标记实体为 PostgreSQL 继承子表（<c>CREATE TABLE ... INHERITS (parent)</c>）。
///     实体类必须 C# 继承带 <c>[SugarTable]</c> + <see cref="CodeFirstAttribute"/> 的父实体；
///     子类只声明自身多出的列。与 <see cref="DatabasePartitionAttribute"/> 互斥。
/// </summary>
/// <remarks>
///     见 <see href="https://www.postgresql.org/docs/current/ddl-inherit.html"/>。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DatabaseInheritAttribute : Attribute
{
}
