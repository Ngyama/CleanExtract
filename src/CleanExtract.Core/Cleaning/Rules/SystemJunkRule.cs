using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Cleaning.Rules;

public sealed class SystemJunkRule : ICleanRule
{
    public string Id => "system.junk";

    public IEnumerable<RuleContribution> Evaluate(CleanContext context)
    {
        var name = context.FileName;
        var config = context.Config;

        if (config.FilterMacosMetadata)
        {
            if (name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase))
            {
                yield return Hit("system.macos.dsstore", "macOS Finder metadata", 100);
                yield break;
            }

            if (name.StartsWith("._", StringComparison.Ordinal) && name.Length > 2)
            {
                yield return Hit("system.macos.appledouble", "macOS AppleDouble metadata", 95);
                yield break;
            }

            if (HasSegment(context, "__MACOSX")
                || HasSegment(context, ".Spotlight-V100")
                || HasSegment(context, ".Trashes")
                || HasSegment(context, ".fseventsd"))
            {
                yield return Hit("system.macos.metadata-dir", "macOS metadata directory", 100);
                yield break;
            }
        }

        if (config.FilterThumbsDb && name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
        {
            yield return Hit("system.windows.thumbsdb", "Windows thumbnail cache", 95);
            yield break;
        }

        if (name.Equals("ehthumbs.db", StringComparison.OrdinalIgnoreCase))
        {
            yield return Hit("system.windows.ehthumbs", "Windows media thumbnail cache", 95);
            yield break;
        }

        if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
        {
            if (config.FilterDesktopIni)
            {
                yield return Hit("system.windows.desktop.ini", "Windows folder customization file", 90);
            }
            else
            {
                yield return new RuleContribution
                {
                    RuleId = "system.windows.desktop.ini",
                    Suggested = Classification.Suspicious,
                    Reason = "Windows folder customization file; kept unless filtering is enabled",
                    Score = 42,
                };
            }
        }
    }

    private static bool HasSegment(CleanContext context, string segment)
    {
        return context.PathSegments.Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static RuleContribution Hit(string id, string reason, int score)
    {
        return new RuleContribution
        {
            RuleId = id,
            Suggested = Classification.Trash,
            Reason = reason,
            Score = score,
            HardDecision = true,
        };
    }
}
