using Microsoft.Extensions.Logging;
using Sms.Contracts.Models;
using Sms.Client.Interfaces;
using Sms.ConsoleApp.Data;

namespace Sms.ConsoleApp.Services;

public sealed class OrderService(
    ISmsClient smsClient,
    OrderInputParser parser,
    ILogger<OrderService> logger)
{
    public async Task RunOrderLoopAsync(
        IReadOnlyList<MenuItemEntity> menu,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("Введите заказ в формате: Id:Количество;Id:Количество");
        Console.WriteLine("Пример: 5979224:2;9084246:0.408   (пустая строка или 'exit' — выход)");

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) ||
                input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            logger.LogInformation("Order input received: {Input}", input);

            if (!parser.TryParse(input, out var parsed, out var parseError))
            {
                logger.LogWarning("Order validation failed: {Error}", parseError);
                Console.WriteLine($"Ошибка: {parseError}");
                continue;
            }

            var validationError = ValidateAgainstMenu(parsed, menu, out var orderItems);
            if (validationError is not null)
            {
                logger.LogWarning("Order validation failed: {Error}", validationError);
                Console.WriteLine($"Ошибка: {validationError}");
                Console.WriteLine("Повторите ввод.");
                continue;
            }

            var order = new Order(Guid.NewGuid().ToString(), orderItems);

            try
            {
                var result = await smsClient.SendOrderAsync(order, cancellationToken);

                if (result.Success)
                {
                    logger.LogInformation("Order sent: {OrderId}", order.Id);
                    Console.WriteLine("УСПЕХ: заказ отправлен.");
                }
                else
                {
                    logger.LogWarning("Order rejected: {OrderId}: {Error}", order.Id, result.ErrorMessage);
                    Console.WriteLine($"Ошибка заказа: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Order send failed: {OrderId}", order.Id);
                Console.WriteLine($"Ошибка отправки: {ex.Message}");
            }

            Console.WriteLine("Повторите ввод (пустая строка — выход).");
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
