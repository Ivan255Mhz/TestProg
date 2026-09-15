using Microsoft.Extensions.Logging;
using Microsoft.Win32;

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
                InitializeUserVariable(name, defaultValue);
                value = defaultValue;
                comment = "Инициализирована значением по умолчанию";
                _logger.LogInformation(
                    "Переменная '{Name}' отсутствует, инициализирована значением по умолчанию на уровне User",
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

    private static void InitializeUserVariable(string name, string value)
    {
        if (value.Length == 0)
        {
            using var environmentKey = Registry.CurrentUser.OpenSubKey("Environment", writable: true);
            environmentKey?.SetValue(name, string.Empty);
            return;
        }

        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
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
