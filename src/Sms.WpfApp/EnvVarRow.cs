using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sms.WpfApp;

public sealed class EnvVarRow : INotifyPropertyChanged
{
    private readonly Action<EnvVarRow, string?> _onValueChanged;
    private string _value;
    private string _comment;

    public EnvVarRow(string field, string value, string comment, Action<EnvVarRow, string?> onValueChanged)
    {
        _onValueChanged = onValueChanged;
        _value = value;
        _comment = comment;
        Field = field;
    }

    public EnvVarRow()
    {
        _onValueChanged = static (_, _) => { };
        _value = string.Empty;
        _comment = string.Empty;
        Field = string.Empty;
    }

    public string Field { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (string.IsNullOrWhiteSpace(Field))
            {
                _value = value ?? string.Empty;
                Comment = "Строку без имени поля нельзя добавить";
                return;
            }

            var newValue = value ?? string.Empty;
            if (string.Equals(_value, newValue, StringComparison.Ordinal))
            {
                return;
            }

            _value = newValue;
            _onValueChanged(this, newValue);
            OnPropertyChanged();
        }
    }

    public string Comment
    {
        get => _comment;
        private set
        {
            _comment = value;
            OnPropertyChanged();
        }
    }

    public void SetComment(string comment) => Comment = comment;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
