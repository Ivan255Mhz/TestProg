using System.Globalization;

namespace Sms.ConsoleApp.Services;

public record ParsedOrderItem(string Id, decimal Quantity);

public sealed class OrderInputParser
{
    public bool TryParse(string input, out IReadOnlyList<ParsedOrderItem> items, out string? error)
    {
        error = null;
        items = [];

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Ввод пуст. Введите позиции в формате: Id:Количество;Id:Количество";
            return false;
        }

        var result = new List<ParsedOrderItem>();
        var parts = input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var (part, index) in parts.Select((p, i) => (p, i + 1)))
        {
            var nameValue = part.Split(':');

            if (nameValue.Length != 2)
            {
                error = $"Позиция {index} (\"{part}\"): формат \"Id:Количество\".";
                return false;
            }

            var id = nameValue[0].Trim();
            var quantityText = nameValue[1].Trim().Replace(',', '.');

            if (id.Length == 0)
            {
                error = $"Позиция {index}: не указан Id блюда.";
                return false;
            }

            if (!decimal.TryParse(quantityText, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity))
            {
                error = $"Позиция {index}: количество \"{nameValue[1]}\" не является числом.";
                return false;
            }

            if (quantity <= 0)
            {
                error = $"Позиция {index}: количество должно быть больше 0.";
                return false;
            }

            result.Add(new ParsedOrderItem(id, quantity));
        }

        items = result
            .GroupBy(p => p.Id, StringComparer.Ordinal)
            .Select(g => new ParsedOrderItem(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        return true;
    }
}
