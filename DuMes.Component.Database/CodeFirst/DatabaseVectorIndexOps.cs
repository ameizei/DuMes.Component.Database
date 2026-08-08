namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     pgvector 距离操作符类；须与查询运算符一致才能走索引。
/// </summary>
public enum DatabaseVectorIndexOps
{
    /// <summary><c>vector_l2_ops</c> ↔ <c>&lt;-&gt;</c>（欧氏距离；坐标默认）。</summary>
    L2 = 0,

    /// <summary><c>vector_ip_ops</c> ↔ <c>&lt;#&gt;</c>（内积；注意返回负内积）。</summary>
    InnerProduct = 1,

    /// <summary><c>vector_cosine_ops</c> ↔ <c>&lt;=&gt;</c>（余弦距离）。</summary>
    Cosine = 2
}
