using DuMes.Component.Database.Audit;
using DuMes.Component.Database.CodeFirst;
using DuMes.Component.Database.Entities;
using SqlSugar;

namespace TestConsole.Entities.Crud;

/// <summary>
///     演示实体。变更用链式 <c>WithAudit(builder).SetName(前,后).SetTags(前,后)</c>。
/// </summary>
[SugarTable("demo_product")]
[CodeFirst]
[Tenant("system")]
// 对齐产线 material_barcode_list：jsonb 数组用 GIN jsonb_path_ops，便于 @> 包含查询
[DatabaseJsonbIndex("ix_{table}_tags", nameof(Tags))]
public class DemoProduct : DatabaseEntity
{
    [SugarColumn(ColumnName = "name", Length = 100)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "price", DecimalDigits = 2, Length = 18)]
    public decimal Price { get; set; }

    [SugarColumn(ColumnName = "status", Length = 32)]
    public DemoProductStatus Status { get; set; }

    [SugarColumn(ColumnName = "detail", IsJson = true, IsNullable = true, ColumnDataType = "jsonb")]
    public DemoProductDetail Detail { get; set; }

    [SugarColumn(ColumnName = "tags", IsJson = true, IsNullable = true, ColumnDataType = "jsonb")]
    public List<DemoProductTag> Tags { get; set; }

    [SugarColumn(ColumnName = "create_time")]
    public DateTime CreateTime { get; set; }

    [SugarColumn(ColumnName = "modify_time", IsNullable = true)]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(ColumnName = "is_delete")]
    public bool IsDelete { get; set; }

    /// <summary>
    ///     挂上统一审计表 Builder：<c>WithAudit(b).SetName(前,后).SetTags(前,后)</c>。
    /// </summary>
    public AuditUpdate WithAudit(DatabaseAuditBuilder<DatabaseAuditRecord> audit) => new(this, audit);

    /// <summary>DemoProduct 字段级审计链式更新。</summary>
    public sealed class AuditUpdate
    {
        private readonly DemoProduct _entity;
        private readonly DatabaseAuditBuilder<DatabaseAuditRecord> _audit;

        internal AuditUpdate(DemoProduct entity, DatabaseAuditBuilder<DatabaseAuditRecord> audit)
        {
            _entity = entity;
            _audit = audit;
        }

        public AuditUpdate SetName(string before, string after)
        {
            if (before != after)
            {
                _audit.Scalar("Name", before, after, label: "名称");
                _entity.Name = after;
            }

            return this;
        }

        public AuditUpdate SetPrice(decimal before, decimal after)
        {
            if (before != after)
            {
                _audit.Scalar("Price", before, after, label: "价格");
                _entity.Price = after;
            }

            return this;
        }

        public AuditUpdate SetStatus(DemoProductStatus before, DemoProductStatus after)
        {
            if (before != after)
            {
                _audit.Scalar("Status", before, after, label: "状态");
                _entity.Status = after;
            }

            return this;
        }

        public AuditUpdate SetDetailSku(string before, string after)
        {
            if (before != after)
            {
                _audit.Nested("Detail.Sku", before, after, label: "SKU");
                _entity.Detail ??= new DemoProductDetail();
                _entity.Detail.Sku = after;
            }

            return this;
        }

        public AuditUpdate SetDetailPreferredStatus(DemoProductStatus before, DemoProductStatus after)
        {
            if (before != after)
            {
                _audit.Nested("Detail.PreferredStatus", before, after, label: "偏好状态");
                _entity.Detail ??= new DemoProductDetail();
                _entity.Detail.PreferredStatus = after;
            }

            return this;
        }

        /// <summary><c>SetTags([改前…], [改后…])</c></summary>
        public AuditUpdate SetTags(List<DemoProductTag> before, List<DemoProductTag> after)
        {
            before ??= [];
            after ??= [];
            if (TagsEqual(before, after))
                return this;

            _audit.List("Tags", before, after, label: "标签");
            _entity.Tags = after;
            return this;
        }
    }

    private static bool TagsEqual(List<DemoProductTag> a, List<DemoProductTag> b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Code != b[i].Code || a[i].Label != b[i].Label || a[i].TagId != b[i].TagId
                || a[i].RelatedStatus != b[i].RelatedStatus)
                return false;
        }

        return true;
    }
}
