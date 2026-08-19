using System.Windows;

namespace CleanExtract;

public partial class PasswordWindow : Window
{
    public PasswordWindow(string archivePath, bool previousWasWrong)
    {
        InitializeComponent();
        WindowAppearance.Apply(this);
        PromptText.Text = $"压缩包“{System.IO.Path.GetFileName(archivePath)}”已加密，请输入密码。密码不会被保存。";
        if (previousWasWrong)
        {
            HintText.Text = "密码不正确，请重试。";
            HintText.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) => PasswordBox.Focus();
    }

    public string Password { get; private set; } = string.Empty;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Password = PasswordBox.Password;
        DialogResult = !string.IsNullOrEmpty(Password);
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Password = string.Empty;
        DialogResult = false;
    }
}
