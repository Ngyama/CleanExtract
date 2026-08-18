using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Cleaning.Rules;

public sealed class UrlShortcutRule : ICleanRule
{
    public string Id => "ad.url";

    public IEnumerable<RuleContribution> Evaluate(CleanContext context)
    {
        if (!context.Config.EnableUrlInspection)
            yield break;
        if (!string.Equals(context.Extension, ".url", StringComparison.OrdinalIgnoreCase))
            yield break;
        if (context.Entry.IsDirectory)
            yield break;

        yield return new RuleContribution
        {
            RuleId = "ad.url.extension",
            Suggested = Classification.Suspicious,
            Reason = "Internet shortcut (.url) is a common ad vehicle",
            Score = 12,
        };

        if (IsVagueAdName(context.NormalizedFileStem))
        {
            yield return new RuleContribution
            {
                RuleId = "ad.url.vague-name",
                Suggested = Classification.Suspicious,
                Reason = "Shortcut name is a typical ad label, but not unique enough to discard automatically",
                Score = 28,
            };
        }

        if (IsTrustedName(context.NormalizedFileStem, context.Config.TrustedUrlNames))
        {
            yield return new RuleContribution
            {
                RuleId = "ad.url.trusted-name",
                Suggested = Classification.Clean,
                Reason = "Shortcut name looks like official documentation or website",
                Score = -40,
            };
        }

        if (context.Content is null)
            yield break;

        var target = InternetShortcutParser.TryGetUrl(context.Content.Text);
        if (string.IsNullOrWhiteSpace(target))
            yield break;

        var host = UrlExtractor.TryGetHost(target);
        if (host is null)
            yield break;

        if (DomainMatcher.IsListed(host, context.Config.TrustedDomains))
        {
            yield return new RuleContribution
            {
                RuleId = "domain.trusted",
                Suggested = Classification.Clean,
                Reason = $"URL points to trusted domain {host}",
                Score = -50,
            };
            yield break;
        }

        if (DomainMatcher.IsListed(host, context.Config.BlockedDomains))
        {
            yield return new RuleContribution
            {
                RuleId = "domain.blocked",
                Suggested = Classification.Trash,
                Reason = $"URL points to blocked domain {host}",
                Score = 90,
                HardDecision = true,
            };
        }
    }

    private static bool IsTrustedName(string normalized, IEnumerable<string> names)
    {
        return names.Any(name =>
            NameNormalizer.EqualsPhrase(normalized, name)
            || NameNormalizer.ContainsPhrase(normalized, name));
    }

    private static bool IsVagueAdName(string normalized)
    {
        return NameNormalizer.EqualsPhrase(normalized, "说明")
               || NameNormalizer.EqualsPhrase(normalized, "网址")
               || NameNormalizer.EqualsPhrase(normalized, "地址")
               || NameNormalizer.EqualsPhrase(normalized, "发布页")
               || NameNormalizer.EqualsPhrase(normalized, "下载");
    }
}

public static class DomainMatcher
{
    public static bool IsListed(string host, IEnumerable<string> domains)
    {
        foreach (var domain in domains)
        {
            var normalized = domain.Trim().Trim('.').ToLowerInvariant();
            if (normalized.Length == 0)
                continue;
            if (host.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return true;
            if (host.EndsWith("." + normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
