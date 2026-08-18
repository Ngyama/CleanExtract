using System.Text.RegularExpressions;
using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Cleaning.Rules;

public sealed class TextContentRule : ICleanRule
{
    private static readonly Regex BareUrlLine = new(@"^\s*https?://\S+\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    public string Id => "ad.text";

    public IEnumerable<RuleContribution> Evaluate(CleanContext context)
    {
        if (!context.Config.EnableTextInspection)
            yield break;
        if (context.Content is null)
            yield break;
        if (context.Entry.IsDirectory)
            yield break;

        var ext = context.Extension;
        if (ext is not (".txt" or ".html" or ".htm" or ".url" or ".md"))
            yield break;

        var text = context.Content.Text;
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var urls = UrlExtractor.Extract(text);
        var promoHits = context.Config.PromoContentPhrases
            .Where(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var readme = context.Config.ReadmeHints.Any(hint =>
            NameNormalizer.ContainsPhrase(context.NormalizedFileStem, hint));

        if (promoHits.Count > 0)
        {
            var score = Math.Min(18 + promoHits.Count * 10, 48);
            if (readme)
                score -= 14;
            yield return new RuleContribution
            {
                RuleId = "ad.text.promo",
                Suggested = Classification.Suspicious,
                Reason = $"文本包含推广用语（{string.Join("、", promoHits.Take(3))}）",
                Score = Math.Max(score, 8),
            };
        }

        if (urls.Count >= 3)
        {
            yield return new RuleContribution
            {
                RuleId = "ad.text.many-urls",
                Suggested = Classification.Suspicious,
                Reason = $"小文件中包含 {urls.Count} 个 URL",
                Score = readme ? 12 : 22,
            };
        }

        if (LooksLikeUrlOnlyFlyer(text, urls))
        {
            var score = readme ? 28 : 45;
            if (context.Extension == ".url")
                score += 8;
            yield return new RuleContribution
            {
                RuleId = "ad.text.url-only",
                Suggested = score >= 50 ? Classification.Suspicious : Classification.Suspicious,
                Reason = "内容几乎只有网址，缺少正常说明文字",
                Score = score,
            };
        }
    }

    private static bool LooksLikeUrlOnlyFlyer(string text, IReadOnlyList<string> urls)
    {
        if (urls.Count == 0)
            return false;

        var compact = Regex.Replace(text, @"\s+", "");
        if (compact.Length == 0)
            return false;

        var urlChars = urls.Sum(u => u.Length);
        if (urlChars >= compact.Length * 0.7 && compact.Length < 400)
            return true;

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        if (lines.Count is > 0 and <= 6 && lines.Count(l => BareUrlLine.IsMatch(l)) >= Math.Max(1, lines.Count - 1))
            return true;

        return false;
    }
}
