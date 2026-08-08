using System.Collections;
using System.Text.Json;
using DuMes.Component.Database.Entities;
using DuMes.Component.Database.Serialization;

namespace DuMes.Component.Database.Audit;

/// <summary>
///     构造 <see cref="DatabaseAuditRecord"/>：标量 / 嵌套路径 / List（含 Added/Removed）。
/// </summary>
/// <example>
/// <code>
/// var audit = DatabaseAuditBuilder.For&lt;DatabaseAuditRecord&gt;("Station", stationId, "Update")
///     .By(userId)
///     .Scalar("Name", "ST-01", "ST-02", label: "Name")
///     .Nested("PLC.Name", "S7", "NJ", label: "Plc.Name")
///     .List("LoginMethods", new[] { "Web", "Mobile" }, new[] { "Web", "Api" }, label: "LoginMethods")
///     .Build();
/// </code>
/// </example>
public sealed class DatabaseAuditBuilder<TRecord> where TRecord : DatabaseAuditRecord, new()
{
    private readonly TRecord _record;

    private DatabaseAuditBuilder(TRecord record)
    {
        _record = record;
    }

    /// <summary>新建审计记录（已 <c>NewId</c>，<c>CreationTime = DateTime.Now</c>）。</summary>
    public static DatabaseAuditBuilder<TRecord> For(string entityName, Ulid entityId, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var record = new TRecord().NewId().At();
        record.EntityName = entityName.Trim();
        record.EntityId = entityId;
        record.Action = action.Trim();
        record.Changes = [];
        return new DatabaseAuditBuilder<TRecord>(record);
    }

    /// <summary>操作人。</summary>
    public DatabaseAuditBuilder<TRecord> By(Ulid? creatorId)
    {
        _record.By(creatorId);
        return this;
    }

    /// <summary>覆盖操作时间（默认已是 <c>DateTime.Now</c>）。</summary>
    public DatabaseAuditBuilder<TRecord> At(DateTime creationTime)
    {
        _record.At(creationTime);
        return this;
    }

    /// <summary>标量字段变更。</summary>
    public DatabaseAuditBuilder<TRecord> Scalar(string path, object before, object after, string label = null)
    {
        return Change(path, DatabaseAuditValueKind.Scalar, before, after, label);
    }

    /// <summary>嵌套/JSON 子路径变更，如 <c>PLC.Name</c>。</summary>
    public DatabaseAuditBuilder<TRecord> Nested(string path, object before, object after, string label = null)
    {
        return Change(path, DatabaseAuditValueKind.Nested, before, after, label);
    }

    /// <summary>图片字段变更（值多为 URL/路径；供前台按图片展示）。</summary>
    public DatabaseAuditBuilder<TRecord> Image(string path, object before, object after, string label = null)
    {
        return Change(path, DatabaseAuditValueKind.Image, before, after, label);
    }

    /// <summary>图标字段变更（值多为 icon 名或 URL；供前台按图标展示）。</summary>
    public DatabaseAuditBuilder<TRecord> Icon(string path, object before, object after, string label = null)
    {
        return Change(path, DatabaseAuditValueKind.Icon, before, after, label);
    }

    /// <summary>
    ///     集合变更：<c>Before</c>/<c>After</c> 为数组，并计算 <c>Added</c>/<c>Removed</c>（按 JSON 规范化值比较）。
    /// </summary>
    public DatabaseAuditBuilder<TRecord> List(string path, IEnumerable before, IEnumerable after, string label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var beforeList = NormalizeList(before);
        var afterList = NormalizeList(after);
        DiffLists(beforeList, afterList, out var added, out var removed);

        _record.Changes.Add(new DatabaseAuditFieldChange
        {
            Path = path.Trim(),
            Label = label,
            Kind = DatabaseAuditValueKind.List,
            Before = beforeList,
            After = afterList,
            Added = added,
            Removed = removed
        });
        return this;
    }

    /// <summary>通用追加一条变更（不自动算 List 差分）。</summary>
    public DatabaseAuditBuilder<TRecord> Change(
        string path,
        DatabaseAuditValueKind kind,
        object before,
        object after,
        string label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _record.Changes.Add(new DatabaseAuditFieldChange
        {
            Path = path.Trim(),
            Label = label,
            Kind = kind,
            Before = before,
            After = after
        });
        return this;
    }

    /// <summary>得到可落库 / 可直接返回前台的审计记录。</summary>
    public TRecord Build() => _record;

    /// <summary>当前是否已记录到至少一条字段变更（业务侧：空则无需写库）。</summary>
    public bool HasChanges => _record.Changes is { Count: > 0 };

    private static List<object> NormalizeList(IEnumerable source)
    {
        var list = new List<object>();
        if (source == null)
            return list;

        foreach (var item in source)
            list.Add(item);
        return list;
    }

    /// <summary>多重集差分：按 JSON 规范化键匹配，保留重复项次数。</summary>
    private static void DiffLists(
        List<object> beforeList,
        List<object> afterList,
        out List<object> added,
        out List<object> removed)
    {
        added = [];
        removed = [];

        var beforeKeys = beforeList.Select(ToCompareKey).ToList();
        var afterKeys = afterList.Select(ToCompareKey).ToList();

        var pool = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        for (var i = 0; i < beforeList.Count; i++)
        {
            var key = beforeKeys[i];
            if (!pool.TryGetValue(key, out var queue))
            {
                queue = new Queue<int>();
                pool[key] = queue;
            }

            queue.Enqueue(i);
        }

        var matchedBefore = new bool[beforeList.Count];
        for (var i = 0; i < afterList.Count; i++)
        {
            var key = afterKeys[i];
            if (pool.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                matchedBefore[queue.Dequeue()] = true;
                continue;
            }

            added.Add(afterList[i]);
        }

        for (var i = 0; i < beforeList.Count; i++)
        {
            if (!matchedBefore[i])
                removed.Add(beforeList[i]);
        }
    }

    private static string ToCompareKey(object value)
    {
        if (value == null)
            return "null";

        // 引用快照：只按 Id 比较增减，Name/Extra 变化不视为另一项
        if (value is DatabaseAuditRef auditRef)
            return "ref:" + (auditRef.Id ?? string.Empty);

        return JsonSerializer.Serialize(value, DatabaseJsonOptions.JsonStringOptions);
    }
}

/// <summary><see cref="DatabaseAuditBuilder{TRecord}"/> 非泛型入口（默认 <see cref="DatabaseAuditRecord"/>）。</summary>
public static class DatabaseAuditBuilder
{
    public static DatabaseAuditBuilder<DatabaseAuditRecord> For(string entityName, Ulid entityId, string action)
        => DatabaseAuditBuilder<DatabaseAuditRecord>.For(entityName, entityId, action);

    public static DatabaseAuditBuilder<TRecord> For<TRecord>(string entityName, Ulid entityId, string action)
        where TRecord : DatabaseAuditRecord, new()
        => DatabaseAuditBuilder<TRecord>.For(entityName, entityId, action);
}
