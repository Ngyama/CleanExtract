using CleanExtract.Core;
using CleanExtract.Core.Cleaning;
using CleanExtract.Core.Config;
using CleanExtract.Core.Logging;

namespace CleanExtract;

public sealed class AppState
{
    public AppState(RuleConfig rules, AppSettings settings, CleanerEngine cleaner, IAppLog log)
    {
        Rules = rules;
        Settings = settings;
        Cleaner = cleaner;
        Log = log;
    }

    public RuleConfig Rules { get; }

    public AppSettings Settings { get; }

    public CleanerEngine Cleaner { get; }

    public IAppLog Log { get; }

    public void SaveAll()
    {
        ConfigStore.SaveAllUserConfig(AppPaths.UserDataDirectory, Rules, Settings);
        Log.Info("Saved user settings, rules, domains, and overrides.");
    }

    public void SaveOverrides()
    {
        ConfigStore.SaveUserOverrides(AppPaths.UserDataDirectory, Rules);
        Log.Info("Saved filename overrides.");
    }

    public void AlwaysKeep(string fileName)
    {
        ConfigStore.AddAlwaysKeep(Rules, fileName);
        SaveOverrides();
    }

    public void AlwaysFilter(string fileName)
    {
        ConfigStore.AddAlwaysFilter(Rules, fileName);
        SaveOverrides();
    }

    public LoadedConfig RestoreRuleDefaults()
    {
        ConfigStore.RestoreDefaultRules(AppPaths.UserDataDirectory);
        var loaded = ConfigStore.Load(AppContext.BaseDirectory, AppPaths.UserDataDirectory, Log);
        CopyRules(loaded.Rules, Rules);
        CopySettings(loaded.Settings, Settings);
        Settings.ApplyTo(Rules);
        SaveOverrides();
        Log.Info("Restored default rules. Filename overrides were kept.");
        return loaded;
    }

    private static void CopySettings(AppSettings source, AppSettings target)
    {
        target.FilterMacosMetadata = source.FilterMacosMetadata;
        target.FilterThumbsDb = source.FilterThumbsDb;
        target.FilterDesktopIni = source.FilterDesktopIni;
        target.KeepSuspicious = source.KeepSuspicious;
        target.EnableAdFilenameDetection = source.EnableAdFilenameDetection;
        target.EnableUrlInspection = source.EnableUrlInspection;
        target.EnableTextInspection = source.EnableTextInspection;
        target.EnableImageAdDetection = source.EnableImageAdDetection;
        target.CheckForUpdates = source.CheckForUpdates;
        target.UpdateFeedUrl = source.UpdateFeedUrl;
    }

    private static void CopyRules(RuleConfig source, RuleConfig target)
    {
        target.TrashThreshold = source.TrashThreshold;
        target.SuspiciousThreshold = source.SuspiciousThreshold;
        target.MaxInspectBytes = source.MaxInspectBytes;
        target.FilterMacosMetadata = source.FilterMacosMetadata;
        target.FilterThumbsDb = source.FilterThumbsDb;
        target.FilterDesktopIni = source.FilterDesktopIni;
        target.KeepSuspicious = source.KeepSuspicious;
        target.EnableAdFilenameDetection = source.EnableAdFilenameDetection;
        target.EnableUrlInspection = source.EnableUrlInspection;
        target.EnableTextInspection = source.EnableTextInspection;
        target.EnableImageAdDetection = source.EnableImageAdDetection;
        Replace(target.AdPhrasesHigh, source.AdPhrasesHigh);
        Replace(target.AdPhrasesMedium, source.AdPhrasesMedium);
        Replace(target.AdPhrasesLow, source.AdPhrasesLow);
        Replace(target.PromoContentPhrases, source.PromoContentPhrases);
        Replace(target.TrustedUrlNames, source.TrustedUrlNames);
        Replace(target.ReadmeHints, source.ReadmeHints);
        Replace(target.InspectExtensions, source.InspectExtensions);
        Replace(target.ImageExtensions, source.ImageExtensions);
        Replace(target.ImageAdPhrasesHigh, source.ImageAdPhrasesHigh);
        Replace(target.ImageAdPhrasesMedium, source.ImageAdPhrasesMedium);
        Replace(target.ImageProtectedNames, source.ImageProtectedNames);
        Replace(target.BlockedDomains, source.BlockedDomains);
        Replace(target.TrustedDomains, source.TrustedDomains);
        Replace(target.SuspiciousDomains, source.SuspiciousDomains);
        Replace(target.AlwaysKeepNames, source.AlwaysKeepNames);
        Replace(target.AlwaysFilterNames, source.AlwaysFilterNames);
    }

    private static void Replace(List<string> target, List<string> source)
    {
        target.Clear();
        target.AddRange(source);
    }
}
