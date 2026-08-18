using System.Text;

namespace CleanExtract.Core.Logging;

public sealed class FileAppLog : IAppLog, IDisposable
{
    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly bool _alsoConsole;
    private StreamWriter? _writer;

    public FileAppLog(string filePath, bool alsoConsole = false)
    {
        _filePath = filePath;
        _alsoConsole = alsoConsole;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
    }

    public string FilePath => _filePath;

    public void Debug(string message) => Write("DEBUG", message, null);

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {Sanitize(message)}";
        if (exception is not null)
            line += $" | {exception.GetType().Name}: {Sanitize(exception.Message)}";

        lock (_gate)
        {
            _writer?.WriteLine(line);
        }

        if (_alsoConsole)
            System.Diagnostics.Debug.WriteLine(line);
    }

    private static string Sanitize(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var text = message;
        // Never keep password switch values if a caller accidentally includes them.
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"-p\S+",
            "-p***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return text.ReplaceLineEndings(" ");
    }
}
