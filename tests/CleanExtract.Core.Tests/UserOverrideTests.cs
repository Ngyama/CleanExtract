using CleanExtract.Core.Cleaning;
using CleanExtract.Core.Config;
using CleanExtract.Core.Logging;

namespace CleanExtract.Core.Tests;

public sealed class UserOverrideTests
{
    [Fact]
    public void AlwaysKeep_OverridesSystemJunk()
    {
        var config = new RuleConfig();
        ConfigStore.AddAlwaysKeep(config, ".DS_Store");
        var engine = new CleanerEngine(config);
        var verdict = engine.Classify(Entries.File(".DS_Store", 6));
        Assert.Equal(Classification.Clean, verdict.Classification);
        Assert.Equal("user.always-keep", verdict.MatchedRule);
    }

    [Fact]
    public void AlwaysFilter_OverridesReadme()
    {
        var config = new RuleConfig();
        ConfigStore.AddAlwaysFilter(config, "README.txt");
        var engine = new CleanerEngine(config);
        var verdict = engine.Classify(Entries.File("Game/README.txt", 400));
        Assert.Equal(Classification.Trash, verdict.Classification);
        Assert.Equal("user.always-filter", verdict.MatchedRule);
    }

    [Fact]
    public void AlwaysKeep_WinsOverAlwaysFilter()
    {
        var config = new RuleConfig();
        ConfigStore.AddAlwaysFilter(config, "notes.txt");
        ConfigStore.AddAlwaysKeep(config, "notes.txt");
        Assert.Contains("notes.txt", config.AlwaysKeepNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes.txt", config.AlwaysFilterNames, StringComparer.OrdinalIgnoreCase);
        var engine = new CleanerEngine(config);
        Assert.Equal(Classification.Clean, engine.Classify(Entries.File("notes.txt")).Classification);
    }

    [Fact]
    public void CustomHighPhrase_CanMarkTrash()
    {
        var config = new RuleConfig();
        config.AdPhrasesHigh.Add("内部测试广告词XYZ");
        var engine = new CleanerEngine(config);
        var verdict = engine.Classify(Entries.File("内部测试广告词XYZ.url", 40));
        Assert.Equal(Classification.Trash, verdict.Classification);
    }

    [Fact]
    public void ShortenerDomain_DoesNotTrashTrustedDocumentationUrl()
    {
        var content = EntryContent.FromBytes("""
            [InternetShortcut]
            URL=https://github.com/example/project
            """u8.ToArray());
        var engine = new CleanerEngine();
        var verdict = engine.Classify(Entries.File("Documentation.url", 80), content);
        Assert.Equal(Classification.Clean, verdict.Classification);
    }

    [Fact]
    public void ShortenerUrl_IsNotTrashByItself()
    {
        var content = EntryContent.FromBytes("""
            [InternetShortcut]
            URL=https://bit.ly/abc
            """u8.ToArray());
        var engine = new CleanerEngine();
        var verdict = engine.Classify(Entries.File("Official Website.url", 60), content);
        Assert.NotEqual(Classification.Trash, verdict.Classification);
    }

    [Fact]
    public void DirectoryFileName_IgnoresTrailingSlash()
    {
        var entry = Entries.File("__MACOSX/", 0);
        Assert.Equal("__MACOSX", entry.FileName);
    }
}

public sealed class ConfigStoreTests
{
    [Fact]
    public void SaveAndLoad_UserOverrides()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cleanextract-cfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var rules = new RuleConfig();
            ConfigStore.AddAlwaysKeep(rules, "keep-me.txt");
            ConfigStore.AddAlwaysFilter(rules, "drop-me.url");
            ConfigStore.SaveUserOverrides(dir, rules);

            var loaded = ConfigStore.Load(appDirectory: dir, userDataDirectory: dir, NullLog.Instance);
            Assert.Contains("keep-me.txt", loaded.Rules.AlwaysKeepNames);
            Assert.Contains("drop-me.url", loaded.Rules.AlwaysFilterNames);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
