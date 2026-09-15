using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Client.Factories;
using Sms.Client.Interfaces;
using Sms.Client.Options;
using Sms.ConsoleApp.Data;
using Sms.ConsoleApp.Logging;
using Sms.ConsoleApp.Services;

ILoggerFactory? fileLoggerFactory = null;
try
{
    using var cts = new CancellationTokenSource();

    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddJsonFile(
        Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        optional: false,
        reloadOnChange: true);
    builder.Logging.AddSimpleConsole();
    builder.Logging.AddSmsFileLogger();

    fileLoggerFactory = LoggerFactory.Create(b => b.AddProvider(new FileLoggerProvider()));

    var services = builder.Services;

    var clientOptions = new SmsClientOptions();
    builder.Configuration.GetSection(SmsClientOptions.SectionName).Bind(clientOptions);

    services.AddHttpClient("SmsClient", c =>
        c.Timeout = TimeSpan.FromSeconds(clientOptions.TimeoutSeconds));
    services.AddSingleton<SmsClientFactory>();
    services.AddSingleton<OrderInputParser>();
    services.AddSingleton<ConsoleOutput>();
    services.AddSingleton<ISmsClient>(sp =>
        sp.GetRequiredService<SmsClientFactory>().Create(clientOptions));

    services.AddDbContext<AppDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("SmsDb")
            ?? throw new InvalidOperationException("Connection string 'SmsDb' not configured");
        options.UseNpgsql(connectionString);
    });

    services.AddScoped<MenuService>();
    services.AddScoped<OrderService>();

    using var host = builder.Build();

    var console = host.Services.GetRequiredService<ConsoleOutput>();
    var logger = host.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Sms.ConsoleApp");

    logger.LogInformation("Application started");
    console.WriteLine("=== SMS Консольное приложение ===");

    logger.LogInformation(
        "Client configured: TransportType={TransportType}, BaseUrl={BaseUrl}, Endpoint={Endpoint}",
        clientOptions.TransportType, clientOptions.BaseUrl, clientOptions.Endpoint);

    // 1. База данных
    {
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cts.Token);
        logger.LogInformation("Database initialized");
        console.WriteLine("База данных инициализирована.");
    }

    // 2. СМС-клиент, получение и сохранение меню
    ISmsClient smsClient = host.Services.GetRequiredService<ISmsClient>();

    logger.LogInformation(
        "Requesting menu: TransportType={TransportType}", clientOptions.TransportType);
    console.WriteLine($"Получение меню ({clientOptions.TransportType})...");

    IReadOnlyList<MenuItemEntity> menu;
    try
    {
        var freshMenu = await smsClient.GetMenuAsync(cts.Token);
        logger.LogInformation("Menu received: {Count} items", freshMenu.Count);
        console.WriteLine($"Получено блюд: {freshMenu.Count}");

        using var scope = host.Services.CreateScope();
        menu = await scope.ServiceProvider.GetRequiredService<MenuService>()
            .UpdateMenuFromServerAsync(freshMenu, cts.Token);
        logger.LogInformation("Menu saved to database");
        console.WriteLine("Меню сохранено в PostgreSQL.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Menu request/save failed");
        console.WriteLine($"Ошибка получения меню: {ex.Message}");
        console.WriteLine("Приложение завершает работу.");
        logger.LogInformation("Application stopped");
        return 1;
    }

    // 3. Вывод меню
    console.WriteLine();
    console.WriteLine("=== МЕНЮ ===");
    foreach (var item in menu)
    {
        console.WriteLine($"{item.Name} – {item.Id} ({item.Article}) – {item.Price:0.00}");
    }

    // 4. Ввод и отправка заказа
    using (var scope = host.Services.CreateScope())
    {
        var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();
        await orderService.RunOrderLoopAsync(menu, cts.Token);
    }

    logger.LogInformation("Application stopped");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Критическая ошибка: {ex.Message}");
    fileLoggerFactory?.CreateLogger("Sms.ConsoleApp")
        .LogError(ex, "Критическая ошибка приложения");
    return 2;
}
finally
{
    fileLoggerFactory?.Dispose();
}
