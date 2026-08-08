namespace DuMes.Component.Database.Audit;

/// <summary>
///     引用型审计值：业务表可只存 Id，审计里建议同时快照当时的名称等展示字段，
///     前台无需再反查；角色改名/删除后历史仍可读。
/// </summary>
/// <remarks>
///     JSON 示例：<c>{ "id":"01K…", "name":"管理员", "extra": { "code":"admin" } }</c>
/// </remarks>
public sealed class DatabaseAuditRef
{
    /// <summary>业务主键（建议字符串化 Ulid）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>记录当时的显示名（快照）。</summary>
    public string Name { get; set; }

    /// <summary>可选自定义快照（编码、类型等），前台按需展示。</summary>
    public Dictionary<string, object> Extra { get; set; }

    public static DatabaseAuditRef Of(Ulid id, string name, Dictionary<string, object> extra = null)
        => Of(id.ToString(), name, extra);

    public static DatabaseAuditRef Of(Ulid? id, string name, Dictionary<string, object> extra = null)
        => id is null ? Of((string)null, name, extra) : Of(id.Value.ToString(), name, extra);

    public static DatabaseAuditRef Of(string id, string name, Dictionary<string, object> extra = null)
    {
        return new DatabaseAuditRef
        {
            Id = id ?? string.Empty,
            Name = name,
            Extra = extra
        };
    }

    /// <summary>从 Id 列表 + 名称解析器生成快照列表（写审计时用）。</summary>
    public static List<DatabaseAuditRef> FromIds(IEnumerable<Ulid> ids, Func<Ulid, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(nameOf);
        var list = new List<DatabaseAuditRef>();
        if (ids == null)
            return list;

        foreach (var id in ids)
            list.Add(Of(id, nameOf(id)));
        return list;
    }

    /// <summary>带 Extra 的解析。</summary>
    public static List<DatabaseAuditRef> FromIds(
        IEnumerable<Ulid> ids,
        Func<Ulid, (string Name, Dictionary<string, object> Extra)> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        var list = new List<DatabaseAuditRef>();
        if (ids == null)
            return list;

        foreach (var id in ids)
        {
            var (name, extra) = resolve(id);
            list.Add(Of(id, name, extra));
        }

        return list;
    }
}
