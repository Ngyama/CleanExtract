using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CleanExtract.Core;
using CleanExtract.Core.Archive;
using CleanExtract.Core.Cleaning;
using CleanExtract.Core.Logging;
using CleanExtract.Core.Workflow;

namespace CleanExtract;

public enum UiState
{
    Empty,
    Ready,
    Running,
    Success,
    Failed,
    Details,
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IArchiveBackend _backend;
    private readonly CleanExtractWorkflow _workflow;
    private readonly IAppLog _log;
    private readonly AppState _app;
    private CancellationTokenSource? _cts;

    private UiState _state = UiState.Empty;
    private string _archivePath = string.Empty;
    private string _archiveName = string.Empty;
    private string _archiveMeta = string.Empty;
    private double _progressPercent;
    private bool _isProgressIndeterminate = true;
    private string _statusText = string.Empty;
    private string _errorText = string.Empty;
    private string _resultHeadline = string.Empty;
    private string _resultDetail = string.Empty;
    private FilterSummary? _summary;

    public MainViewModel(IArchiveBackend backend, CleanExtractWorkflow workflow, AppState state)
    {
        _backend = backend;
        _workflow = workflow;
        _app = state;
        _log = state.Log;
        BrowseCommand = new RelayCommand(Browse, () => State is not UiState.Running);
        ExtractCommand = new RelayCommand(() => _ = ExtractAsync(), () => State is UiState.Ready && !string.IsNullOrEmpty(ArchivePath));
        CancelCommand = new RelayCommand(Cancel, () => State is UiState.Running);
        ResetCommand = new RelayCommand(Reset, () => State is not UiState.Running);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => !string.IsNullOrEmpty(Summary?.OutputDirectory));
        ShowDetailsCommand = new RelayCommand(() => State = UiState.Details, () => Summary is not null);
        HideDetailsCommand = new RelayCommand(() => State = UiState.Success);
        SettingsCommand = new RelayCommand(OpenSettings, () => State is not UiState.Running);
        AlwaysKeepCommand = new RelayCommand<DetailRow>(AlwaysKeep);
        AlwaysFilterCommand = new RelayCommand<DetailRow>(AlwaysFilter);
    }

    public Action? OpenSettingsRequested { get; set; }

    private void OpenSettings() => OpenSettingsRequested?.Invoke();

    private void AlwaysKeep(DetailRow? row)
    {
        if (row is null)
            return;
        _app.AlwaysKeep(row.FileName);
        row.StatusNote = $"已记住：下次将保留 {row.FileName}";
    }

    private void AlwaysFilter(DetailRow? row)
    {
        if (row is null)
            return;
        _app.AlwaysFilter(row.FileName);
        row.StatusNote = $"已记住：下次将过滤 {row.FileName}";
    }

    public ICommand BrowseCommand { get; }
    public ICommand ExtractCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand ShowDetailsCommand { get; }
    public ICommand HideDetailsCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand AlwaysKeepCommand { get; }
    public ICommand AlwaysFilterCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DetailRow> TrashRows { get; } = [];
    public ObservableCollection<DetailRow> SuspiciousRows { get; } = [];

    public UiState State
    {
        get => _state;
        private set
        {
            if (Set(ref _state, value))
                RaiseCommands();
        }
    }

    public string ArchivePath
    {
        get => _archivePath;
        private set => Set(ref _archivePath, value);
    }

    public string ArchiveName
    {
        get => _archiveName;
        private set => Set(ref _archiveName, value);
    }

    public string ArchiveMeta
    {
        get => _archiveMeta;
        private set => Set(ref _archiveMeta, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            if (Set(ref _progressPercent, value))
                OnPropertyChanged(nameof(ProgressPercentText));
        }
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set
        {
            if (Set(ref _isProgressIndeterminate, value))
                OnPropertyChanged(nameof(ShowProgressPercent));
        }
    }

    public bool ShowProgressPercent => !IsProgressIndeterminate;

    public string ProgressPercentText => $"{ProgressPercent:0}%";

    public string ErrorText
    {
        get => _errorText;
        private set => Set(ref _errorText, value);
    }

    public string ResultHeadline
    {
        get => _resultHeadline;
        private set => Set(ref _resultHeadline, value);
    }

    public string ResultDetail
    {
        get => _resultDetail;
        private set => Set(ref _resultDetail, value);
    }

    public FilterSummary? Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public bool IsEmpty => State is UiState.Empty;
    public bool IsReady => State is UiState.Ready;
    public bool IsRunning => State is UiState.Running;
    public bool IsSuccess => State is UiState.Success;
    public bool IsFailed => State is UiState.Failed;
    public bool IsDetails => State is UiState.Details;
    public bool ShowHome => State is UiState.Empty or UiState.Ready;
    public bool HasTrash => TrashRows.Count > 0;
    public bool HasSuspicious => SuspiciousRows.Count > 0;

    public async Task SetArchiveAsync(string path)
    {
        if (State is UiState.Running)
            return;
        if (!File.Exists(path))
        {
            State = UiState.Failed;
            ErrorText = "找不到这个压缩包。";
            OnPropertyChanged(nameof(IsFailed));
            return;
        }

        ArchivePath = Path.GetFullPath(path);
        ArchiveName = Path.GetFileName(ArchivePath);
        var size = new FileInfo(ArchivePath).Length;
        ArchiveMeta = FileSizeFormatter.Format(size);
        Summary = null;
        ErrorText = string.Empty;
        State = UiState.Ready;
        NotifyState();

        try
        {
            var entries = await _backend.ListEntriesAsync(ArchivePath, password: null).ConfigureAwait(true);
            var files = entries.Count(e => !e.IsDirectory);
            ArchiveMeta = $"{FileSizeFormatter.Format(size)}  ·  {files} 个文件";
        }
        catch (PasswordRequiredException)
        {
            ArchiveMeta = $"{FileSizeFormatter.Format(size)}  ·  需要密码";
        }
        catch (Exception ex)
        {
            _log.Warn($"Preview listing failed: {ex.Message}");
            ArchiveMeta = FileSizeFormatter.Format(size);
        }
    }

    public async Task OpenAndExtractAsync(string path)
    {
        await SetArchiveAsync(path).ConfigureAwait(true);
        if (State is UiState.Ready)
            await ExtractAsync().ConfigureAwait(true);
    }

    public void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择压缩包",
            Filter = "Archives|*.zip;*.rar;*.7z;*.xz;*.gz;*.tar;*.tgz;*.001;*.iso;*.cab|All files|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() == true)
            _ = SetArchiveAsync(dialog.FileName);
    }

    private async Task ExtractAsync()
    {
        if (string.IsNullOrEmpty(ArchivePath) || State is UiState.Running)
            return;

        _cts = new CancellationTokenSource();
        State = UiState.Running;
        StatusText = "正在分析压缩包...";
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
        NotifyState();

        var progress = new Progress<WorkflowProgress>(p =>
        {
            StatusText = p.Message;
            if (p.Percent is double pct)
            {
                IsProgressIndeterminate = false;
                ProgressPercent = pct;
            }
            else if (string.Equals(p.Stage, "extract", StringComparison.Ordinal))
            {
                IsProgressIndeterminate = false;
                ProgressPercent = 0;
            }
            else
            {
                IsProgressIndeterminate = true;
                ProgressPercent = 0;
            }
        });
        try
        {
            var result = await _workflow.RunAsync(ArchivePath, progress: progress, cancellationToken: _cts.Token)
                .ConfigureAwait(true);
            Summary = result.Summary;
            ResultHeadline = "解压完成";
            ResultDetail = BuildResultDetail(result.Summary, _app.Rules.KeepSuspicious);
            if (!string.IsNullOrWhiteSpace(result.Warning))
                ResultDetail += Environment.NewLine + result.Warning;
            FillDetails(result.Summary);
            State = UiState.Success;
        }
        catch (OperationCanceledException)
        {
            State = UiState.Failed;
            ErrorText = "已取消。";
        }
        catch (Exception ex)
        {
            _log.Error("Extract failed", ex);
            State = UiState.Failed;
            ErrorText = UserMessages.For(ex);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            NotifyState();
        }
    }

    private void Cancel() => _cts?.Cancel();

    private void Reset()
    {
        ArchivePath = string.Empty;
        ArchiveName = string.Empty;
        ArchiveMeta = string.Empty;
        StatusText = string.Empty;
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
        ErrorText = string.Empty;
        ResultHeadline = string.Empty;
        ResultDetail = string.Empty;
        Summary = null;
        TrashRows.Clear();
        SuspiciousRows.Clear();
        State = UiState.Empty;
        NotifyState();
    }

    private void OpenFolder()
    {
        var dir = Summary?.OutputDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return;
        Process.Start(new ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true,
        });
    }

    private void FillDetails(FilterSummary summary)
    {
        TrashRows.Clear();
        SuspiciousRows.Clear();
        var keepSuspicious = _app.Rules.KeepSuspicious;
        foreach (var item in summary.Verdicts.Where(v => !v.ShouldExtract(keepSuspicious)))
            TrashRows.Add(DetailRow.From(item, kept: false));
        foreach (var item in summary.Verdicts.Where(v =>
                     v.Classification == Classification.Suspicious && v.ShouldExtract(keepSuspicious)))
            SuspiciousRows.Add(DetailRow.From(item, kept: true));
        OnPropertyChanged(nameof(HasTrash));
        OnPropertyChanged(nameof(HasSuspicious));
    }

    private static string BuildResultDetail(FilterSummary summary, bool keepSuspicious)
    {
        var files = $"{summary.FileCount} 个文件";
        var excluded = summary.Verdicts.Count(v => !v.ShouldExtract(keepSuspicious) && !v.Entry.IsDirectory);
        var suspiciousKept = summary.Verdicts.Count(v =>
            v.Classification == Classification.Suspicious && v.ShouldExtract(keepSuspicious) && !v.Entry.IsDirectory);
        var filtered = $"过滤 {excluded} 个文件";
        var suspicious = keepSuspicious
            ? $"{suspiciousKept} 个可疑文件已保留"
            : "已按设置过滤可疑文件";
        return $"{files}{Environment.NewLine}{filtered}{Environment.NewLine}{suspicious}";
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsDetails));
        OnPropertyChanged(nameof(ShowHome));
        RaiseCommands();
    }

    private void RaiseCommands()
    {
        (BrowseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExtractCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ShowDetailsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class DetailRow : INotifyPropertyChanged
{
    private string _statusNote = string.Empty;

    public required string Path { get; init; }
    public required string FileName { get; init; }
    public required string Reason { get; init; }
    public required string Confidence { get; init; }

    public string StatusNote
    {
        get => _statusNote;
        set
        {
            if (_statusNote == value)
                return;
            _statusNote = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusNote)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStatusNote)));
        }
    }

    public bool HasStatusNote => !string.IsNullOrWhiteSpace(StatusNote);

    public event PropertyChangedEventHandler? PropertyChanged;

    public static DetailRow From(CleanVerdict verdict, bool kept)
    {
        var confidence = verdict.Classification == Classification.Trash
            ? (verdict.Score >= 90 ? "确定垃圾" : "高置信度")
            : kept
                ? "置信度不足，已保留"
                : "按设置已过滤";
        return new DetailRow
        {
            Path = verdict.Entry.Path,
            FileName = string.IsNullOrEmpty(verdict.Entry.FileName) ? verdict.Entry.Path : verdict.Entry.FileName,
            Reason = verdict.Reason,
            Confidence = confidence,
        };
    }
}
