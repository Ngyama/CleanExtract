using System.Windows;
using CleanExtract.Core.Shell;
using Velopack;

namespace CleanExtract;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build()
            .OnFirstRun(_ =>
            {
                try
                {
                    var exe = Environment.ProcessPath;
                    if (!string.IsNullOrWhiteSpace(exe))
                        ExplorerIntegration.Install(exe);
                }
                catch
                {
                    // First-run Explorer menu install is best-effort.
                }
            })
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
