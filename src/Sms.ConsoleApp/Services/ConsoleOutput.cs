using Microsoft.Extensions.Logging;

namespace Sms.ConsoleApp.Services;

public sealed class ConsoleOutput
{
    private readonly ILogger _logger;

    public ConsoleOutput(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("Console");
    }

    public void WriteLine(string message)
    {
        Console.WriteLine(message);
        _logger.LogInformation("{Message}", message);
    }

    public void WriteLine()
    {
        Console.WriteLine();
    }
}
