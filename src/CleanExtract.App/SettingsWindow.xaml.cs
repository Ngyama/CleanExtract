using System.Windows;

namespace CleanExtract;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppState state)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(state);
    }
}
