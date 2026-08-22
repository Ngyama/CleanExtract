using CleanExtract.Core.Archive;
using CleanExtract.Core.Cleaning;
using CleanExtract.Core.IO;
using CleanExtract.Core.Logging;

namespace CleanExtract.Core.Workflow;

public sealed class CleanExtractWorkflow
{
    private readonly IArchiveBackend _backend;
    private readonly CleanerEngine _cleaner;
    private readonly IAppLog _log;
    private readonly IPasswordPrompt _passwordPrompt;

    public CleanExtractWorkflow(
        IArchiveBackend backend,
        CleanerEngine cleaner,
        IPasswordPrompt passwordPrompt,
        IAppLog log)
    {
        _backend = backend;
        _cleaner = cleaner;
        _passwordPrompt = passwordPrompt;
        _log = log;
    }

    public async Task<CleanExtractResult> RunAsync(
        string archivePath,
        string? outputDirectory = null,
        bool uniquifyOutput = true,
        IProgress<WorkflowProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var plan = await AnalyzeAsync(archivePath, progress, cancellationToken).ConfigureAwait(false);
        return await ExtractAsync(plan, outputDirectory, uniquifyOutput, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ExtractPlan> AnalyzeAsync(
        string archivePath,
        IProgress<WorkflowProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
            throw new ArchiveException($"Archive not found: {archivePath}");

        var fullArchivePath = Path.GetFullPath(archivePath);
        _log.Info($"Archive: {fullArchivePath}");
        Report(progress, "list", "正在分析压缩包...");

        var (entries, password) = await ListWithPasswordAsync(fullArchivePath, progress, cancellationToken)
            .ConfigureAwait(false);

        _log.Info($"Listed {entries.Count} entries.");
        Report(progress, "analyze", $"正在分析 {entries.Count} 个条目...");

        var preliminary = _cleaner.Classify(entries);
        var (contents, inspectedPassword) = await InspectContentAsync(fullArchivePath, preliminary, password, progress, cancellationToken)
            .ConfigureAwait(false);
        if (inspectedPassword is not null)
            password = inspectedPassword;
        var verdicts = _cleaner.Classify(entries, contents);

        foreach (var item in verdicts.Where(v => !v.ShouldExtract(_cleaner.Config.KeepSuspicious)))
            _log.Info($"Exclude: {item.Entry.Path} [{item.MatchedRule}] {item.Classification} {item.Reason}");
        foreach (var item in verdicts.Where(v =>
                     v.Classification == Classification.Suspicious && v.ShouldExtract(_cleaner.Config.KeepSuspicious)))
            _log.Info($"Keep suspicious: {item.Entry.Path} [{item.MatchedRule}] {item.Reason}");

        return new ExtractPlan
        {
            ArchivePath = fullArchivePath,
            Entries = entries,
            Contents = contents,
            Verdicts = verdicts,
            Password = password,
        };
    }

    public ExtractPlan Recategorize(ExtractPlan plan)
        => plan.WithVerdicts(_cleaner.Classify(plan.Entries, plan.Contents));

    public async Task<CleanExtractResult> ExtractAsync(
        ExtractPlan plan,
        string? outputDirectory = null,
        bool uniquifyOutput = true,
        IProgress<WorkflowProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var password = plan.Password;
        var verdicts = plan.Verdicts;
        var excluded = plan.ExcludedPaths(_cleaner.Config.KeepSuspicious);

        if (NeedsPasswordForExtract(verdicts, password, _cleaner.Config.KeepSuspicious))
        {
            password = await RequestPasswordAsync(plan.ArchivePath, previousWasWrong: false, cancellationToken)
                .ConfigureAwait(false);
        }

        var destination = OutputDirectoryResolver.Resolve(plan.ArchivePath, outputDirectory, uniquifyOutput);
        OutputDirectoryResolver.EnsureWritable(destination);
        _log.Info($"Output: {destination}");

        Report(progress, "extract", "正在解压...");
        var extractProgress = new Progress<double>(p => Report(progress, "extract", $"正在解压... {p:0}%", p));
        ExtractResult extract;
        try
        {
            extract = await _backend.ExtractAsync(
                plan.ArchivePath,
                destination,
                excluded,
                password,
                extractProgress,
                cancellationToken).ConfigureAwait(false);
        }
        catch (WrongPasswordException)
        {
            password = await RequestPasswordAsync(plan.ArchivePath, previousWasWrong: true, cancellationToken)
                .ConfigureAwait(false);
            extract = await _backend.ExtractAsync(
                plan.ArchivePath,
                destination,
                excluded,
                password,
                extractProgress,
                cancellationToken).ConfigureAwait(false);
        }

        var summary = new FilterSummary
        {
            ArchivePath = plan.ArchivePath,
            OutputDirectory = destination,
            Verdicts = verdicts,
            ArchiveBytes = new FileInfo(plan.ArchivePath).Length,
        };

        _log.Info($"Extracted to {destination}. Filtered {summary.TrashCount} trash, kept {summary.SuspiciousCount} suspicious.");
        Report(progress, "done", "解压完成", 100);

        return new CleanExtractResult
        {
            Summary = summary,
            OutputDirectory = destination,
            Warning = extract.Warning,
        };
    }

    private async Task<(Dictionary<string, EntryContent> Contents, string? Password)> InspectContentAsync(
        string archivePath,
        IReadOnlyList<CleanVerdict> preliminary,
        string? password,
        IProgress<WorkflowProgress>? progress,
        CancellationToken cancellationToken)
    {
        var targets = preliminary.Where(v => _cleaner.NeedsContent(v.Entry, v)).ToList();
        var contents = new Dictionary<string, EntryContent>(StringComparer.OrdinalIgnoreCase);
        if (targets.Count == 0)
            return (contents, password);

        if (password is null && targets.Any(v => v.Entry.IsEncrypted))
        {
            try
            {
                password = await RequestPasswordAsync(archivePath, previousWasWrong: false, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (PasswordRequiredException)
            {
                _log.Warn("Skipping encrypted content inspection; password was not provided.");
                targets = targets.Where(v => !v.Entry.IsEncrypted).ToList();
            }
        }

        Report(progress, "inspect", $"正在检查 {targets.Count} 个可疑文件...");
        foreach (var verdict in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var bytes = await _backend.ReadEntryAsync(archivePath, verdict.Entry, password, cancellationToken)
                    .ConfigureAwait(false);
                if (bytes.Length > _cleaner.Config.MaxInspectBytes)
                    bytes = bytes.AsSpan(0, _cleaner.Config.MaxInspectBytes).ToArray();
                contents[verdict.Entry.Path] = EntryContent.FromBytes(bytes);
                _log.Debug($"Inspected {verdict.Entry.Path} ({bytes.Length} bytes).");
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not PasswordRequiredException)
            {
                _log.Warn($"Could not inspect {verdict.Entry.Path}: {ex.Message}");
            }
        }

        return (contents, password);
    }

    private async Task<(IReadOnlyList<ArchiveEntry> Entries, string? Password)> ListWithPasswordAsync(
        string archivePath,
        IProgress<WorkflowProgress>? progress,
        CancellationToken cancellationToken)
    {
        string? password = null;
        var attempted = false;
        while (true)
        {
            try
            {
                var entries = await _backend.ListEntriesAsync(archivePath, password, cancellationToken).ConfigureAwait(false);
                return (entries, password);
            }
            catch (PasswordRequiredException)
            {
                Report(progress, "password", attempted ? "密码不正确，请重试..." : "这个压缩包需要密码...");
                password = await RequestPasswordAsync(archivePath, previousWasWrong: attempted, cancellationToken)
                    .ConfigureAwait(false);
                attempted = true;
            }
        }
    }

    private async Task<string> RequestPasswordAsync(string archivePath, bool previousWasWrong, CancellationToken cancellationToken)
    {
        var password = await _passwordPrompt.RequestPasswordAsync(archivePath, previousWasWrong, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(password))
            throw new PasswordRequiredException("A password is required to open this archive.");
        return password;
    }

    private static bool NeedsPasswordForExtract(
        IReadOnlyList<CleanVerdict> verdicts,
        string? password,
        bool keepSuspicious)
    {
        if (password is not null)
            return false;
        return verdicts.Any(v => v.ShouldExtract(keepSuspicious) && v.Entry.IsEncrypted);
    }

    private static void Report(IProgress<WorkflowProgress>? progress, string stage, string message, double? percent = null)
    {
        progress?.Report(new WorkflowProgress
        {
            Stage = stage,
            Message = message,
            Percent = percent,
        });
    }
}
