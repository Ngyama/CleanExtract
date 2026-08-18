using System.Text.Json;
using System.Text.Json.Serialization;
using CleanExtract.Core.Logging;

namespace CleanExtract.Core.Config;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static LoadedConfig Load(string appDirectory, string userDataDirectory, IAppLog log)
    {
        var rules = new RuleConfig();
        var settings = new AppSettings();

        MergeRulesFile(Path.Combine(appDirectory, "config", "rules.json"), rules, log, replaceLists: false);
        MergeDomainsFile(Path.Combine(appDirectory, "config", "domains.json"), rules, log, replaceLists: false);
        MergeSettingsFile(Path.Combine(appDirectory, "config", "settings.json"), settings, log);

        Directory.CreateDirectory(userDataDirectory);
        MergeRulesFile(Path.Combine(userDataDirectory, "rules.json"), rules, log, replaceLists: true);
        MergeDomainsFile(Path.Combine(userDataDirectory, "domains.json"), rules, log, replaceLists: true);
        MergeSettingsFile(Path.Combine(userDataDirectory, "settings.json"), settings, log);
        MergeOverridesFile(Path.Combine(userDataDirectory, "overrides.json"), rules, log);

        settings.ApplyTo(rules);
        return new LoadedConfig(rules, settings);
    }

    public static void SaveUserSettings(string userDataDirectory, AppSettings settings)
        => Write(Path.Combine(userDataDirectory, "settings.json"), settings);

    public static void SaveUserRules(string userDataDirectory, RuleConfig rules)
    {
        var payload = new RuleConfig
        {
            TrashThreshold = rules.TrashThreshold,
            SuspiciousThreshold = rules.SuspiciousThreshold,
            MaxInspectBytes = rules.MaxInspectBytes,
            FilterMacosMetadata = rules.FilterMacosMetadata,
            FilterThumbsDb = rules.FilterThumbsDb,
            FilterDesktopIni = rules.FilterDesktopIni,
            KeepSuspicious = rules.KeepSuspicious,
            EnableAdFilenameDetection = rules.EnableAdFilenameDetection,
            EnableUrlInspection = rules.EnableUrlInspection,
            EnableTextInspection = rules.EnableTextInspection,
            AdPhrasesHigh = rules.AdPhrasesHigh,
            AdPhrasesMedium = rules.AdPhrasesMedium,
            AdPhrasesLow = rules.AdPhrasesLow,
            PromoContentPhrases = rules.PromoContentPhrases,
            TrustedUrlNames = rules.TrustedUrlNames,
            ReadmeHints = rules.ReadmeHints,
            InspectExtensions = rules.InspectExtensions,
        };
        Write(Path.Combine(userDataDirectory, "rules.json"), payload);
    }

    public static void SaveUserDomains(string userDataDirectory, RuleConfig rules)
    {
        Write(Path.Combine(userDataDirectory, "domains.json"), new DomainLists
        {
            BlockedDomains = rules.BlockedDomains,
            TrustedDomains = rules.TrustedDomains,
            SuspiciousDomains = rules.SuspiciousDomains,
        });
    }

    public static void SaveUserOverrides(string userDataDirectory, RuleConfig rules)
    {
        Write(Path.Combine(userDataDirectory, "overrides.json"), new UserOverrides
        {
            AlwaysKeepNames = rules.AlwaysKeepNames,
            AlwaysFilterNames = rules.AlwaysFilterNames,
        });
    }

    public static void SaveAllUserConfig(string userDataDirectory, RuleConfig rules, AppSettings settings)
    {
        settings.ApplyTo(rules);
        Directory.CreateDirectory(userDataDirectory);
        SaveUserSettings(userDataDirectory, settings);
        SaveUserRules(userDataDirectory, rules);
        SaveUserDomains(userDataDirectory, rules);
        SaveUserOverrides(userDataDirectory, rules);
    }

    public static void RestoreDefaultRules(string userDataDirectory)
    {
        DeleteIfExists(Path.Combine(userDataDirectory, "rules.json"));
        DeleteIfExists(Path.Combine(userDataDirectory, "domains.json"));
        DeleteIfExists(Path.Combine(userDataDirectory, "settings.json"));
    }

    public static void AddAlwaysKeep(RuleConfig rules, string name)
        => MoveName(name, addTo: rules.AlwaysKeepNames, removeFrom: rules.AlwaysFilterNames);

    public static void AddAlwaysFilter(RuleConfig rules, string name)
        => MoveName(name, addTo: rules.AlwaysFilterNames, removeFrom: rules.AlwaysKeepNames);

    private static void MoveName(string name, List<string> addTo, List<string> removeFrom)
    {
        name = name.Trim();
        if (name.Length == 0)
            return;
        removeFrom.RemoveAll(item => item.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (!addTo.Contains(name, StringComparer.OrdinalIgnoreCase))
            addTo.Add(name);
    }

    private static void MergeRulesFile(string path, RuleConfig target, IAppLog log, bool replaceLists)
    {
        var loaded = TryRead<RuleConfig>(path, log);
        if (loaded is null)
            return;

        target.TrashThreshold = loaded.TrashThreshold;
        target.SuspiciousThreshold = loaded.SuspiciousThreshold;
        target.MaxInspectBytes = loaded.MaxInspectBytes;
        target.FilterMacosMetadata = loaded.FilterMacosMetadata;
        target.FilterThumbsDb = loaded.FilterThumbsDb;
        target.FilterDesktopIni = loaded.FilterDesktopIni;
        target.KeepSuspicious = loaded.KeepSuspicious;
        target.EnableAdFilenameDetection = loaded.EnableAdFilenameDetection;
        target.EnableUrlInspection = loaded.EnableUrlInspection;
        target.EnableTextInspection = loaded.EnableTextInspection;
        CopyList(target.AdPhrasesHigh, loaded.AdPhrasesHigh, replaceLists);
        CopyList(target.AdPhrasesMedium, loaded.AdPhrasesMedium, replaceLists);
        CopyList(target.AdPhrasesLow, loaded.AdPhrasesLow, replaceLists);
        CopyList(target.PromoContentPhrases, loaded.PromoContentPhrases, replaceLists);
        CopyList(target.TrustedUrlNames, loaded.TrustedUrlNames, replaceLists);
        CopyList(target.ReadmeHints, loaded.ReadmeHints, replaceLists);
        CopyList(target.InspectExtensions, loaded.InspectExtensions, replaceLists);
        CopyList(target.BlockedDomains, loaded.BlockedDomains, replaceLists);
        CopyList(target.TrustedDomains, loaded.TrustedDomains, replaceLists);
        CopyList(target.SuspiciousDomains, loaded.SuspiciousDomains, replaceLists);
        CopyList(target.AlwaysKeepNames, loaded.AlwaysKeepNames, replaceLists);
        CopyList(target.AlwaysFilterNames, loaded.AlwaysFilterNames, replaceLists);
    }

    private static void MergeDomainsFile(string path, RuleConfig target, IAppLog log, bool replaceLists)
    {
        var loaded = TryRead<DomainLists>(path, log);
        if (loaded is null)
            return;
        CopyList(target.BlockedDomains, loaded.BlockedDomains, replaceLists);
        CopyList(target.TrustedDomains, loaded.TrustedDomains, replaceLists);
        CopyList(target.SuspiciousDomains, loaded.SuspiciousDomains, replaceLists);
    }

    private static void MergeOverridesFile(string path, RuleConfig target, IAppLog log)
    {
        var loaded = TryRead<UserOverrides>(path, log);
        if (loaded is null)
            return;
        CopyList(target.AlwaysKeepNames, loaded.AlwaysKeepNames, replace: true);
        CopyList(target.AlwaysFilterNames, loaded.AlwaysFilterNames, replace: true);
    }

    private static void MergeSettingsFile(string path, AppSettings target, IAppLog log)
    {
        var loaded = TryRead<AppSettings>(path, log);
        if (loaded is null)
            return;
        target.FilterMacosMetadata = loaded.FilterMacosMetadata;
        target.FilterThumbsDb = loaded.FilterThumbsDb;
        target.FilterDesktopIni = loaded.FilterDesktopIni;
        target.KeepSuspicious = loaded.KeepSuspicious;
        target.EnableAdFilenameDetection = loaded.EnableAdFilenameDetection;
        target.EnableUrlInspection = loaded.EnableUrlInspection;
        target.EnableTextInspection = loaded.EnableTextInspection;
    }

    private static T? TryRead<T>(string path, IAppLog log) where T : class
    {
        try
        {
            if (!File.Exists(path))
                return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            log.Warn($"Failed to read config {path}: {ex.Message}");
            return null;
        }
    }

    private static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static void CopyList(List<string> target, List<string>? source, bool replace)
    {
        if (source is null)
            return;
        if (replace)
        {
            target.Clear();
            target.AddRange(source);
            return;
        }

        foreach (var item in source)
        {
            if (!target.Contains(item, StringComparer.OrdinalIgnoreCase))
                target.Add(item);
        }
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort restore.
        }
    }
}

public sealed record LoadedConfig(RuleConfig Rules, AppSettings Settings);
