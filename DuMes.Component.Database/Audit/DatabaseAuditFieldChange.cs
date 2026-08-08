using System.Text.Json.Serialization;

namespace DuMes.Component.Database.Audit;

/// <summary>
///     单字段（或集合）改前/改后，前台可直接按 <see cref="Path"/> 展示差异列表。
/// </summary>
/// <remarks>
///     JSON 示例（驼峰）：
///     <code>
///     { "path": "PLC.Name", "label": "Plc.Name", "kind": "Nested", "before": "S7", "after": "NJ" }
///     { "path": "Avatar", "label": "Avatar", "kind": "Image", "before": "/a.png", "after": "/b.png" }
///     { "path": "MenuIcon", "label": "MenuIcon", "kind": "Icon", "before": "home", "after": "setting" }
///     { "path": "LoginMethods", "kind": "List", "before": ["Web","Mobile"], "after": ["Web"], "added": [], "removed": ["Mobile"] }
///     </code>
/// </remarks>
public sealed class DatabaseAuditFieldChange
{
    /// <summary>
    ///     相对实体的点号路径（不含实体名）。
    ///     例：<c>Name</c>、<c>PLC.Name</c>、<c>LoginMethods</c>。
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     字段名称键（可空；供前台 I18N，勿存某语言文案）。
    ///     空则前台回退用 <see cref="Path"/> 作键或原文。组件内置键见 <c>DatabaseAuditFieldNames</c>。
    /// </summary>
    public string Label { get; set; }

    /// <summary>值形态（含 <c>Image</c>/<c>Icon</c> 供前台展示）。</summary>
    public DatabaseAuditValueKind Kind { get; set; } = DatabaseAuditValueKind.Scalar;

    /// <summary>
    ///     改前值。标量/嵌套为单个 JSON 值；List 为 JSON 数组。
    ///     引用型建议用 <see cref="DatabaseAuditRef"/>（Id + 当时名称），避免前台只看到裸 Id。
    ///     新建时可为 <c>null</c>；删除时 <see cref="After"/> 可为 <c>null</c>。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public object Before { get; set; }

    /// <summary>改后值；形态同 <see cref="Before"/>。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public object After { get; set; }

    /// <summary>List 专用：相对改前新增的项（前台可标绿）。非 List 时为 <c>null</c>。</summary>
    public List<object> Added { get; set; }

    /// <summary>List 专用：相对改前删除的项（前台可标红）。非 List 时为 <c>null</c>。</summary>
    public List<object> Removed { get; set; }
}
