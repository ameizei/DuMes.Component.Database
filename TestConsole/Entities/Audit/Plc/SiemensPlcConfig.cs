using System.Text.Json;
using DuMes.Component.Database.Audit;

namespace TestConsole.Entities.Audit.Plc;

/// <summary>西门子 PLC 配置（由工站 <c>Plc</c> JsonDocument 按品牌转换而来）。</summary>
public sealed class SiemensPlcConfig : DemoPlcConfig
{
    public override DemoPlcBrand Brand => DemoPlcBrand.Siemens;

    public int Rack { get; set; }

    public int Slot { get; set; }

    public static SiemensPlcConfig FromDocument(JsonDocument document) => DeserializeOrNew<SiemensPlcConfig>(document);

    public AuditUpdate WithAudit(DatabaseAuditBuilder<DatabaseAuditRecord> audit) => new(this, audit);

    /// <summary>转换后的西门子对象上链式 Set；路径前缀 <c>Plc.</c>。</summary>
    public sealed class AuditUpdate
    {
        private readonly SiemensPlcConfig _plc;
        private readonly DatabaseAuditBuilder<DatabaseAuditRecord> _audit;

        internal AuditUpdate(SiemensPlcConfig plc, DatabaseAuditBuilder<DatabaseAuditRecord> audit)
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

        public AuditUpdate SetRack(int before, int after)
        {
            if (before != after)
            {
                _audit.Nested("Plc.Rack", before, after, label: "机架号");
                _plc.Rack = after;
            }

            return this;
        }

        public AuditUpdate SetSlot(int before, int after)
        {
            if (before != after)
            {
                _audit.Nested("Plc.Slot", before, after, label: "槽号");
                _plc.Slot = after;
            }

            return this;
        }
    }
}
