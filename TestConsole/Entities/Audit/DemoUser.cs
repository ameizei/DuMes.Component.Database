using DuMes.Component.Database.Audit;
using DuMes.Component.Database.CodeFirst;
using DuMes.Component.Database.Entities;
using SqlSugar;

namespace TestConsole.Entities.Audit;

/// <summary>
///     用户演示：多角色存 <see cref="RoleIds"/>（Ulid）；一对一外键 <see cref="ProfileId"/>。
///     审计写 <see cref="DatabaseAuditRef"/> 快照（Id + 当时名称），业务列仍只存 Id。
/// </summary>
[SugarTable("demo_user")]
[CodeFirst]
[Tenant("system")]
public class DemoUser : DatabaseEntity
{
    [SugarColumn(ColumnName = "name", Length = 64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>角色 Id 列表（jsonb，业务表只存 Ulid）。</summary>
    [SugarColumn(ColumnName = "role_ids", IsJson = true, IsNullable = true, ColumnDataType = "jsonb")]
    public List<Ulid> RoleIds { get; set; } = [];

    /// <summary>一对一资料外键（可空 = 未绑定）。</summary>
    [SugarColumn(ColumnName = "profile_id", Length = 26, IsNullable = true)]
    public Ulid? ProfileId { get; set; }

    [SugarColumn(ColumnName = "create_time")]
    public DateTime CreateTime { get; set; }

    public AuditUpdate WithAudit(DatabaseAuditBuilder<DatabaseAuditRecord> audit) => new(this, audit);

    public sealed class AuditUpdate
    {
        private readonly DemoUser _entity;
        private readonly DatabaseAuditBuilder<DatabaseAuditRecord> _audit;

        internal AuditUpdate(DemoUser entity, DatabaseAuditBuilder<DatabaseAuditRecord> audit)
        {
            _entity = entity;
            _audit = audit;
        }

        public AuditUpdate SetName(string before, string after)
        {
            if (before != after)
            {
                _audit.Scalar("Name", before, after, label: "用户名");
                _entity.Name = after;
            }

            return this;
        }

        /// <summary>
        ///     多角色：业务列写 Id 列表；审计写带名称的 <see cref="DatabaseAuditRef"/> 快照。
        /// </summary>
        /// <param name="nameOf">写审计时解析当时名称（可查字典/缓存，勿依赖事后反查）。</param>
        public AuditUpdate SetRoleIds(List<Ulid> before, List<Ulid> after, Func<Ulid, string> nameOf)
        {
            ArgumentNullException.ThrowIfNull(nameOf);
            before ??= [];
            after ??= [];
            if (RoleIdsEqual(before, after))
                return this;

            var beforeRefs = DatabaseAuditRef.FromIds(before, nameOf);
            var afterRefs = DatabaseAuditRef.FromIds(after, nameOf);
            _audit.List("RoleIds", beforeRefs, afterRefs, label: "角色");
            _entity.RoleIds = after;
            return this;
        }

        /// <summary>一对一外键：业务列写 Id；审计快照 Id+名称。</summary>
        public AuditUpdate SetProfileId(Ulid? before, Ulid? after, Func<Ulid?, string> nameOf)
        {
            ArgumentNullException.ThrowIfNull(nameOf);
            if (before == after)
                return this;

            _audit.Scalar(
                "ProfileId",
                before is null ? null : DatabaseAuditRef.Of(before, nameOf(before)),
                after is null ? null : DatabaseAuditRef.Of(after, nameOf(after)),
                label: "资料外键");
            _entity.ProfileId = after;
            return this;
        }
    }

    private static bool RoleIdsEqual(List<Ulid> a, List<Ulid> b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Count != b.Count)
            return false;

        var left = a.ToHashSet();
        return left.Count == a.Count && b.All(left.Contains);
    }
}
