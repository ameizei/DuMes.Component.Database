namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     PostgreSQL jsonb GIN 操作符类（见
///     <see href="https://www.postgresql.org/docs/current/datatype-json.html#JSON-INDEXING"/>）。
/// </summary>
public enum DatabaseJsonbIndexOps
{
    /// <summary>
    ///     <c>jsonb_path_ops</c>：索引更小，适合 <c>@></c> 包含查询
    ///     （如条码列表 <c>material_barcode_list @> '[{"Barcode":"…"}]'</c>）。
    /// </summary>
    PathOps = 0,

    /// <summary>
    ///     <c>jsonb_ops</c>（GIN 默认）：支持 <c>?</c> / <c>?&amp;</c> / <c>?|</c> / <c>@></c> 等，索引更大。
    /// </summary>
    Ops = 1
}
