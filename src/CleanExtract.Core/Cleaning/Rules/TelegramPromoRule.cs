using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Cleaning.Rules;

public sealed class TelegramPromoRule : ICleanRule
{
    public string Id => "ad.telegram";

    private static readonly string[] Phrases =
    [
        "tg频道",
        "telegram频道",
        "telegramchannel",
        "电报频道",
        "来自tg",
        "来自telegram",
        "来自电报",
        "关注tg",
        "加入tg",
        "telegram群",
        "电报群",
        "tg群",
    ];

    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".dll",
        ".so",
        ".dylib",
        ".sys",
    };

    public IEnumerable<RuleContribution> Evaluate(CleanContext context)
    {
        if (!context.Config.EnableAdFilenameDetection || context.Entry.IsDirectory)
            yield break;
        if (SkipExtensions.Contains(context.Extension))
            yield break;

        var stem = context.NormalizedFileStem;
        if (stem.Length == 0)
            yield break;

        string? hit = null;
        foreach (var phrase in Phrases.OrderByDescending(p => p.Length))
        {
            if (NameNormalizer.ContainsPhrase(stem, phrase))
            {
                hit = phrase;
                break;
            }
        }

        if (hit is null)
            yield break;

        yield return new RuleContribution
        {
            RuleId = "ad.telegram.filename",
            Suggested = Classification.Trash,
            Reason = $"文件名像 Telegram 推广（含“{hit}”）",
            Score = 92,
            HardDecision = true,
        };
    }
}
