using System.Security.Cryptography;
using System.Text;

namespace CleanExtract.Core.Logging;

public sealed class FileAppLog : IAppLog, IDisposable
{
    private readonly object _gate = new();
    private readonly bool _alsoConsole;
    private readonly Mutex? _mutex;
    private StreamWriter? _writer;

    public FileAppLog(string filePath, bool alsoConsole = false)
        : this(filePath, alsoConsole, FileShare.ReadWrite)
    {
    }

    private FileAppLog(string filePath, bool alsoConsole, FileShare share)
    {
        FilePath = filePath;
        _alsoConsole = alsoConsole;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        _writer = new StreamWriter(
            new FileStream(filePath, FileMode.Append, FileAccess.Write, share, bufferSize: 4096, FileOptions.None),
            new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
        _mutex = CreateFileMutex(filePath);
    }

    public static IAppLog Create(string filePath, bool alsoConsole = false)
    {
        try
        {
            return new FileAppLog(filePath, alsoConsole, FileShare.ReadWrite);
        }
        catch (IOException)
        {
            var directory = Path.GetDirectoryName(filePath)!;
            var alt = Path.Combine(
                directory,
                $"{Path.GetFileNameWithoutExtension(filePath)}-{Environment.ProcessId}{Path.GetExtension(filePath)}");
            try
            {
                return new FileAppLog(alt, alsoConsole, FileShare.ReadWrite);
            }
            catch
            {
                return NullLog.Instance;
            }
        }
    }

    public string FilePath { get; }

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

        _mutex?.Dispose();
    }

    private void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {Sanitize(message)}";
        if (exception is not null)
            line += $" | {exception.GetType().Name}: {Sanitize(exception.Message)}";

        var acquired = false;
        try
        {
            acquired = TryAcquireMutex();
            lock (_gate)
            {
                try
                {
                    if (_writer is null)
                        return;

                    _writer.BaseStream.Seek(0, SeekOrigin.End);
                    _writer.WriteLine(line);
                }
                catch
                {
                    // Logging must not crash the app.
                }
            }
        }
        finally
        {
            if (acquired)
            {
                try
                {
                    _mutex!.ReleaseMutex();
                }
                catch
                {
                    // ignore
                }
            }
        }

        if (_alsoConsole)
            System.Diagnostics.Debug.WriteLine(line);
    }

    private bool TryAcquireMutex()
    {
        if (_mutex is null)
            return false;

        try
        {
            return _mutex.WaitOne(TimeSpan.FromSeconds(2));
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Mutex? CreateFileMutex(string filePath)
    {
        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(filePath.ToUpperInvariant())));
            return new Mutex(false, @"Local\CleanExtract.Log." + hash[..16]);
        }
        catch
        {
            return null;
        }
    }

    private static string Sanitize(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var text = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"-p\S+",
            "-p***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return text.ReplaceLineEndings(" ");
    }
}
