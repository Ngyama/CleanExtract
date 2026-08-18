using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Workflow;

public sealed class FilterSummary
{
    public required string ArchivePath { get; init; }

    public required string OutputDirectory { get; init; }

    public required IReadOnlyList<CleanVerdict> Verdicts { get; init; }

    public IReadOnlyList<CleanVerdict> Files => Verdicts.Where(v => !v.Entry.IsDirectory).ToList();

    public int TotalEntries => Verdicts.Count;

    public int FileCount => Files.Count;

    public IReadOnlyList<CleanVerdict> Trash => Verdicts.Where(v => v.Classification == Classification.Trash).ToList();

    public IReadOnlyList<CleanVerdict> SuspiciousKept =>
        Verdicts.Where(v => v.Classification == Classification.Suspicious).ToList();

    public int TrashCount => Trash.Count;

    public int SuspiciousCount => SuspiciousKept.Count;

    public int ExtractedCount => Verdicts.Count(v => v.Classification != Classification.Trash);

    public long ArchiveBytes { get; init; }
}

public sealed class WorkflowProgress
{
    public required string Stage { get; init; }

    public required string Message { get; init; }

    public double? Percent { get; init; }
}

public sealed class CleanExtractResult
{
    public required FilterSummary Summary { get; init; }

    public required string OutputDirectory { get; init; }

    public string? Warning { get; init; }
}

public interface IPasswordPrompt
{
    Task<string?> RequestPasswordAsync(string archivePath, bool previousWasWrong, CancellationToken cancellationToken);
}
