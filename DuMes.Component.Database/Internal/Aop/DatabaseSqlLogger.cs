using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DuMes.Component.Database.Internal.Aop;

/// <summary>
///     SQL AOP 使用的 <see cref="ILogger" /> 访问点（由 DI 在宿主启动或首次解析时注入）。
/// </summary>
internal static class DatabaseSqlLogger
{
    private static ILogger _logger = NullLogger.Instance;

    public static ILogger Logger => _logger;

    public static void Initialize(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger("DuMes.Component.Database");
    }
}
