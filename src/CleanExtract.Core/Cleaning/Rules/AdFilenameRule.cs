using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Cleaning.Rules;

public sealed class AdFilenameRule : ICleanRule
{
    public string Id => "ad.filename";

    public IEnumerable<RuleContribution> Evaluate(CleanContext context)
    {
        if (!context.Config.EnableAdFilenameDetection || context.Entry.IsDirectory)
            yield break;

        var stem = context.NormalizedFileStem;
        if (stem.Length == 0)
            yield break;

        var ext = context.Extension;
        var high = FindPhrase(stem, context.Config.AdPhrasesHigh);
        var medium = high is null ? FindPhrase(stem, context.Config.AdPhrasesMedium) : null;
        var low = high is null && medium is null ? FindPhrase(stem, context.Config.AdPhrasesLow) : null;
        var readme = HasReadmeHint(stem, context.Config.ReadmeHints);

        if (high is not null)
        {
            var score = 78;
            var suggested = Classification.Trash;
            var reason = $"文件名包含典型资源站“{high}”模式";

            if (ext is ".url")
                score += 10;
            else if (ext is ".html" or ".htm")
                score += 4;
            else if (ext is ".txt")
                score -= 8;

            if (readme)
            {
                score -= 30;
                suggested = Classification.Suspicious;
                reason += "，但文件名也像说明文档，降低置信度";
            }

            if (context.IsArchiveRoot)
                score += 4;

            yield return Contribute("ad.filename.high", suggested, reason, score);
            yield break;
        }

        if (medium is not null)
        {
            var score = 46;
            var reason = $"文件名包含推广特征“{medium}”";
            if (ext is ".url")
                score += 12;
            if (readme)
            {
                score -= 18;
                reason += "，但更像说明文件";
            }

            if (context.IsArchiveRoot)
                score += 4;

            yield return Contribute("ad.filename.medium", Classification.Suspicious, reason, score);
            yield break;
        }

        if (low is not null)
        {
            var isInstructionName = NameNormalizer.EqualsPhrase(stem, "使用说明")
                                    || NameNormalizer.EqualsPhrase(stem, "下载说明");
            var score = isInstructionName ? 40 : 22;
            var reason = isInstructionName
                ? "文件名像下载说明，存在广告嫌疑但可能是真正的说明文档"
                : $"文件名包含弱广告特征“{low}”";
            if (!isInstructionName && (readme || NameNormalizer.ContainsPhrase(stem, "说明")))
            {
                score -= 10;
                reason = $"文件名包含“{low}”，但更像说明/README";
            }

            if (ext is ".url")
                score += 10;

            yield return Contribute("ad.filename.low", Classification.Suspicious, reason, Math.Max(score, 12));
        }
    }

    private static string? FindPhrase(string normalized, IEnumerable<string> phrases)
    {
        foreach (var phrase in phrases.OrderByDescending(p => p.Length))
        {
            if (NameNormalizer.ContainsPhrase(normalized, phrase))
                return phrase;
        }

        return null;
    }

    private static bool HasReadmeHint(string normalized, IEnumerable<string> hints)
    {
        return hints.Any(hint => NameNormalizer.ContainsPhrase(normalized, hint));
    }

    private static RuleContribution Contribute(string id, Classification suggested, string reason, int score)
    {
        return new RuleContribution
        {
            RuleId = id,
            Suggested = suggested,
            Reason = reason,
            Score = Math.Clamp(score, 0, 100),
        };
    }
}
