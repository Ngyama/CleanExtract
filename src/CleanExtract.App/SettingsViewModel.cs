using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CleanExtract.Core;
using CleanExtract.Core.Config;
using CleanExtract.Core.Shell;

namespace CleanExtract;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly AppState _state;
    private bool _filterMacosMetadata;
    private bool _filterThumbsDb;
    private bool _filterDesktopIni;
    private bool _keepSuspicious;
    private bool _enableAdFilenameDetection;
    private bool _enableUrlInspection;
    private bool _enableTextInspection;
    private bool _enableImageAdDetection;
    private bool _checkForUpdates;
    private bool _shellInstalled;
    private string _shellStatus = string.Empty;
    private string _highPhrases = string.Empty;
    private string _mediumPhrases = string.Empty;
    private string _lowPhrases = string.Empty;
    private string _promoPhrases = string.Empty;
    private string _blockedDomains = string.Empty;
    private string _trustedDomains = string.Empty;
    private string _suspiciousDomains = string.Empty;
    private string _alwaysKeep = string.Empty;
    private string _alwaysFilter = string.Empty;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(AppState state)
    {
        _state = state;
        SaveCommand = new RelayCommand(Save);
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        InstallShellCommand = new RelayCommand(InstallShell);
        UninstallShellCommand = new RelayCommand(UninstallShell);
        OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder);
        CheckUpdatesCommand = new RelayCommand(() => _ = CheckUpdatesAsync());
        LoadFromState();
        RefreshShellStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SaveCommand { get; }
    public ICommand RestoreDefaultsCommand { get; }
    public ICommand InstallShellCommand { get; }
    public ICommand UninstallShellCommand { get; }
    public ICommand OpenConfigFolderCommand { get; }
    public ICommand CheckUpdatesCommand { get; }

    public bool FilterMacosMetadata { get => _filterMacosMetadata; set => Set(ref _filterMacosMetadata, value); }
    public bool FilterThumbsDb { get => _filterThumbsDb; set => Set(ref _filterThumbsDb, value); }
    public bool FilterDesktopIni { get => _filterDesktopIni; set => Set(ref _filterDesktopIni, value); }
    public bool KeepSuspicious { get => _keepSuspicious; set => Set(ref _keepSuspicious, value); }
    public bool EnableAdFilenameDetection { get => _enableAdFilenameDetection; set => Set(ref _enableAdFilenameDetection, value); }
    public bool EnableUrlInspection { get => _enableUrlInspection; set => Set(ref _enableUrlInspection, value); }
    public bool EnableTextInspection { get => _enableTextInspection; set => Set(ref _enableTextInspection, value); }
    public bool EnableImageAdDetection { get => _enableImageAdDetection; set => Set(ref _enableImageAdDetection, value); }
    public bool CheckForUpdates { get => _checkForUpdates; set => Set(ref _checkForUpdates, value); }
    public bool ShellInstalled { get => _shellInstalled; private set => Set(ref _shellInstalled, value); }
    public string ShellStatus { get => _shellStatus; private set => Set(ref _shellStatus, value); }
    public string HighPhrases { get => _highPhrases; set => Set(ref _highPhrases, value); }
    public string MediumPhrases { get => _mediumPhrases; set => Set(ref _mediumPhrases, value); }
    public string LowPhrases { get => _lowPhrases; set => Set(ref _lowPhrases, value); }
    public string PromoPhrases { get => _promoPhrases; set => Set(ref _promoPhrases, value); }
    public string BlockedDomains { get => _blockedDomains; set => Set(ref _blockedDomains, value); }
    public string TrustedDomains { get => _trustedDomains; set => Set(ref _trustedDomains, value); }
    public string SuspiciousDomains { get => _suspiciousDomains; set => Set(ref _suspiciousDomains, value); }
    public string AlwaysKeep { get => _alwaysKeep; set => Set(ref _alwaysKeep, value); }
    public string AlwaysFilter { get => _alwaysFilter; set => Set(ref _alwaysFilter, value); }
    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }

    private void LoadFromState()
    {
        var rules = _state.Rules;
        var settings = _state.Settings;
        FilterMacosMetadata = settings.FilterMacosMetadata;
        FilterThumbsDb = settings.FilterThumbsDb;
        FilterDesktopIni = settings.FilterDesktopIni;
        KeepSuspicious = settings.KeepSuspicious;
        EnableAdFilenameDetection = settings.EnableAdFilenameDetection;
        EnableUrlInspection = settings.EnableUrlInspection;
        EnableTextInspection = settings.EnableTextInspection;
        EnableImageAdDetection = settings.EnableImageAdDetection;
        CheckForUpdates = settings.CheckForUpdates;
        HighPhrases = ListText.Join(rules.AdPhrasesHigh);
        MediumPhrases = ListText.Join(rules.AdPhrasesMedium);
        LowPhrases = ListText.Join(rules.AdPhrasesLow);
        PromoPhrases = ListText.Join(rules.PromoContentPhrases);
        BlockedDomains = ListText.Join(rules.BlockedDomains);
        TrustedDomains = ListText.Join(rules.TrustedDomains);
        SuspiciousDomains = ListText.Join(rules.SuspiciousDomains);
        AlwaysKeep = ListText.Join(rules.AlwaysKeepNames);
        AlwaysFilter = ListText.Join(rules.AlwaysFilterNames);
    }

    private void Save()
    {
        ApplyEditorToState();
        _state.SaveAll();
        StatusMessage = "已保存。下次解压会使用这些规则。";
    }

    private void RestoreDefaults()
    {
        if (MessageBox.Show(
                "恢复关键词、域名和开关为默认值？始终保留/过滤的文件名会保留。",
                "Clean Extract",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        _state.RestoreRuleDefaults();
        LoadFromState();
        StatusMessage = "已恢复默认规则。";
    }

    private void ApplyEditorToState()
    {
        var settings = _state.Settings;
        settings.FilterMacosMetadata = FilterMacosMetadata;
        settings.FilterThumbsDb = FilterThumbsDb;
        settings.FilterDesktopIni = FilterDesktopIni;
        settings.KeepSuspicious = KeepSuspicious;
        settings.EnableAdFilenameDetection = EnableAdFilenameDetection;
        settings.EnableUrlInspection = EnableUrlInspection;
        settings.EnableTextInspection = EnableTextInspection;
        settings.EnableImageAdDetection = EnableImageAdDetection;
        settings.CheckForUpdates = CheckForUpdates;

        var rules = _state.Rules;
        Replace(rules.AdPhrasesHigh, ListText.Split(HighPhrases));
        Replace(rules.AdPhrasesMedium, ListText.Split(MediumPhrases));
        Replace(rules.AdPhrasesLow, ListText.Split(LowPhrases));
        Replace(rules.PromoContentPhrases, ListText.Split(PromoPhrases));
        Replace(rules.BlockedDomains, ListText.Split(BlockedDomains));
        Replace(rules.TrustedDomains, ListText.Split(TrustedDomains));
        Replace(rules.SuspiciousDomains, ListText.Split(SuspiciousDomains));
        Replace(rules.AlwaysKeepNames, ListText.Split(AlwaysKeep));
        Replace(rules.AlwaysFilterNames, ListText.Split(AlwaysFilter));
        settings.ApplyTo(rules);
    }

    private void InstallShell()
    {
        try
        {
            var exe = CurrentExecutable();
            ExplorerIntegration.Install(exe);
            RefreshShellStatus();
            StatusMessage = "已添加到资源管理器右键菜单。";
        }
        catch (Exception ex)
        {
            StatusMessage = "无法安装右键菜单：" + ex.Message;
        }
    }

    private void UninstallShell()
    {
        try
        {
            ExplorerIntegration.Uninstall();
            RefreshShellStatus();
            StatusMessage = "已移除右键菜单。";
        }
        catch (Exception ex)
        {
            StatusMessage = "无法移除右键菜单：" + ex.Message;
        }
    }

    private void RefreshShellStatus()
    {
        var exe = CurrentExecutable();
        ShellInstalled = ExplorerIntegration.IsInstalled();
        if (ExplorerIntegration.IsInstalledFor(exe))
            ShellStatus = "右键菜单已安装，指向当前程序。";
        else if (ShellInstalled)
            ShellStatus = "右键菜单已安装，但指向另一个位置。可以重新安装以修复。";
        else
            ShellStatus = "尚未安装右键菜单。安装后，可在 ZIP / RAR / 7z 上右键选择“干净解压”。";
    }

    private async Task CheckUpdatesAsync()
    {
        ApplyEditorToState();
        _state.SaveAll();
        StatusMessage = "正在检查更新...";
        var result = await AppUpdate.CheckAsync(_state.Settings, _state.Log, downloadAndRestart: false);
        StatusMessage = result.Message;
        if (result.UpdateAvailable && result.Version is not null)
        {
            var apply = MessageBox.Show(
                $"发现新版本 {result.Version}。下载并重启以完成更新？",
                "Clean Extract",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            if (apply != MessageBoxResult.OK)
                return;

            StatusMessage = "正在下载更新...";
            var applied = await AppUpdate.CheckAsync(_state.Settings, _state.Log, downloadAndRestart: true);
            StatusMessage = applied.Message;
        }
    }

    private static void OpenConfigFolder()
    {
        Directory.CreateDirectory(AppPaths.UserDataDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.UserDataDirectory,
            UseShellExecute = true,
        });
    }

    private static string CurrentExecutable()
        => Environment.ProcessPath
           ?? Path.Combine(AppContext.BaseDirectory, "CleanExtract.exe");

    private static void Replace(List<string> target, List<string> source)
    {
        target.Clear();
        target.AddRange(source);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
