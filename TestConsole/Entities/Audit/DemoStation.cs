using System.Text.Json;
using DuMes.Component.Database.Audit;
using DuMes.Component.Database.CodeFirst;
using DuMes.Component.Database.Entities;
using SqlSugar;
using TestConsole.Entities.Audit.Plc;

namespace TestConsole.Entities.Audit;

/// <summary>
///     工站：<c>Plc</c> 列统一 <see cref="JsonDocument"/>；按 <see cref="PlcBrand"/> 转成西门子/三菱配置后再 Set。
/// </summary>
[SugarTable("demo_station")]
[CodeFirst]
[Tenant("system")]
public class DemoStation : DatabaseEntity
{
    [SugarColumn(ColumnName = "name", Length = 64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>PLC 品牌（决定如何反序列化 <see cref="Plc"/>）。</summary>
    [SugarColumn(ColumnName = "plc_brand", Length = 32)]
    public DemoPlcBrand PlcBrand { get; set; }

    /// <summary>PLC 配置 jsonb（各品牌字段不同，落库形态统一）。</summary>
    [SugarColumn(ColumnName = "plc", IsJson = true, IsNullable = true, ColumnDataType = "jsonb")]
    public JsonDocument Plc { get; set; }

    [SugarColumn(ColumnName = "login_methods", IsJson = true, IsNullable = true, ColumnDataType = "jsonb")]
    public List<string> LoginMethods { get; set; }

    [SugarColumn(ColumnName = "create_time")]
    public DateTime CreateTime { get; set; }

    /// <summary>按品牌把 <see cref="Plc"/> 转成具体配置实体。</summary>
    public DemoPlcConfig GetPlcConfig() => DemoPlcConfig.FromDocument(PlcBrand, Plc);

    /// <summary>把改完的配置写回 <see cref="Plc"/>（并同步品牌）。</summary>
    public void SetPlcConfig(DemoPlcConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Plc?.Dispose();
        Plc = config.ToDocument();
        PlcBrand = config.Brand;
    }

    public AuditUpdate WithAudit(DatabaseAuditBuilder<DatabaseAuditRecord> audit) => new(this, audit);

    public sealed class AuditUpdate
    {
        private readonly DemoStation _entity;
        private readonly DatabaseAuditBuilder<DatabaseAuditRecord> _audit;

        internal AuditUpdate(DemoStation entity, DatabaseAuditBuilder<DatabaseAuditRecord> audit)
        {
            _entity = entity;
            _audit = audit;
        }

        public AuditUpdate SetName(string before, string after)
        {
            if (before != after)
            {
                _audit.Scalar("Name", before, after, label: "工站名称");
                _entity.Name = after;
            }

            return this;
        }

        public AuditUpdate SetLoginMethods(List<string> before, List<string> after)
        {
            before ??= [];
            after ??= [];
            if (before.SequenceEqual(after))
                return this;

            _audit.List("LoginMethods", before, after, label: "登录方式");
            _entity.LoginMethods = after;
            return this;
        }
    }
}
