namespace DuMes.Component.Database.Entities;

/// <summary>
///     组件内置可审计字段的名称键（写入 <c>changes[].label</c>，供前台 I18N）。
/// </summary>
public static class DatabaseAuditFieldNames
{
    /// <summary>业务实体排序；资源键建议映射展示名（如 zh-CN「排序」）。</summary>
    public const string Sort = "Sort";
}
