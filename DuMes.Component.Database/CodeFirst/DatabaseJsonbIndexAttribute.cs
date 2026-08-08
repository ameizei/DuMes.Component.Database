namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     为 jsonb 列声明 GIN 索引（SqlSugar <c>[SugarIndex]</c> 仅生成 B-tree，无法表达 GIN）。
///     InitTables 时对普通表 / 分区父表 / 继承表执行
///     <c>CREATE INDEX IF NOT EXISTS … USING GIN (col [jsonb_path_ops|jsonb_ops])</c>。
/// </summary>
/// <remarks>
///     默认 <see cref="DatabaseJsonbIndexOps.PathOps"/>，对齐产线
///     <c>material_barcode_list</c> 一类「数组/对象包含查询」场景。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DatabaseJsonbIndexAttribute : Attribute
{
    /// <param name="indexName">索引名；可用 <c>{table}</c> 占位表名。</param>
    /// <param name="propertyName">实体属性名（非列名），须为 jsonb 列。</param>
    /// <param name="ops">GIN 操作符类；默认 <see cref="DatabaseJsonbIndexOps.PathOps"/>。</param>
    public DatabaseJsonbIndexAttribute(
        string indexName,
        string propertyName,
        DatabaseJsonbIndexOps ops = DatabaseJsonbIndexOps.PathOps)
    {
        if (string.IsNullOrWhiteSpace(indexName))
            throw new ArgumentException("索引名不能为空。", nameof(indexName));
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("属性名不能为空。", nameof(propertyName));

        IndexName = indexName.Trim();
        PropertyName = propertyName.Trim();
        Ops = ops;
    }

    /// <summary>索引名（支持 <c>{table}</c>）。</summary>
    public string IndexName { get; }

    /// <summary>实体属性名。</summary>
    public string PropertyName { get; }

    /// <summary>GIN 操作符类。</summary>
    public DatabaseJsonbIndexOps Ops { get; }
}
