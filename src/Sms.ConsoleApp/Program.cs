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

    var services = builder.Services;

    services.AddHttpClient();
    services.AddSingleton<SmsClientFactory>();
    services.AddSingleton<OrderInputParser>();

    var clientOptions = new SmsClientOptions();
    builder.Configuration.GetSection(SmsClientOptions.SectionName).Bind(clientOptions);
    services.AddSingleton<ISmsClient>(sp =>
        sp.GetRequiredService<SmsClientFactory>().Create(clientOptions));

    services.AddDbContext<AppDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("SmsDb")
            ?? throw new InvalidOperationException("Connection string 'SmsDb' not configured");
        options.UseNpgsql(connectionString);
    });

    services.AddSingleton<MenuService>();
    services.AddSingleton<OrderService>();

    using var host = builder.Build();

    var logger = host.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Sms.ConsoleApp");

    logger.LogInformation("Application started");
    Console.WriteLine("=== SMS Консольное приложение ===");

    var options = clientOptions;
    logger.LogInformation(
        "Client configured: TransportType={TransportType}, BaseUrl={BaseUrl}, Endpoint={Endpoint}",
        options.TransportType, options.BaseUrl, options.Endpoint);

    // 1. База данных
    {
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cts.Token);
        logger.LogInformation("Database initialized");
        Console.WriteLine("База данных инициализирована.");
    }

    // 2. СМС-клиент, получение и сохранение меню
    ISmsClient smsClient = host.Services.GetRequiredService<ISmsClient>();
    var menuService = host.Services.GetRequiredService<MenuService>();

    logger.LogInformation(
        "Requesting menu: TransportType={TransportType}", options.TransportType);
    Console.WriteLine($"Получение меню ({options.TransportType})...");

    IReadOnlyList<MenuItemEntity> menu;
    try
    {
        var freshMenu = await smsClient.GetMenuAsync(cts.Token);
        logger.LogInformation("Menu received: {Count} items", freshMenu.Count);
        Console.WriteLine($"Получено блюд: {freshMenu.Count}");

        menu = await menuService.UpdateMenuFromServerAsync(freshMenu, cts.Token);
        logger.LogInformation("Menu saved to database");
        Console.WriteLine("Меню сохранено в PostgreSQL.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Menu request/save failed");
        Console.WriteLine($"Ошибка получения меню: {ex.Message}");
        Console.WriteLine("Приложение завершает работу.");
        logger.LogInformation("Application stopped");
        return 1;
    }

    // 3. Вывод меню
    Console.WriteLine();
    Console.WriteLine("=== МЕНЮ ===");
    foreach (var item in menu)
    {
        Console.WriteLine($"{item.Name} – {item.Id} ({item.Article}) – {item.Price:0.00}");
    }

    // 4. Ввод и отправка заказа
    var orderService = host.Services.GetRequiredService<OrderService>();
    await orderService.RunOrderLoopAsync(menu, cts.Token);

    logger.LogInformation("Application stopped");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Критическая ошибка: {ex.Message}");
    return 2;
}
