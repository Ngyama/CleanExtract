using System.Diagnostics;
using System.Text;
using CleanExtract.Core.Archive;
using CleanExtract.Core.Logging;

namespace CleanExtract.Core.SevenZip;

internal sealed class SevenZipProcessRunner
{
    private readonly string _executablePath;
    private readonly IAppLog _log;

    public SevenZipProcessRunner(string executablePath, IAppLog log)
    {
        _executablePath = executablePath;
        _log = log;
    }

    public async Task<SevenZipProcessResult> RunTextAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return await RunCoreAsync(arguments, workingDirectory, captureStdoutAsBinary: false, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SevenZipProcessResult> RunBinaryStdoutAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        return await RunCoreAsync(arguments, workingDirectory, captureStdoutAsBinary: true, progress: null, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SevenZipProcessResult> RunCoreAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        bool captureStdoutAsBinary,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_executablePath))
            throw new ArchiveBackendNotFoundException(_executablePath);

        var psi = new ProcessStartInfo
        {
            FileName = _executablePath,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Path.GetDirectoryName(_executablePath) ?? Environment.CurrentDirectory
                : workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        _log.Info($"7-Zip start: {Path.GetFileName(_executablePath)} {SevenZipArgumentRedactor.Redact(arguments)}");

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
                throw new ArchiveException("Failed to start the 7-Zip process.");
        }
        catch (Exception ex) when (ex is not ArchiveException)
        {
            throw new ArchiveException("Failed to start the 7-Zip process.", ex);
        }

        try
        {
            process.StandardInput.Close();
        }
        catch
        {
            // Ignore stdin close failures; 7-Zip does not need input.
        }

        var passwordSupplied = arguments.Any(static a => a.StartsWith("-p", StringComparison.Ordinal) && a.Length > 2);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stdoutBytes = new MemoryStream();
        var stderr = new StringBuilder();
        var stdoutText = new StringBuilder();

        Task stdoutTask;
        if (captureStdoutAsBinary)
        {
            stdoutTask = CopyToAsync(process.StandardOutput.BaseStream, stdoutBytes, linked.Token);
        }
        else
        {
            stdoutTask = ReadLinesAsync(
                process.StandardOutput,
                stdoutText,
                progress: null,
                passwordSupplied,
                process,
                linked.Token);
        }

        var stderrTask = ReadLinesAsync(
            process.StandardError,
            stderr,
            progress,
            passwordSupplied,
            process,
            linked.Token);

        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch (PasswordRequiredException)
        {
            TryKill(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new OperationCancelledArchiveException();
        }

        var result = new SevenZipProcessResult
        {
            ExitCode = process.ExitCode,
            StdoutText = captureStdoutAsBinary ? string.Empty : stdoutText.ToString(),
            StdoutBytes = captureStdoutAsBinary ? stdoutBytes.ToArray() : [],
            StderrText = stderr.ToString(),
        };

        _log.Info($"7-Zip exit code: {result.ExitCode}");
        if (!string.IsNullOrWhiteSpace(result.StderrText))
            _log.Debug($"7-Zip stderr: {TrimForLog(result.StderrText)}");

        return result;
    }

    private static async Task CopyToAsync(Stream source, MemoryStream destination, CancellationToken cancellationToken)
    {
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        StringBuilder sink,
        IProgress<double>? progress,
        bool passwordSupplied,
        Process process,
        CancellationToken cancellationToken)
    {
        var buffer = new char[256];
        var line = new StringBuilder();
        var skipLf = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
                break;

            for (var i = 0; i < count; i++)
            {
                var ch = buffer[i];
                if (skipLf)
                {
                    skipLf = false;
                    if (ch == '\n')
                        continue;
                }

                if (ch == '\r')
                {
                    skipLf = true;
                    HandleOutputLine(line, sink, progress, passwordSupplied, process);
                    continue;
                }

                if (ch == '\n')
                {
                    HandleOutputLine(line, sink, progress, passwordSupplied, process);
                    continue;
                }

                line.Append(ch);
            }
        }

        HandleOutputLine(line, sink, progress, passwordSupplied, process);
    }

    private static void HandleOutputLine(
        StringBuilder line,
        StringBuilder sink,
        IProgress<double>? progress,
        bool passwordSupplied,
        Process process)
    {
        var text = line.ToString();
        line.Clear();
        sink.AppendLine(text);
        if (text.Length == 0)
            return;

        if (!passwordSupplied && IsPasswordPrompt(text))
        {
            TryKill(process);
            throw new PasswordRequiredException();
        }

        if (progress is not null && SevenZipProgressParser.TryParsePercent(text, out var percent))
            progress.Report(percent);
    }

    private static bool IsPasswordPrompt(string line)
        => line.Contains("Enter password", StringComparison.OrdinalIgnoreCase);

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cancellation.
        }
    }

    private static string TrimForLog(string text)
    {
        var compact = text.ReplaceLineEndings(" ").Trim();
        return compact.Length <= 800 ? compact : compact[..800] + "...";
    }
}

internal sealed class SevenZipProcessResult
{
    public required int ExitCode { get; init; }
    public required string StdoutText { get; init; }
    public required byte[] StdoutBytes { get; init; }
    public required string StderrText { get; init; }
}

internal static class SevenZipArgumentRedactor
{
    public static string Redact(IReadOnlyList<string> arguments)
    {
        return string.Join(' ', arguments.Select(RedactOne));
    }

    private static string RedactOne(string argument)
    {
        if (argument.StartsWith("-p", StringComparison.Ordinal) && argument.Length > 2)
            return "-p***";
        return argument;
    }
}

internal static class SevenZipProgressParser
{
    public static bool TryParsePercent(string line, out double percent)
    {
        percent = 0;
        var text = line.Trim();
        var index = text.IndexOf('%');
        if (index <= 0)
            return false;

        var start = index - 1;
        while (start >= 0 && char.IsDigit(text[start]))
            start--;
        start++;
        if (start >= index)
            return false;
        if (!int.TryParse(text[start..index], out var value))
            return false;
        percent = Math.Clamp(value, 0, 100);
        return true;
    }
}
