using CleanExtract.Core.Archive;
using CleanExtract.Core.Logging;

namespace CleanExtract.Core.SevenZip;

public sealed class SevenZipBackend : IArchiveBackend
{
    private readonly SevenZipProcessRunner _runner;
    private readonly IAppLog _log;

    public SevenZipBackend(string executablePath, IAppLog log)
    {
        ExecutablePath = executablePath;
        _log = log;
        _runner = new SevenZipProcessRunner(executablePath, log);
    }

    public string ExecutablePath { get; }

    public async Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(
        string archivePath,
        string? password,
        CancellationToken cancellationToken = default)
    {
        EnsureArchiveExists(archivePath);
        var args = new List<string>
        {
            "l",
            "-slt",
            "-sccUTF-8",
            "-bso1",
            "-bse2",
            "-bsp0",
        };
        AddPassword(args, password);
        args.Add("--");
        args.Add(archivePath);

        var result = await _runner.RunTextAsync(
            args,
            Path.GetDirectoryName(archivePath),
            progress: null,
            cancellationToken).ConfigureAwait(false);

        SevenZipErrorMapper.ThrowIfFailed(result, passwordProvided: !string.IsNullOrEmpty(password));

        var entries = SevenZipListParser.Parse(result.StdoutText);
        _log.Info($"Listed {entries.Count} entries from archive.");
        return entries;
    }

    public async Task<byte[]> ReadEntryAsync(
        string archivePath,
        ArchiveEntry entry,
        string? password,
        CancellationToken cancellationToken = default)
    {
        EnsureArchiveExists(archivePath);
        if (entry.IsDirectory)
            return [];

        var args = new List<string>
        {
            "e",
            "-so",
            "-y",
            "-sccUTF-8",
            "-spd",
            "-bso0",
            "-bse2",
            "-bsp2",
        };
        AddPassword(args, password);
        args.Add("--");
        args.Add(archivePath);
        args.Add(entry.Path);

        var result = await _runner.RunBinaryStdoutAsync(
            args,
            Path.GetDirectoryName(archivePath),
            cancellationToken).ConfigureAwait(false);

        SevenZipErrorMapper.ThrowIfFailed(result, passwordProvided: !string.IsNullOrEmpty(password));
        return result.StdoutBytes;
    }

    public async Task<ExtractResult> ExtractAsync(
        string archivePath,
        string outputDirectory,
        IReadOnlyList<string> excludedPaths,
        string? password,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureArchiveExists(archivePath);
        Directory.CreateDirectory(outputDirectory);

        var args = new List<string>
        {
            "x",
            "-y",
            "-sccUTF-8",
            "-spd",
            "-aoa",
            "-bso0",
            "-bse2",
            "-bsp2",
            $"-o{outputDirectory}",
        };
        AddPassword(args, password);

        string? excludeFile = null;
        try
        {
            if (excludedPaths.Count > 0)
            {
                excludeFile = await WriteExcludeListAsync(excludedPaths, cancellationToken).ConfigureAwait(false);
                args.Add("-scsUTF-8");
                args.Add($"-x@{excludeFile}");
                _log.Info($"Extract excluding {excludedPaths.Count} entries.");
            }

            args.Add("--");
            args.Add(archivePath);

            var result = await _runner.RunTextAsync(
                args,
                Path.GetDirectoryName(archivePath),
                progress,
                cancellationToken).ConfigureAwait(false);

            SevenZipErrorMapper.ThrowIfFailed(result, passwordProvided: !string.IsNullOrEmpty(password));

            return new ExtractResult
            {
                OutputDirectory = outputDirectory,
                ExitCode = result.ExitCode,
                Succeeded = result.ExitCode is 0 or 1,
                Warning = result.ExitCode == 1
                    ? "7-Zip completed with warnings. Some files may have been skipped."
                    : null,
            };
        }
        finally
        {
            if (excludeFile is not null)
            {
                try
                {
                    File.Delete(excludeFile);
                }
                catch
                {
                    // Temp cleanup is best-effort.
                }
            }
        }
    }

    private static void AddPassword(List<string> args, string? password)
    {
        if (!string.IsNullOrEmpty(password))
            args.Add("-p" + password);
    }

    private static void EnsureArchiveExists(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new ArchiveException($"Archive not found: {archivePath}");
    }

    private static async Task<string> WriteExcludeListAsync(
        IReadOnlyList<string> excludedPaths,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cleanextract-exclude-{Guid.NewGuid():N}.txt");
        var lines = excludedPaths
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        await File.WriteAllLinesAsync(path, lines, new System.Text.UTF8Encoding(false), cancellationToken)
            .ConfigureAwait(false);
        return path;
    }
}
