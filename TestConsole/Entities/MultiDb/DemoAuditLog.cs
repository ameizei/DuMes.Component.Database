using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.MultiDb;

/// <summary>
///     演示审计实体（落在 ConfigId=<c>demo</c> / searchpath=demo）。
/// </summary>
[SugarTable("demo_audit_log")]
[CodeFirst]
[Tenant("demo")]
public class DemoAuditLog
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "product_id", Length = 26)]
    public Ulid ProductId { get; set; }

    [SugarColumn(ColumnName = "action", Length = 32)]
    public string Action { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "message", Length = 500, IsNullable = true)]
    public string Message { get; set; }

    [SugarColumn(ColumnName = "create_time")]
    public DateTime CreateTime { get; set; }
}
