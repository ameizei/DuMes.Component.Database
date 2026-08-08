using System.Text.Json;
using DuMes.Component.Database.Audit;

namespace TestConsole.Entities.Audit.Plc;

/// <summary>三菱 PLC 配置（由工站 <c>Plc</c> JsonDocument 按品牌转换而来）。</summary>
public sealed class MitsubishiPlcConfig : DemoPlcConfig
{
    public override DemoPlcBrand Brand => DemoPlcBrand.Mitsubishi;

    public int NetworkNumber { get; set; }

    public int StationNumber { get; set; }

    public static MitsubishiPlcConfig FromDocument(JsonDocument document) => DeserializeOrNew<MitsubishiPlcConfig>(document);

    public AuditUpdate WithAudit(DatabaseAuditBuilder<DatabaseAuditRecord> audit) => new(this, audit);

    /// <summary>转换后的三菱对象上链式 Set；路径前缀 <c>Plc.</c>。</summary>
    public sealed class AuditUpdate
    {
        private readonly MitsubishiPlcConfig _plc;
        private readonly DatabaseAuditBuilder<DatabaseAuditRecord> _audit;

        internal AuditUpdate(MitsubishiPlcConfig plc, DatabaseAuditBuilder<DatabaseAuditRecord> audit)
        {
            _plc = plc;
            _audit = audit;
        }

        public AuditUpdate SetName(string before, string after)
        {
            if (before != after)
            {
                _audit.Nested("Plc.Name", before, after, label: "PLC名称");
                _plc.Name = after;
            }

            return this;
        }

        public AuditUpdate SetIp(string before, string after)
        {
            if (before != after)
            {
                _audit.Nested("Plc.Ip", before, after, label: "PLC地址");
                _plc.Ip = after;
            }

            return this;
        }

        public AuditUpdate SetNetworkNumber(int before, int after)
        {
            if (before != after)
            {
                _audit.Nested("Plc.NetworkNumber", before, after, label: "网络号");
                _plc.NetworkNumber = after;
            }

            return this;
        }

        public AuditUpdate SetStationNumber(int before, int after)
        {
            if (before != after)
            {
                _audit.Nested("Plc.StationNumber", before, after, label: "站号");
                _plc.StationNumber = after;
            }

            return this;
        }
    }
}
