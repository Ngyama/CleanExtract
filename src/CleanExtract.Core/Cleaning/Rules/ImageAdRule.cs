using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Cleaning.Rules;

public sealed class ImageAdRule : ICleanRule
{
    public string Id => "ad.image";

    public IEnumerable<RuleContribution> Evaluate(CleanContext context)
    {
        if (!context.Config.EnableImageAdDetection || context.Entry.IsDirectory)
            yield break;
        if (!IsImage(context.Extension, context.Config.ImageExtensions))
            yield break;

        var stem = context.NormalizedFileStem;
        if (stem.Length == 0)
            yield break;

        if (IsProtected(stem, context.Config.ImageProtectedNames))
            yield break;

        var high = FindPhrase(stem, context.Config.ImageAdPhrasesHigh);
        if (high is not null)
        {
            var score = 86;
            if (context.IsArchiveRoot)
                score += 6;
            yield return new RuleContribution
            {
                RuleId = "ad.image.high",
                Suggested = Classification.Trash,
                Reason = $"图片文件名像推广素材（含“{high}”）",
                Score = Math.Clamp(score, 0, 100),
            };
            yield break;
        }

        var medium = FindPhrase(stem, context.Config.ImageAdPhrasesMedium);
        if (medium is not null)
        {
            var score = 44;
            if (context.IsArchiveRoot)
                score += 10;
            yield return new RuleContribution
            {
                RuleId = "ad.image.medium",
                Suggested = Classification.Suspicious,
                Reason = $"图片文件名包含推广特征“{medium}”",
                Score = Math.Clamp(score, 0, 100),
            };
        }
    }

    public static bool IsImage(string extension, IEnumerable<string> extensions)
        => extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

    private static string? FindPhrase(string normalized, IEnumerable<string> phrases)
    {
        foreach (var phrase in phrases.OrderByDescending(p => p.Length))
        {
            if (NameNormalizer.ContainsPhrase(normalized, phrase))
                return phrase;
        }

        return null;
    }

    private static bool IsProtected(string normalized, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var needle = NameNormalizer.NormalizeStem(name);
            if (needle.Length == 0)
                continue;
            if (normalized.Equals(needle, StringComparison.Ordinal))
                return true;
            if (normalized.StartsWith(needle, StringComparison.Ordinal)
                && normalized.Length > needle.Length
                && normalized[needle.Length..].All(static ch => char.IsDigit(ch)))
                return true;
        }

        return false;
    }
}
