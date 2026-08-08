namespace DuMes.Component.Database.Entities;

/// <summary>
///     标记领域属性：变更应写入统一审计表 <c>changes</c>（字段级差异）。
///     <see cref="Name"/> 为稳定名称键，原样写入 <c>changes[].label</c>，供前台 I18N 解析（勿写某语言展示文案）。
///     生命周期列（创建/修改/软删）勿标此特性。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DatabaseAuditFieldAttribute : Attribute
{
    public DatabaseAuditFieldAttribute(string name = null)
    {
        Name = name;
    }

    /// <summary>
    ///     审计字段名称键（建议与属性名或资源键一致，如 <c>Sort</c>、<c>Name</c>）。
    ///     空则回退属性名。写入 JSON <c>label</c>，前台用 I18N 组件按键取文案。
    /// </summary>
    public string Name { get; }
}
