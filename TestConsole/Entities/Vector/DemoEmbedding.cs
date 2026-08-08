using DuMes.Component.Database.CodeFirst;
using SqlSugar;

namespace TestConsole.Entities.Vector;

/// <summary>
///     PostgreSQL pgvector 示例（<c>vector(3)</c>）。
/// </summary>
[SugarTable("demo_embedding")]
[CodeFirst]
[Tenant("system")]
public class DemoEmbedding
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 26)]
    public Ulid Id { get; set; }

    [SugarColumn(ColumnName = "title", Length = 128)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "embedding")]
    [DatabaseVector(3)]
    public float[] Embedding { get; set; }
}
