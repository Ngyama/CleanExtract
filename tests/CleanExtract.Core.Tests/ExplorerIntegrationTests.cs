using CleanExtract.Core.Shell;
using System.Runtime.Versioning;

namespace CleanExtract.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class ExplorerIntegrationTests
{
    [Fact]
    public void CommandLine_QuotesExecutableAndPlaceholder()
    {
        var command = ExplorerIntegration.CommandLine(@"C:\Program Files\Clean Extract\CleanExtract.exe");
        Assert.Equal("\"C:\\Program Files\\Clean Extract\\CleanExtract.exe\" \"%1\"", command);
    }

    [Fact]
    public void InstallUninstall_WritesHkcuAndCleansUp()
    {
        var dummy = Path.Combine(Path.GetTempPath(), "cleanextract-shell-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllBytes(dummy, [0x4D, 0x5A]);
        var previous = ExplorerIntegration.InstalledExecutablePath();
        try
        {
            ExplorerIntegration.Install(dummy);
            Assert.True(ExplorerIntegration.IsInstalledFor(dummy));
            ExplorerIntegration.Uninstall();
            Assert.False(ExplorerIntegration.IsInstalledFor(dummy));
        }
        finally
        {
            try
            {
                ExplorerIntegration.Uninstall();
            }
            catch
            {
                // ignore
            }

            if (!string.IsNullOrWhiteSpace(previous) && File.Exists(previous))
            {
                try
                {
                    ExplorerIntegration.Install(previous);
                }
                catch
                {
                    // ignore restore
                }
            }

            try
            {
                File.Delete(dummy);
            }
            catch
            {
                // ignore
            }
        }
    }
}
