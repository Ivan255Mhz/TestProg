using Microsoft.Extensions.Logging;
using System.Text;

namespace Sms.ConsoleApp.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logsDirectory;
    private readonly string _baseFileName;
    private readonly object _lock = new();

    public FileLoggerProvider(
        string logsDirectory = "logs",
        string baseFileName = "test-sms-console-app")
    {
        _logsDirectory = Path.Combine(AppContext.BaseDirectory, logsDirectory);
        _baseFileName = baseFileName;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    internal void Write(string message)
    {
        var fileName = $"{_baseFileName}-{DateTime.Now:yyyyMMdd}.log";
        Directory.CreateDirectory(_logsDirectory);

        lock (_lock)
        {
            File.AppendAllText(
                Path.Combine(_logsDirectory, fileName),
                message,
                Encoding.UTF8);
        }
    }

    public void Dispose()
    {
    }

    private sealed class FileLogger(FileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

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

            var line =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} " +
                $"[{logLevel.ToString().ToUpperInvariant()}] {categoryName}: {formatter(state, exception)}";

            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            provider.Write(line + Environment.NewLine);
        }
    }
}
