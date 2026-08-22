using System.IO;
using System.Windows;
using CleanExtract.Core;
using CleanExtract.Core.Cleaning;
using CleanExtract.Core.Config;
using CleanExtract.Core.Logging;
using CleanExtract.Core.SevenZip;
using CleanExtract.Core.Shell;
using CleanExtract.Core.Workflow;

namespace CleanExtract;

public partial class App : Application
{
    private IAppLog? _log;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            _log?.Error("Unhandled UI exception", args.Exception);
            MessageBox.Show(
                UserMessages.For(args.Exception),
                "Clean Extract",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                _log?.Error("Unhandled exception", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _log?.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        try
        {
            _log = FileAppLog.Create(AppPaths.TodayLogFile, alsoConsole: true);
            _log.Info("Clean Extract starting.");

            var args = StartupArgs.Collect(e.Args);
            if (TryHandleCli(args))
                return;

            var archive = StartupArgs.ResolveArchive(args);
            if (args.Length > 0)
                _log.Info("Startup arguments: " + string.Join(" ", args.Select(StartupArgs.Redact)));
            if (archive is not null)
                _log.Info("Command-line archive: " + archive);

            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe) && ExplorerIntegration.IsInstalledFor(exe))
                ExplorerIntegration.Install(exe);

            var config = ConfigStore.Load(AppContext.BaseDirectory, AppPaths.UserDataDirectory, _log);
            var backend = new SevenZipBackend(SevenZipLocator.Find(), _log);
            var cleaner = new CleanerEngine(config.Rules);
            var appState = new AppState(config.Rules, config.Settings, cleaner, _log);

            var window = new MainWindow();
            var prompt = new UiPasswordPrompt(window);
            var workflow = new CleanExtractWorkflow(backend, cleaner, prompt, _log);
            var viewModel = new MainViewModel(workflow, appState);
            viewModel.OpenSettingsRequested = () =>
            {
                var settings = new SettingsWindow(appState) { Owner = window };
                settings.ShowDialog();
            };
            window.DataContext = viewModel;
            window.ViewModel = viewModel;

            MainWindow = window;
            if (archive is not null)
            {
                window.Loaded += async (_, _) => await viewModel.OpenAndExtractAsync(archive);
            }

            window.Show();

            if (archive is null && appState.Settings.CheckForUpdates)
            {
                window.ContentRendered += async (_, _) =>
                {
                    try
                    {
                        await PromptUpdateAsync(appState, window);
                    }
                    catch (Exception ex)
                    {
                        _log?.Warn("Startup update check failed: " + ex.Message);
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _log?.Error("Startup failed", ex);
            MessageBox.Show(UserMessages.For(ex), "Clean Extract", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private bool TryHandleCli(string[] args)
    {
        if (args.Length == 0)
            return false;

        var quiet = args.Any(arg => arg.Equals("--quiet", StringComparison.OrdinalIgnoreCase)
                                    || arg.Equals("-q", StringComparison.OrdinalIgnoreCase));
        var tokens = args.Where(arg => !arg.Equals("--quiet", StringComparison.OrdinalIgnoreCase)
                                       && !arg.Equals("-q", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (tokens.Length == 0)
            return false;

        var command = tokens[0];
        if (command.Equals("--install-shell", StringComparison.OrdinalIgnoreCase)
            || command.Equals("--install-context-menu", StringComparison.OrdinalIgnoreCase))
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CleanExtract.exe");
            ExplorerIntegration.Install(exe);
            _log?.Info($"Installed Explorer menu for {exe}");
            if (!quiet)
                MessageBox.Show("已添加到资源管理器右键菜单。在 ZIP / RAR / 7z 上右键即可干净解压。", "Clean Extract");
            Shutdown(0);
            return true;
        }

        if (command.Equals("--uninstall-shell", StringComparison.OrdinalIgnoreCase)
            || command.Equals("--uninstall-context-menu", StringComparison.OrdinalIgnoreCase))
        {
            ExplorerIntegration.Uninstall();
            _log?.Info("Removed Explorer menu.");
            if (!quiet)
                MessageBox.Show("已移除资源管理器右键菜单。", "Clean Extract");
            Shutdown(0);
            return true;
        }

        return false;
    }

    private static async Task PromptUpdateAsync(AppState state, Window owner)
    {
        var result = await AppUpdate.CheckAsync(state.Settings, state.Log, downloadAndRestart: false);
        if (!result.UpdateAvailable)
            return;

        var apply = MessageBox.Show(
            owner,
            $"发现新版本 {result.Version}。下载并重启以完成更新？",
            "Clean Extract",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);
        if (apply != MessageBoxResult.OK)
            return;

        await AppUpdate.CheckAsync(state.Settings, state.Log, downloadAndRestart: true);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.Info("Clean Extract exiting.");
        if (_log is IDisposable disposable)
            disposable.Dispose();
        base.OnExit(e);
    }
}
