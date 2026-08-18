using CleanExtract.Core.Archive;

namespace CleanExtract.Core.Cleaning;

public sealed class CleanVerdict
{
    public required ArchiveEntry Entry { get; init; }

    public required Classification Classification { get; init; }

    public required int Score { get; init; }

    public required string Reason { get; init; }

    public string? MatchedRule { get; init; }

    public IReadOnlyList<RuleContribution> Contributions { get; init; } = [];

    public bool ShouldExtract(bool keepSuspicious)
    {
        return Classification switch
        {
            Classification.Trash => false,
            Classification.Suspicious => keepSuspicious,
            _ => true,
        };
    }
}
