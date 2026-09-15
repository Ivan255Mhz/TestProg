using Microsoft.Extensions.Logging;
using Sms.Contracts.Models;
using Sms.Client.Interfaces;
using Sms.ConsoleApp.Data;

namespace Sms.ConsoleApp.Services;

public sealed class OrderService(
    ISmsClient smsClient,
    OrderInputParser parser,
    ConsoleOutput console,
    ILogger<OrderService> logger)
{
    public async Task RunOrderLoopAsync(
        IReadOnlyList<MenuItemEntity> menu,
        CancellationToken cancellationToken = default)
    {
        console.WriteLine();
        console.WriteLine("Введите заказ в формате: Id:Количество;Id:Количество");
        console.WriteLine("Пример: 5979224:2;9084246:0.408   ('exit' — выход)");

        var order = new Order(Guid.NewGuid().ToString(), new List<OrderItem>());

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (input is null ||
                input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            logger.LogInformation("Order input received: {Input}", input);

            if (!parser.TryParse(input, out var parsed, out var parseError))
            {
                logger.LogWarning("Order validation failed: {Error}", parseError);
                console.WriteLine($"Ошибка: {parseError}");
                continue;
            }

            var validationError = ValidateAgainstMenu(parsed, menu, out var orderItems);
            if (validationError is not null)
            {
                logger.LogWarning("Order validation failed: {Error}", validationError);
                console.WriteLine($"Ошибка: {validationError}");
                console.WriteLine("Повторите ввод.");
                continue;
            }

            ((List<OrderItem>)order.Items).AddRange(orderItems);

            try
            {
                var result = await smsClient.SendOrderAsync(order, cancellationToken);

                if (result.Success)
                {
                    logger.LogInformation("Order sent: {OrderId}", order.Id);
                    console.WriteLine("УСПЕХ");
                }
                else
                {
                    logger.LogWarning("Order rejected: {OrderId}: {Error}", order.Id, result.ErrorMessage);
                    console.WriteLine(result.ErrorMessage ?? "Сервер не вернул текст ошибки");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Order send failed: {OrderId}", order.Id);
                console.WriteLine(ex.Message);
            }

            ((List<OrderItem>)order.Items).Clear();
            console.WriteLine("Повторите ввод ('exit' — выход).");
        }
    }

    private static string? ValidateAgainstMenu(
        IReadOnlyList<ParsedOrderItem> parsed,
        IReadOnlyList<MenuItemEntity> menu,
        out List<OrderItem> items)
    {
        items = [];
        var menuById = menu.ToDictionary(m => m.Id, StringComparer.Ordinal);

        foreach (var (item, index) in parsed.Select((p, i) => (p, i + 1)))
        {
            if (!menuById.TryGetValue(item.Id, out var menuItem))
            {
                return $"блюдо с кодом {item.Id} не найдено в меню.";
            }

            if (!menuItem.IsWeighted && item.Quantity != decimal.Truncate(item.Quantity))
            {
                return $"блюдо {item.Id} не весовое — количество должно быть целым.";
            }

            items.Add(new OrderItem(item.Id, item.Quantity));
        }

        return null;
    }
}
