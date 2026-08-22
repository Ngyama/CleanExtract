using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CleanExtract.Core;
using CleanExtract.Core.Cleaning;
using CleanExtract.Core.IO;
using CleanExtract.Core.Logging;
using CleanExtract.Core.Workflow;
using Microsoft.Win32;

namespace CleanExtract;

public enum UiState
{
    Empty,
    Analyzing,
    Preview,
    Running,
    Success,
    Failed,
    Details,
}

public enum OutputPlacement
{
    SiblingFolder,
    ArchiveDirectory,
    Custom,
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly CleanExtractWorkflow _workflow;
    private readonly IAppLog _log;
    private readonly AppState _app;
    private CancellationTokenSource? _cts;
    private ExtractPlan? _plan;
    private int _loadGeneration;
    private OutputPlacement _outputPlacement = OutputPlacement.SiblingFolder;

    private UiState _state = UiState.Empty;
    private string _archivePath = string.Empty;
    private string _archiveName = string.Empty;
    private string _archiveMeta = string.Empty;
    private double _progressPercent;
    private bool _isProgressIndeterminate = true;
    private string _statusText = string.Empty;
    private string _errorHeadline = "无法完成解压";
    private string _errorText = string.Empty;
    private string _resultHeadline = string.Empty;
    private string _resultDetail = string.Empty;
    private string _previewSummary = string.Empty;
    private string _outputDirectory = string.Empty;
    private string _outputHint = string.Empty;
    private string _previewNote = string.Empty;
    private FilterSummary? _summary;

