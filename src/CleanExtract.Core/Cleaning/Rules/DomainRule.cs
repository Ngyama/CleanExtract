using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Cleaning.Rules;

public sealed class DomainRule : ICleanRule
{
    public string Id => "domain";

    public IEnumerable<RuleContribution> Evaluate(CleanContext context)
    {
        if (context.Content?.Text is null)
            yield break;

        var urls = UrlExtractor.Extract(context.Content.Text);
        if (context.Extension == ".url")
        {
            var shortcut = InternetShortcutParser.TryGetUrl(context.Content.Text);
            if (!string.IsNullOrWhiteSpace(shortcut))
                urls = urls.Append(shortcut).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        foreach (var url in urls)
        {
            var host = UrlExtractor.TryGetHost(url);
            if (host is null)
                continue;

            if (DomainMatcher.IsListed(host, context.Config.BlockedDomains))
            {
                yield return new RuleContribution
                {
                    RuleId = "domain.blocked",
                    Suggested = Classification.Trash,
                    Reason = $"内容指向已知拦截域名 {host}",
                    Score = 88,
                    HardDecision = true,
                };
                yield break;
            }

            if (DomainMatcher.IsListed(host, context.Config.SuspiciousDomains))
            {
                yield return new RuleContribution
                {
                    RuleId = "domain.shortener",
                    Suggested = Classification.Suspicious,
                    Reason = $"链接使用短链域名 {host}，只作为风险特征，不会单独过滤",
                    Score = 18,
                };
            }
        }
    }
}
