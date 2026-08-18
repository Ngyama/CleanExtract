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
    private FileAppLog? _log;

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
            _log = new FileAppLog(AppPaths.TodayLogFile, alsoConsole: true);
            _log.Info("Clean Extract starting.");

            if (TryHandleCli(e.Args))
                return;

            var config = ConfigStore.Load(AppContext.BaseDirectory, AppPaths.UserDataDirectory, _log);
            var backend = new SevenZipBackend(SevenZipLocator.Find(), _log);
            var cleaner = new CleanerEngine(config.Rules);
            var appState = new AppState(config.Rules, config.Settings, cleaner, _log);

            var window = new MainWindow();
            var prompt = new UiPasswordPrompt(window);
            var workflow = new CleanExtractWorkflow(backend, cleaner, prompt, _log);
            var viewModel = new MainViewModel(backend, workflow, appState);
            viewModel.OpenSettingsRequested = () =>
            {
                var settings = new SettingsWindow(appState) { Owner = window };
                settings.ShowDialog();
            };
            window.DataContext = viewModel;
            window.ViewModel = viewModel;

            MainWindow = window;
            window.Show();

            var archive = e.Args.Select(arg => arg.Trim('"')).FirstOrDefault(File.Exists);
            if (archive is not null)
                window.Loaded += async (_, _) => await viewModel.OpenAndExtractAsync(archive);
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

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.Info("Clean Extract exiting.");
        _log?.Dispose();
        base.OnExit(e);
    }
}
