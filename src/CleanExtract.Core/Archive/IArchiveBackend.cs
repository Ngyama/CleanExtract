namespace CleanExtract.Core.Archive;

public interface IArchiveBackend
{
    Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(
        string archivePath,
        string? password,
        CancellationToken cancellationToken = default);

    Task<byte[]> ReadEntryAsync(
        string archivePath,
        ArchiveEntry entry,
        string? password,
        CancellationToken cancellationToken = default);

    Task<ExtractResult> ExtractAsync(
        string archivePath,
        string outputDirectory,
        IReadOnlyList<string> excludedPaths,
        string? password,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
