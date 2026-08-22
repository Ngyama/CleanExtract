using CleanExtract.Core.Archive;
using CleanExtract.Core.Cleaning.Rules;
using CleanExtract.Core.Config;

namespace CleanExtract.Core.Cleaning;

public sealed class CleanerEngine
{
    private readonly RuleConfig _config;
    private readonly IReadOnlyList<ICleanRule> _rules;

    public CleanerEngine(RuleConfig? config = null, IEnumerable<ICleanRule>? extraRules = null)
    {
        _config = config ?? new RuleConfig();
        var rules = new List<ICleanRule>
        {
            new UserOverrideRule(),
            new SystemJunkRule(),
            new AdFilenameRule(),
            new TelegramPromoRule(),
            new ImageAdRule(),
            new UrlShortcutRule(),
            new TextContentRule(),
            new DomainRule(),
            new PathHeuristicRule(),
        };
        if (extraRules is not null)
            rules.AddRange(extraRules);
        _rules = rules;
    }

    public RuleConfig Config => _config;

    public IReadOnlyList<CleanVerdict> Classify(
        IReadOnlyList<ArchiveEntry> entries,
        IReadOnlyDictionary<string, EntryContent>? contents = null)
    {
        return entries.Select(entry =>
        {
            EntryContent? content = null;
            contents?.TryGetValue(entry.Path, out content);
            return Classify(entry, content);
        }).ToList();
    }

    public CleanVerdict Classify(ArchiveEntry entry, EntryContent? content = null)
    {
        var context = new CleanContext
        {
            Entry = entry,
            Config = _config,
            Content = content,
            NormalizedFileStem = NameNormalizer.NormalizeStem(entry.FileName),
        };

        var contributions = new List<RuleContribution>();
        foreach (var rule in _rules)
        {
            foreach (var contribution in rule.Evaluate(context))
                contributions.Add(contribution);
        }

        return Summarize(entry, contributions);
    }

    public bool NeedsContent(ArchiveEntry entry, CleanVerdict? preliminary = null)
    {
        if (entry.IsDirectory)
            return false;
        if (entry.Size <= 0 || entry.Size > _config.MaxInspectBytes)
            return false;

        var ext = entry.Extension;
        if (!_config.InspectExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return false;

        if (preliminary?.Classification == Classification.Trash
            && preliminary.Contributions.Any(c => c.HardDecision))
            return false;

        if (ext == ".url" && _config.EnableUrlInspection)
            return true;

        if (!_config.EnableTextInspection)
            return false;

        if (entry.IsAtArchiveRoot)
            return true;

        if (entry.Size <= 2048)
            return true;

        var stem = NameNormalizer.NormalizeStem(entry.FileName);
        var phrases = _config.AdPhrasesHigh
            .Concat(_config.AdPhrasesMedium)
            .Concat(_config.AdPhrasesLow);
        return phrases.Any(phrase => NameNormalizer.ContainsPhrase(stem, phrase));
    }

    private CleanVerdict Summarize(ArchiveEntry entry, List<RuleContribution> contributions)
    {
        var score = Math.Clamp(contributions.Sum(c => c.Score), 0, 100);
        var hardKeep = contributions.FirstOrDefault(c => c.HardDecision && c.Suggested == Classification.Clean);
        var hardTrash = contributions.FirstOrDefault(c => c.HardDecision && c.Suggested == Classification.Trash);

        Classification classification;
        if (hardKeep is not null)
            classification = Classification.Clean;
        else if (hardTrash is not null || score >= _config.TrashThreshold)
            classification = Classification.Trash;
        else if (score >= _config.SuspiciousThreshold)
            classification = Classification.Suspicious;
        else
            classification = Classification.Clean;

        var primary = hardKeep
                      ?? hardTrash
                      ?? contributions
                          .OrderByDescending(c => c.Score)
                          .ThenByDescending(c => (int)c.Suggested)
                          .FirstOrDefault();

        var downgradedFromTrash = false;
        if (classification == Classification.Trash
            && hardKeep is null
            && hardTrash is null
            && LooksLikeProtectedDocument(entry))
        {
            classification = Classification.Suspicious;
            downgradedFromTrash = true;
        }

        var reason = classification switch
        {
            Classification.Clean when contributions.Count == 0 => "未命中垃圾规则",
            Classification.Clean => primary?.Reason ?? "未达到可疑阈值，按正常文件保留",
            _ when downgradedFromTrash => "文件名像说明文档，广告特征不足以自动丢弃",
            _ => primary?.Reason ?? "命中清理规则",
        };

        return new CleanVerdict
        {
            Entry = entry,
            Classification = classification,
            Score = score,
            Reason = reason,
            MatchedRule = primary?.RuleId,
            Contributions = contributions,
        };
    }

    private bool LooksLikeProtectedDocument(ArchiveEntry entry)
    {
        var stem = NameNormalizer.NormalizeStem(entry.FileName);
        if (stem.Length == 0)
            return false;
        return _config.ReadmeHints.Any(hint => NameNormalizer.ContainsPhrase(stem, hint));
    }
}
