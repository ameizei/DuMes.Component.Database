namespace DuMes.Component.Database.Audit;

/// <summary>
///     审计字段变更值形态（便于前台按类型渲染）。
///     落库为枚举名（如 <c>Image</c>），非数值。
/// </summary>
public enum DatabaseAuditValueKind
{
    /// <summary>标量字段（字符串、数字、布尔、枚举名等）。</summary>
    Scalar = 0,

    /// <summary>
    ///     嵌套/JSON 子对象上的字段，路径用点号：
    ///     <c>PLC.Name</c>、<c>Detail.Supplier.Code</c>。
    /// </summary>
    Nested = 1,

    /// <summary>图片（URL / 路径等）；前台按图片展示改前改后。</summary>
    Image = 2,

    /// <summary>图标（icon 名 / URL 等）；前台按图标展示改前改后。</summary>
    Icon = 3,

    /// <summary>集合字段；<c>Before</c>/<c>After</c> 为数组，并可带 <c>Added</c>/<c>Removed</c>。</summary>
    List = 4
}
