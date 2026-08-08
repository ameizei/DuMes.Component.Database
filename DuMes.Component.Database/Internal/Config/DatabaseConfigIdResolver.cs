namespace DuMes.Component.Database.Internal.Config;

/// <summary>
///     将实体上的 Tenant/Group ConfigId 解析为注册时的连接标识（忽略大小写）。
/// </summary>
internal static class DatabaseConfigIdResolver
{
    private static string[] _registered = [];

    public static void SetRegistered(IEnumerable<string> configIds)
    {
        _registered = configIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .GroupBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .Select(static g => g.First())
            .ToArray();
    }

    /// <summary>
    ///     若已注册则返回注册侧原始大小写；未命中则返回 trim 后的入参（交由 SqlSugar 报错）。
    /// </summary>
    public static string Resolve(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
            return configId;

        var requested = configId.Trim();
        foreach (var id in _registered)
        {
            if (string.Equals(id, requested, StringComparison.OrdinalIgnoreCase))
                return id;
        }

        return requested;
    }
}
