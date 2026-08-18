using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Cleaning.Rules;

public sealed class UserOverrideRule : ICleanRule
{
    public string Id => "user.override";

    public IEnumerable<RuleContribution> Evaluate(CleanContext context)
    {
        if (Matches(context, context.Config.AlwaysKeepNames))
        {
            yield return new RuleContribution
            {
                RuleId = "user.always-keep",
                Suggested = Classification.Clean,
                Reason = "按你的设置始终保留",
                Score = 0,
                HardDecision = true,
            };
            yield break;
        }

        if (Matches(context, context.Config.AlwaysFilterNames))
        {
            yield return new RuleContribution
            {
                RuleId = "user.always-filter",
                Suggested = Classification.Trash,
                Reason = "按你的设置始终过滤",
                Score = 100,
                HardDecision = true,
            };
        }
    }

    public static bool Matches(CleanContext context, IEnumerable<string> names)
        => names.Any(name => MatchesOne(context, name));

    private static bool MatchesOne(CleanContext context, string raw)
    {
        var name = raw.Trim();
        if (name.Length == 0)
            return false;

        var fileName = context.FileName;
        if (fileName.Equals(name, StringComparison.OrdinalIgnoreCase))
            return true;

        var path = context.Entry.Path.Replace('\\', '/').TrimEnd('/');
        var wanted = name.Replace('\\', '/').TrimEnd('/');
        if (path.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            return true;

        if (path.EndsWith('/' + wanted, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
