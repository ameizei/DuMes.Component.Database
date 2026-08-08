using System.Reflection;
using SqlSugar;
using SqlSugar.IOC;

namespace DuMes.Component.Database.CodeFirst;

/// <summary>
///     CodeFirst 辅助：按 <see cref="DatabaseGroupAttribute.GroupName"/>（= ConfigId）扫描实体并 <c>InitTables</c>。
///     官方说明见 <see href="https://www.donet5.com/Home/Doc?typeId=1206">CodeFirst</see>。
/// </summary>
public static class DatabaseCodeFirst
{
    /// <summary>
    ///     从程序集取出可 CodeFirst 建表的实体（具体类 + <see cref="SugarTable"/> + <see cref="DatabaseGroupAttribute"/>）。
    /// </summary>
    public static Type[] GetEntityTypes(Assembly assembly, Func<Type, bool> predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return FilterEntityTypes(LoadTypes(assembly), groupName: null, predicate);
    }

    /// <summary>
    ///     从程序集取出指定 <paramref name="groupName"/>（ConfigId）下的实体类型。
    /// </summary>
    public static Type[] GetEntityTypes(Assembly assembly, string groupName, Func<Type, bool> predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (string.IsNullOrWhiteSpace(groupName))
            throw new ArgumentException("groupName 不能为空。", nameof(groupName));

        return FilterEntityTypes(LoadTypes(assembly), groupName.Trim(), predicate);
    }

    /// <summary>
    ///     从多个程序集合并扫描实体类型（按 <see cref="Type.FullName"/> 去重）。
    /// </summary>
    public static Type[] GetEntityTypes(IEnumerable<Assembly> assemblies, Func<Type, bool> predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return FilterEntityTypes(LoadTypes(assemblies), groupName: null, predicate);
    }

    /// <summary>
    ///     按 <see cref="DatabaseGroupAttribute.GroupName"/> 分组返回实体类型（键即 ConfigId）。
    /// </summary>
    public static IReadOnlyDictionary<string, Type[]> GetEntityTypesByGroup(Assembly assembly, Func<Type, bool> predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return GroupEntityTypes(FilterEntityTypes(LoadTypes(assembly), groupName: null, predicate));
    }

    /// <summary>
    ///     按 GroupName 分组扫描多个程序集。
    /// </summary>
    public static IReadOnlyDictionary<string, Type[]> GetEntityTypesByGroup(IEnumerable<Assembly> assemblies, Func<Type, bool> predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return GroupEntityTypes(FilterEntityTypes(LoadTypes(assemblies), groupName: null, predicate));
    }

    /// <summary>
    ///     对指定连接执行 <c>CodeFirst.InitTables</c>；<paramref name="entityTypes"/> 为空则跳过。
    /// </summary>
    public static void InitTables(ISqlSugarClient db, params Type[] entityTypes)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (entityTypes == null || entityTypes.Length == 0)
            return;

        db.CodeFirst.InitTables(entityTypes);
    }

    /// <summary>
    ///     扫描程序集，按实体上的 <see cref="DatabaseGroupAttribute.GroupName"/> 路由到对应 ConfigId 并建表。
    ///     可在业务任意时机调用（不必挂在 DI 注册上）。
    /// </summary>
    public static IReadOnlyDictionary<string, Type[]> InitTables(Assembly assembly, Func<Type, bool> predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return InitTablesByGroup(GetEntityTypesByGroup(assembly, predicate));
    }

    /// <summary>
    ///     扫描多个程序集并按 GroupName / ConfigId 建表。
    /// </summary>
    public static IReadOnlyDictionary<string, Type[]> InitTables(IEnumerable<Assembly> assemblies, Func<Type, bool> predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return InitTablesByGroup(GetEntityTypesByGroup(assemblies, predicate));
    }

    /// <summary>
    ///     仅对指定 GroupName（ConfigId）扫描建表。
    /// </summary>
    public static Type[] InitTables(string groupName, Assembly assembly, Func<Type, bool> predicate = null)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            throw new ArgumentException("groupName 不能为空。", nameof(groupName));
        ArgumentNullException.ThrowIfNull(assembly);

        var id = groupName.Trim();
        var types = GetEntityTypes(assembly, id, predicate);
        InitTables(DbScoped.SugarScope.GetConnection(id), types);
        return types;
    }

    private static IReadOnlyDictionary<string, Type[]> InitTablesByGroup(IReadOnlyDictionary<string, Type[]> byGroup)
    {
        foreach (var (groupName, types) in byGroup)
        {
            if (types.Length == 0)
                continue;

            var db = DbScoped.SugarScope.GetConnection(groupName);
            InitTables(db, types);
        }

        return byGroup;
    }

    private static IReadOnlyDictionary<string, Type[]> GroupEntityTypes(Type[] types)
    {
        return types
            .GroupBy(GetGroupName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static g => g.Key,
                static g => g.OrderBy(static t => t.FullName, StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string GetGroupName(Type type)
    {
        var attr = type.GetCustomAttribute<DatabaseGroupAttribute>(inherit: true);
        return attr?.GroupName ?? string.Empty;
    }

    private static Type[] LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static t => t != null).ToArray();
        }
    }

    private static Type[] LoadTypes(IEnumerable<Assembly> assemblies)
    {
        var merged = new List<Type>();
        foreach (var assembly in assemblies)
        {
            if (assembly == null)
                continue;
            merged.AddRange(LoadTypes(assembly));
        }

        return merged.ToArray();
    }

    private static Type[] FilterEntityTypes(IEnumerable<Type> types, string groupName, Func<Type, bool> predicate)
    {
        var query = types
            .Where(static t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(static t => t.GetCustomAttribute<SugarTable>(inherit: true) != null)
            .Where(static t => t.GetCustomAttribute<DatabaseGroupAttribute>(inherit: true) != null);

        if (!string.IsNullOrEmpty(groupName))
            query = query.Where(t => string.Equals(GetGroupName(t), groupName, StringComparison.OrdinalIgnoreCase));

        if (predicate != null)
            query = query.Where(predicate);

        return query
            .GroupBy(static t => t.FullName, StringComparer.Ordinal)
            .Select(static g => g.First())
            .OrderBy(static t => t.FullName, StringComparer.Ordinal)
            .ToArray();
    }
}
