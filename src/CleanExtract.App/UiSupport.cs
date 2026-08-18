using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CleanExtract.Core.Archive;
using CleanExtract.Core.Workflow;

namespace CleanExtract;

internal sealed class UiPasswordPrompt : IPasswordPrompt
{
    private readonly Window _owner;

    public UiPasswordPrompt(Window owner) => _owner = owner;

    public Task<string?> RequestPasswordAsync(string archivePath, bool previousWasWrong, CancellationToken cancellationToken)
    {
        return _owner.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dialog = new PasswordWindow(archivePath, previousWasWrong)
            {
                Owner = _owner,
            };
            return dialog.ShowDialog() == true ? dialog.Password : null;
        }).Task;
    }
}

internal static class UserMessages
{
    public static string For(Exception exception)
    {
        return exception switch
        {
            ArchiveBackendNotFoundException => "找不到内置的 7-Zip。请确认 resources 文件夹中有 7zz.exe 和 7z.dll。",
            WrongPasswordException => "密码不正确。",
            PasswordRequiredException => "这个压缩包需要密码才能打开。",
            MissingVolumeException => "这是一个分卷压缩包，缺少其中一部分文件。请把所有分卷放在同一目录后再试。",
            UnsupportedArchiveException => "无法识别这个压缩包格式，或文件不是有效的压缩包。",
            CorruptedArchiveException => "压缩包已损坏或不完整，无法解压。",
            OperationCancelledArchiveException => "已取消。",
            ArchiveException ex => TranslateArchive(ex),
            UnauthorizedAccessException => "没有权限写入目标文件夹。",
            DirectoryNotFoundException => "找不到目标文件夹。",
            System.IO.IOException io when IsDiskFull(io) => "磁盘空间不足，无法完成解压。",
            System.IO.IOException io => $"无法读写文件：{io.Message}",
            _ => "出现了未预期的错误。详细信息已写入日志。",
        };
    }

    private static string TranslateArchive(ArchiveException exception)
    {
        var text = exception.Message;
        if (text.Contains("not writable", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Permission", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Access", StringComparison.OrdinalIgnoreCase))
            return "没有权限写入目标文件夹。";
        if (text.Contains("disk", StringComparison.OrdinalIgnoreCase)
            || text.Contains("space", StringComparison.OrdinalIgnoreCase))
            return "磁盘空间不足，无法完成解压。";
        if (text.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return "找不到这个压缩包。";
        return text;
    }

    private static bool IsDiskFull(System.IO.IOException exception)
    {
        return exception.Message.Contains("space", StringComparison.OrdinalIgnoreCase)
               || exception.HResult is unchecked((int)0x80070070);
    }
}

internal sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(Convert(parameter)) ?? true;

    public void Execute(object? parameter) => _execute(Convert(parameter));

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Convert(object? parameter) => parameter is T value ? value : default;
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class InverseBoolToVisConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
