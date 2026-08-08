namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     标记实体所属库组（兼容用）；<see cref="GroupName"/> = <c>ConfigId</c>。
///     若需 <c>QueryableWithAttr&lt;T&gt;</c> / <c>InsertableWithAttr</c> 等，请优先使用 SqlSugar
///     <c>[Tenant("configId")]</c>（扫描建表同样识别 Tenant）。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DatabaseGroupAttribute : Attribute
{
    /// <param name="groupName">库组名，须与 <c>Connections[].ConfigId</c> 一致（忽略大小写）。</param>
    public DatabaseGroupAttribute(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            throw new ArgumentException("groupName 不能为空。", nameof(groupName));

        GroupName = groupName.Trim();
    }

    /// <summary>库组名（关联 ConfigId）。</summary>
    public string GroupName { get; }
}