    public MainViewModel(CleanExtractWorkflow workflow, AppState state)
    {
        _workflow = workflow;
        _app = state;
        _log = state.Log;
        BrowseCommand = new RelayCommand(Browse, () => !IsBusy);
        ExtractCommand = new RelayCommand(() => _ = ExtractAsync(), () => State is UiState.Preview && _plan is not null);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        ResetCommand = new RelayCommand(Reset, () => !IsBusy);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => !string.IsNullOrEmpty(Summary?.OutputDirectory));
        ShowDetailsCommand = new RelayCommand(() => State = UiState.Details, () => Summary is not null);
        HideDetailsCommand = new RelayCommand(() => State = UiState.Success);
        SettingsCommand = new RelayCommand(OpenSettings, () => !IsBusy);
        AlwaysKeepCommand = new RelayCommand<DetailRow>(AlwaysKeep);
        AlwaysFilterCommand = new RelayCommand<DetailRow>(AlwaysFilter);
        BrowseOutputCommand = new RelayCommand(BrowseOutput, () => State is UiState.Preview);
        ExtractHereCommand = new RelayCommand(ExtractHere, () => State is UiState.Preview && !string.IsNullOrEmpty(ArchivePath));
        UseDefaultOutputCommand = new RelayCommand(UseDefaultOutput, () => State is UiState.Preview && !string.IsNullOrEmpty(ArchivePath));
    }

    public Action? OpenSettingsRequested { get; set; }

    private void OpenSettings() => OpenSettingsRequested?.Invoke();

    private void AlwaysKeep(DetailRow? row)
    {
        if (row is null)
            return;
        _app.AlwaysKeep(row.FileName);
        if (State is UiState.Preview)
        {
            RecategorizePreview($"已记住：本次和以后都将保留 {row.FileName}");
            return;
        }

        row.StatusNote = $"已记住：下次将保留 {row.FileName}";
    }

    private void AlwaysFilter(DetailRow? row)
    {
        if (row is null)
            return;
        _app.AlwaysFilter(row.FileName);
        if (State is UiState.Preview)
        {
            RecategorizePreview($"已记住：本次和以后都将过滤 {row.FileName}");
            return;
        }

        row.StatusNote = $"已记住：下次将过滤 {row.FileName}";
    }

    private void RecategorizePreview(string note)
    {
        if (_plan is null)
            return;
        _plan = _workflow.Recategorize(_plan);
        PreviewNote = note;
        FillDetails(_plan.Verdicts, pending: true);
        PreviewSummary = BuildPreviewSummary(_plan.Verdicts, _app.Rules.KeepSuspicious);
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
    public ICommand BrowseOutputCommand { get; }
    public ICommand ExtractHereCommand { get; }
    public ICommand UseDefaultOutputCommand { get; }

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

    public string ErrorHeadline
    {
        get => _errorHeadline;
        private set => Set(ref _errorHeadline, value);
    }

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

    public string PreviewSummary
    {
        get => _previewSummary;
        private set => Set(ref _previewSummary, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        private set => Set(ref _outputDirectory, value);
    }

    public string OutputHint
    {
        get => _outputHint;
        private set => Set(ref _outputHint, value);
    }

    public string PreviewNote
    {
        get => _previewNote;
        private set
        {
            if (Set(ref _previewNote, value))
                OnPropertyChanged(nameof(HasPreviewNote));
        }
    }

    public bool HasPreviewNote => !string.IsNullOrWhiteSpace(PreviewNote);

    public FilterSummary? Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public bool IsEmpty => State is UiState.Empty;
    public bool IsPreview => State is UiState.Preview;
    public bool IsBusy => State is UiState.Analyzing or UiState.Running;
    public bool IsSuccess => State is UiState.Success;
    public bool IsFailed => State is UiState.Failed;
    public bool IsDetails => State is UiState.Details;
    public bool ShowHome => State is UiState.Empty;
    public bool HasTrash => TrashRows.Count > 0;
    public bool HasSuspicious => SuspiciousRows.Count > 0;
    public bool IsDefaultOutput => _outputPlacement is OutputPlacement.SiblingFolder;
    public bool ShowRestoreOutput => _outputPlacement is not OutputPlacement.SiblingFolder;

    public async Task SetArchiveAsync(string path)
    {
        if (State is UiState.Running)
            return;

        _cts?.Cancel();
        var generation = ++_loadGeneration;
        _cts = new CancellationTokenSource();
        var cancellation = _cts.Token;

        if (!File.Exists(path))
        {
            Fail("找不到这个压缩包。", "无法打开压缩包");
            return;
        }

        ArchivePath = Path.GetFullPath(path);
        ArchiveName = Path.GetFileName(ArchivePath);
        var size = new FileInfo(ArchivePath).Length;
        ArchiveMeta = FileSizeFormatter.Format(size);
        Summary = null;
        _plan = null;
        ErrorText = string.Empty;
        PreviewNote = string.Empty;
        PreviewSummary = string.Empty;
        TrashRows.Clear();
        SuspiciousRows.Clear();
        State = UiState.Analyzing;
        StatusText = "正在分析压缩包...";
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
        NotifyState();

        var progress = CreateProgress();
        try
        {
            var plan = await _workflow.AnalyzeAsync(ArchivePath, progress, cancellation).ConfigureAwait(true);
            if (generation != _loadGeneration)
                return;

            _plan = plan;
            var files = plan.Entries.Count(e => !e.IsDirectory);
            ArchiveMeta = $"{FileSizeFormatter.Format(size)}  ·  {files} 个文件";
            PreviewSummary = BuildPreviewSummary(plan.Verdicts, _app.Rules.KeepSuspicious);
            FillDetails(plan.Verdicts, pending: true);
            UseDefaultOutput();
            State = UiState.Preview;
        }
        catch (OperationCanceledException)
        {
            if (generation != _loadGeneration)
                return;
            Fail("已取消。", "已取消");
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
                return;
            _log.Error("Analyze failed", ex);
            Fail(UserMessages.For(ex), "无法分析压缩包");
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                _cts.Dispose();
                _cts = null;
                NotifyState();
            }
        }
    }

    public Task OpenAndExtractAsync(string path) => SetArchiveAsync(path);

    public void Browse()
    {
        var dialog = new OpenFileDialog
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
        if (_plan is null || State is not UiState.Preview)
            return;

        _cts = new CancellationTokenSource();
        State = UiState.Running;
        StatusText = "正在解压...";
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
        NotifyState();

        var progress = CreateProgress();
        var uniquify = _outputPlacement is OutputPlacement.SiblingFolder;
        try
        {
            var result = await _workflow.ExtractAsync(
                    _plan,
                    OutputDirectory,
                    uniquify,
                    progress,
                    _cts.Token)
                .ConfigureAwait(true);
            Summary = result.Summary;
            ResultHeadline = "解压完成";
            ResultDetail = BuildResultDetail(result.Summary, _app.Rules.KeepSuspicious);
            if (!string.IsNullOrWhiteSpace(result.Warning))
                ResultDetail += Environment.NewLine + result.Warning;
            FillDetails(result.Summary.Verdicts, pending: false);
            State = UiState.Success;
        }
        catch (OperationCanceledException)
        {
            State = UiState.Preview;
            PreviewNote = "已取消解压，压缩包尚未写入磁盘。";
        }
        catch (Exception ex)
        {
            _log.Error("Extract failed", ex);
            Fail(UserMessages.For(ex), "无法完成解压");
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
        if (State is UiState.Failed && _plan is not null && !string.IsNullOrEmpty(ArchivePath))
        {
            ErrorText = string.Empty;
            State = UiState.Preview;
            NotifyState();
            return;
        }

        _cts?.Cancel();
        _loadGeneration++;
        _plan = null;
        ArchivePath = string.Empty;
        ArchiveName = string.Empty;
        ArchiveMeta = string.Empty;
        StatusText = string.Empty;
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
        ErrorHeadline = "无法完成解压";
        ErrorText = string.Empty;
        ResultHeadline = string.Empty;
        ResultDetail = string.Empty;
        PreviewSummary = string.Empty;
        OutputDirectory = string.Empty;
        OutputHint = string.Empty;
        PreviewNote = string.Empty;
        Summary = null;
        _outputPlacement = OutputPlacement.SiblingFolder;
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

    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择解压目录",
            InitialDirectory = Directory.Exists(OutputDirectory)
                ? OutputDirectory
                : OutputDirectoryResolver.ArchiveParent(ArchivePath),
        };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            return;

        _outputPlacement = OutputPlacement.Custom;
        OutputDirectory = Path.GetFullPath(dialog.FolderName);
        OutputHint = "将解压到你选择的文件夹。已有同名文件会被覆盖。";
        NotifyOutput();
    }

    private void ExtractHere()
    {
        _outputPlacement = OutputPlacement.ArchiveDirectory;
        OutputDirectory = OutputDirectoryResolver.ArchiveParent(ArchivePath);
        OutputHint = "文件将直接解压到压缩包所在目录。已有同名文件会被覆盖。";
        NotifyOutput();
    }

    private void UseDefaultOutput()
    {
        _outputPlacement = OutputPlacement.SiblingFolder;
        OutputDirectory = OutputDirectoryResolver.Resolve(ArchivePath);
        OutputHint = "将解压到独立文件夹。若该文件夹已有内容，会自动加序号。";
        NotifyOutput();
    }

    private void NotifyOutput()
    {
        OnPropertyChanged(nameof(IsDefaultOutput));
        OnPropertyChanged(nameof(ShowRestoreOutput));
        RaiseCommands();
    }

    private void FillDetails(IReadOnlyList<CleanVerdict> verdicts, bool pending)
    {
        TrashRows.Clear();
        SuspiciousRows.Clear();
        var keepSuspicious = _app.Rules.KeepSuspicious;
        foreach (var item in verdicts.Where(v => !v.ShouldExtract(keepSuspicious)))
            TrashRows.Add(DetailRow.From(item, kept: false, pending));
        foreach (var item in verdicts.Where(v =>
                     v.Classification == Classification.Suspicious && v.ShouldExtract(keepSuspicious)))
            SuspiciousRows.Add(DetailRow.From(item, kept: true, pending));
        OnPropertyChanged(nameof(HasTrash));
        OnPropertyChanged(nameof(HasSuspicious));
    }

    private static string BuildPreviewSummary(IReadOnlyList<CleanVerdict> verdicts, bool keepSuspicious)
    {
        var files = verdicts.Count(v => !v.Entry.IsDirectory);
        var excluded = verdicts.Count(v => !v.ShouldExtract(keepSuspicious) && !v.Entry.IsDirectory);
        var suspiciousKept = verdicts.Count(v =>
            v.Classification == Classification.Suspicious && v.ShouldExtract(keepSuspicious) && !v.Entry.IsDirectory);

        if (excluded == 0 && suspiciousKept == 0)
            return $"{files} 个文件，未发现需要过滤的内容。";

        var parts = new List<string> { $"{files} 个文件" };
        if (excluded > 0)
            parts.Add($"将跳过 {excluded} 个");
        if (suspiciousKept > 0)
            parts.Add($"{suspiciousKept} 个可疑文件将保留");
        return string.Join(" · ", parts);
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

    private Progress<WorkflowProgress> CreateProgress()
    {
        return new Progress<WorkflowProgress>(p =>
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
    }

    private void Fail(string message, string headline)
    {
        ErrorHeadline = headline;
        ErrorText = message;
        State = UiState.Failed;
        NotifyState();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsPreview));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsDetails));
        OnPropertyChanged(nameof(ShowHome));
        OnPropertyChanged(nameof(IsDefaultOutput));
        OnPropertyChanged(nameof(ShowRestoreOutput));
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
        (BrowseOutputCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExtractHereCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (UseDefaultOutputCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

    public static DetailRow From(CleanVerdict verdict, bool kept, bool pending = false)
    {
        var confidence = verdict.Classification == Classification.Trash
            ? pending
                ? (verdict.Score >= 90 ? "将跳过" : "高置信度，将跳过")
                : (verdict.Score >= 90 ? "确定垃圾" : "高置信度")
            : pending
                ? kept ? "可疑，将保留" : "按设置将过滤"
                : kept ? "置信度不足，已保留" : "按设置已过滤";
        return new DetailRow
        {
            Path = verdict.Entry.Path,
            FileName = string.IsNullOrEmpty(verdict.Entry.FileName) ? verdict.Entry.Path : verdict.Entry.FileName,
            Reason = verdict.Reason,
            Confidence = confidence,
        };
    }
}
