using System.Collections.ObjectModel;

namespace Sms.WpfApp.ViewModels;

public sealed class MainViewModel
{
    public ObservableCollection<EnvVarRow> Rows { get; }

    public MainViewModel(IEnumerable<EnvVarRow> rows)
    {
        Rows = new ObservableCollection<EnvVarRow>(rows);
    }
}
