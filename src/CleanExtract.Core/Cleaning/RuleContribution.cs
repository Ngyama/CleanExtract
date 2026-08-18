namespace CleanExtract.Core.Cleaning;

public sealed class RuleContribution
{
    public required string RuleId { get; init; }

    public required Classification Suggested { get; init; }

    public required string Reason { get; init; }

    public required int Score { get; init; }

    public bool HardDecision { get; init; }
}
