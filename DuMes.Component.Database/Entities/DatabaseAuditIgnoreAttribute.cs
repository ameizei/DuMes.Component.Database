namespace DuMes.Component.Database.Entities;

/// <summary>
///     标记非领域差异列：不写入审计 <c>changes</c>。
///     典型：主键、创建/修改戳、软删时间——操作人与操作时间由审计行 <c>creator_*</c> 承载。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DatabaseAuditIgnoreAttribute : Attribute;
