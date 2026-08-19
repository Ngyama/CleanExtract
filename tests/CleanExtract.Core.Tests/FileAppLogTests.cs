using CleanExtract.Core.Logging;

namespace CleanExtract.Core.Tests;

public sealed class FileAppLogTests
{
    [Fact]
    public void TwoWriters_CanShareTheSameLogFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "cleanextract-log-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            using (var first = new FileAppLog(path))
            using (var second = new FileAppLog(path))
            {
                first.Info("one");
                second.Info("two");
            }

            Assert.True(File.Exists(path));
            var text = File.ReadAllText(path);
            Assert.Contains("one", text);
            Assert.Contains("two", text);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }
    }
}
