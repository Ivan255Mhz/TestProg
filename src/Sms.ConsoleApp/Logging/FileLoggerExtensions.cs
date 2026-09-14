using Microsoft.Extensions.Logging;

namespace Sms.ConsoleApp.Logging;

internal static class FileLoggerExtensions
{
    public static ILoggingBuilder AddSmsFileLogger(
        this ILoggingBuilder builder,
        string logsDirectory = "logs",
        string baseFileName = "test-sms-console-app")
    {
        builder.AddProvider(new FileLoggerProvider(logsDirectory, baseFileName));
        return builder;
    }
}
