using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Catalyst.Logging;

public static class AppLoggerFactory
{
    private static ILoggerFactory? _factory;

    public static void Initialize(bool verbose = false)
    {
        var minLevel = verbose ? LogLevel.Debug : LogLevel.Information;
        
        _factory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(minLevel);
        });
    }

    public static ILogger<T> CreateLogger<T>() where T : class
    {
        if (_factory is null)
            Initialize();

        return _factory!.CreateLogger<T>();
    }

    public static ILogger CreateLogger(string categoryName)
    {
        if (_factory is null)
            Initialize();

        return _factory!.CreateLogger(categoryName);
    }
}
