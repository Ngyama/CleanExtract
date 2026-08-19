using System.Windows;

namespace CleanExtract;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppState state)
    {
        InitializeComponent();
        WindowAppearance.Apply(this, resizable: true);
        DataContext = new SettingsViewModel(state);
    }
}
