namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     标记实体为 CodeFirst 表（由实体驱动建表/改表），而非 DbFirst（库表已存在、仅映射）。
///     仅带此特性的实体会进入 <see cref="DatabaseCodeFirst.InitTables(System.Reflection.Assembly, System.Func{System.Type, bool})"/> 扫描。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class CodeFirstAttribute : Attribute
{
}
