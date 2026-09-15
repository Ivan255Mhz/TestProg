using Microsoft.Extensions.Logging;

namespace Sms.WpfApp.Services;

public sealed class EnvVarService
{
    private readonly ILogger<EnvVarService> _logger;

    public EnvVarService(ILogger<EnvVarService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<EnvVarRow> LoadRows(IReadOnlyList<string> names, string defaultValue)
    {
        var rows = new List<EnvVarRow>(names.Count);

        foreach (var name in names)
        {
            var current = Environment.GetEnvironmentVariable(
                name, EnvironmentVariableTarget.User);

            string value;
            string comment;
            if (current is null)
            {
                value = defaultValue;
                comment = "Переменная не задана в реестре пользователя";
                _logger.LogInformation(
                    "Переменная '{Name}' не задана, будет использовано значение по умолчанию",
                    name);
            }
            else
            {
                value = current;
                comment = "Переменная пользователя (не изменялась)";
            }

            rows.Add(new EnvVarRow(name, value, comment, OnValueChanged));
        }

        return rows;
    }

    private void OnValueChanged(EnvVarRow row, string? newValue)
    {
        try
        {
            Environment.SetEnvironmentVariable(
                row.Field, newValue ?? string.Empty, EnvironmentVariableTarget.User);

            _logger.LogInformation(
                "Переменная '{Name}' изменена (длина значения: {Length}), уровень: User",
                row.Field, (newValue ?? string.Empty).Length);

            row.SetComment($"Изменено: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось изменить переменную '{Name}'", row.Field);
            row.SetComment($"Ошибка сохранения: {ex.Message}");
        }
    }
}
