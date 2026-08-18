using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Cleaning.Rules;

public sealed class PathHeuristicRule : ICleanRule
{
    public string Id => "path.heuristic";

    public IEnumerable<RuleContribution> Evaluate(CleanContext context)
    {
        if (context.Entry.IsDirectory)
            yield break;
        if (!context.IsArchiveRoot)
            yield break;

        var ext = context.Extension;
        var small = context.Entry.Size > 0 && context.Entry.Size <= 8 * 1024;
        if (!small)
            yield break;

        if (ext is ".url" or ".txt" or ".html" or ".htm")
        {
            yield return new RuleContribution
            {
                RuleId = "path.root-small",
                Suggested = Classification.Suspicious,
                Reason = "位于压缩包根目录的小型链接/文本文件",
                Score = 8,
            };
        }
    }
}
