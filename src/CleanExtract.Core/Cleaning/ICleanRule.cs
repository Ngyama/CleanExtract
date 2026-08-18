namespace CleanExtract.Core.Cleaning;

public interface ICleanRule
{
    string Id { get; }

    IEnumerable<RuleContribution> Evaluate(CleanContext context);
}
