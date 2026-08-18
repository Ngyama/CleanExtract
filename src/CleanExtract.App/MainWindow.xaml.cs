using System.IO;
using System.Windows;

namespace CleanExtract;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainViewModel? ViewModel { get; set; }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is null || !TryGetDroppedFile(e, out var path))
            return;
        await ViewModel.SetArchiveAsync(path);
    }

    private static bool TryGetDroppedFile(DragEventArgs e, out string path)
    {
        path = string.Empty;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return false;
        path = files[0];
        return File.Exists(path);
    }
}
