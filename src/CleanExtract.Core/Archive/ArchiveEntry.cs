namespace CleanExtract.Core.Archive;

public sealed class ArchiveEntry
{
    public required string Path { get; init; }

    public string FileName
    {
        get
        {
            var normalized = Path.Replace('\\', '/').TrimEnd('/');
            return System.IO.Path.GetFileName(normalized);
        }
    }

    public string Extension
    {
        get
        {
            var name = FileName;
            var dot = name.LastIndexOf('.');
            return dot > 0 ? name[dot..].ToLowerInvariant() : string.Empty;
        }
    }

    public long Size { get; init; }

    public long PackedSize { get; init; }

    public bool IsDirectory { get; init; }

    public bool IsEncrypted { get; init; }

    public DateTimeOffset? Modified { get; init; }

    public IReadOnlyList<string> PathSegments
    {
        get
        {
            return Path
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public bool IsAtArchiveRoot => PathSegments.Count <= 1;

    public string DirectoryPath
    {
        get
        {
            var normalized = Path.Replace('\\', '/').TrimEnd('/');
            var slash = normalized.LastIndexOf('/');
            return slash < 0 ? string.Empty : normalized[..slash];
        }
    }
}
