using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sms.WpfApp.Logging;
using Sms.WpfApp.Services;
using Sms.WpfApp.ViewModels;

namespace Sms.WpfApp;

public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var envSection = configuration.GetSection("EnvVariables");
        var names = envSection.GetSection("Names").Get<string[]>() ?? [];
        var defaultValue = envSection["DefaultValue"] ?? string.Empty;

        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new FileLoggerProvider()));

        var serviceLogger = loggerFactory.CreateLogger<EnvVarService>();
        var service = new EnvVarService(serviceLogger);

        serviceLogger.LogInformation("Приложение запущено, переменных в конфигурации: {Count}", names.Length);

        var viewModel = new MainViewModel(service.LoadRows(names, defaultValue));

        var window = new MainWindow { DataContext = viewModel };
        window.Closed += (_, _) =>
        {
            serviceLogger.LogInformation("Приложение остановлено");
            loggerFactory.Dispose();
        };
        window.Show();
    }
}
