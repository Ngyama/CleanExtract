using CleanExtract.Core.Archive;
using CleanExtract.Core.Config;

namespace CleanExtract.Core.Cleaning;

public sealed class CleanContext
{
    public required ArchiveEntry Entry { get; init; }

    public required RuleConfig Config { get; init; }

    public EntryContent? Content { get; init; }

    public string NormalizedFileStem { get; init; } = string.Empty;

    public IReadOnlyList<string> PathSegments => Entry.PathSegments;

    public bool IsArchiveRoot => Entry.IsAtArchiveRoot;

    public string FileName => Entry.FileName;

    public string Extension => Entry.Extension;
}
