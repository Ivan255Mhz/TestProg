using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sms.WpfApp.Logging;
using Sms.WpfApp.Services;
using Sms.WpfApp.ViewModels;

namespace Sms.WpfApp;

public partial class App : Application
{
    private ILoggerFactory? _loggerFactory;

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new FileLoggerProvider()));

        DispatcherUnhandledException += (_, args) =>
        {
            _loggerFactory.CreateLogger("Sms.WpfApp")
                .LogError(args.Exception, "Необработанная ошибка UI");
            MessageBox.Show(
                $"Ошибка: {args.Exception.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var envSection = configuration.GetSection("EnvVariables");
        var names = envSection.GetSection("Names").Get<string[]>() ?? [];
        var defaultValue = envSection["DefaultValue"] ?? string.Empty;

        var serviceLogger = _loggerFactory.CreateLogger<EnvVarService>();
        var service = new EnvVarService(serviceLogger);

        serviceLogger.LogInformation("Приложение запущено, переменных в конфигурации: {Count}", names.Length);

        var viewModel = new MainViewModel(service.LoadRows(names, defaultValue));

        var window = new MainWindow { DataContext = viewModel };
        window.Closed += (_, _) =>
        {
            serviceLogger.LogInformation("Приложение остановлено");
            _loggerFactory.Dispose();
            _loggerFactory = null;
        };
        window.Show();
    }
}
