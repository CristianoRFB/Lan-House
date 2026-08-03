using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Infrastructure;

public sealed class RollingFileLoggerProvider(string filePath) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new RollingFileLogger(filePath, categoryName);

    public void Dispose()
    {
    }

    private sealed class RollingFileLogger(string path, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            AdrenalinaFileLog.Write(path, logLevel, category, formatter(state, exception), exception);
        }
    }
}

public static class AdrenalinaFileLog
{
    private const long MaximumFileBytes = 10 * 1024 * 1024;
    private static readonly ConcurrentDictionary<string, object> FileLocks = new(StringComparer.OrdinalIgnoreCase);

    public static void Write(string filePath, LogLevel level, string category, string message, Exception? exception = null)
    {
        try
        {
            var resolvedPath = Path.GetFullPath(filePath);
            var sync = FileLocks.GetOrAdd(resolvedPath, static _ => new object());
            lock (sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath)!);
                RotateIfNeeded(resolvedPath);

                var safeMessage = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
                var safeException = exception is null
                    ? string.Empty
                    : $" | {exception.GetType().Name}: {exception.Message.Replace('\r', ' ').Replace('\n', ' ').Trim()}";
                var line = $"{DateTimeOffset.Now:O} [{level}] {category}: {safeMessage}{safeException}{Environment.NewLine}";
                File.AppendAllText(resolvedPath, line);
            }
        }
        catch
        {
            // Falhas de observabilidade nunca devem derrubar a aplicação principal.
        }
    }

    private static void RotateIfNeeded(string filePath)
    {
        if (!File.Exists(filePath) || new FileInfo(filePath).Length < MaximumFileBytes)
        {
            return;
        }

        File.Move(filePath, filePath + ".1", overwrite: true);
    }
}
