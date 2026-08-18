using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Tests;

public sealed class CleanerEngineTests
{
    private readonly CleanerEngine _engine = new();

    [Fact]
    public void DsStore_IsTrash()
    {
        var verdict = _engine.Classify(Entries.File(".DS_Store", 6));
        Assert.Equal(Classification.Trash, verdict.Classification);
        Assert.Equal("system.macos.dsstore", verdict.MatchedRule);
    }

    [Fact]
    public void MacosMetadataFolder_IsTrash()
    {
        var verdict = _engine.Classify(Entries.File("__MACOSX/._game.exe", 200));
        Assert.Equal(Classification.Trash, verdict.Classification);
        Assert.Contains("macos", verdict.MatchedRule, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThumbsDb_IsTrash()
    {
        var verdict = _engine.Classify(Entries.File("Game/Thumbs.db", 12_000));
        Assert.Equal(Classification.Trash, verdict.Classification);
    }

    [Fact]
    public void TypicalAdUrl_IsTrash()
    {
        var verdict = _engine.Classify(Entries.File("★本站最新网址.url", 80));
        Assert.Equal(Classification.Trash, verdict.Classification);
        Assert.True(verdict.Score >= 75);
    }

    [Fact]
    public void DecoratedBackupUrl_IsTrash()
    {
        var verdict = _engine.Classify(Entries.File("【备用网址】.url", 90));
        Assert.Equal(Classification.Trash, verdict.Classification);
    }

    [Fact]
    public void ReadmeTxt_IsClean()
    {
        var verdict = _engine.Classify(Entries.File("Game/README.txt", 1_200));
        Assert.Equal(Classification.Clean, verdict.Classification);
    }

    [Fact]
    public void ManualPdf_IsClean()
    {
        var verdict = _engine.Classify(Entries.File("manual.pdf", 2_000_000));
        Assert.Equal(Classification.Clean, verdict.Classification);
    }

    [Fact]
    public void GameExe_IsClean()
    {
        var verdict = _engine.Classify(Entries.File("Game/game.exe", 8_000_000));
        Assert.Equal(Classification.Clean, verdict.Classification);
    }

    [Fact]
    public void OfficialDocumentationUrl_IsNotTrash()
    {
        var content = EntryContent.FromBytes("""
            [InternetShortcut]
            URL=https://github.com/example/project
            """u8.ToArray());

        var verdict = _engine.Classify(Entries.File("Documentation.url", 120), content);
        Assert.NotEqual(Classification.Trash, verdict.Classification);
        Assert.Equal(Classification.Clean, verdict.Classification);
    }

    [Fact]
    public void UrlExtensionAlone_IsNotTrash()
    {
        var verdict = _engine.Classify(Entries.File("Official Website.url", 80));
        Assert.NotEqual(Classification.Trash, verdict.Classification);
    }

    [Fact]
    public void UsageNotesTxt_IsNotTrash()
    {
        var verdict = _engine.Classify(Entries.File("使用说明.txt", 400));
        Assert.NotEqual(Classification.Trash, verdict.Classification);
    }

    [Fact]
    public void GameDownloadReadme_IsNotTrash()
    {
        var verdict = _engine.Classify(Entries.File("游戏下载说明.txt", 600));
        Assert.NotEqual(Classification.Trash, verdict.Classification);
    }

    [Fact]
    public void DesktopIni_IsSuspiciousByDefault()
    {
        var verdict = _engine.Classify(Entries.File("desktop.ini", 88));
        Assert.Equal(Classification.Suspicious, verdict.Classification);
    }

    [Theory]
    [InlineData("Game/日本語フォルダ/説明.txt")]
    [InlineData("Game/hello world.exe")]
    [InlineData("Game/emoji😀.bin")]
    [InlineData("Game/special-#@+.pak")]
    [InlineData("deep/nested/path/data.dat")]
    public void NormalUnicodeAndNestedFiles_AreClean(string path)
    {
        var verdict = _engine.Classify(Entries.File(path, 4096));
        Assert.Equal(Classification.Clean, verdict.Classification);
    }

    [Fact]
    public void NeedsContent_ForSmallRootUrl()
    {
        Assert.True(_engine.NeedsContent(Entries.File("ad.url", 200)));
    }

    [Fact]
    public void DoesNotNeedContent_ForLargeTxt()
    {
        Assert.False(_engine.NeedsContent(Entries.File("changelog.txt", 500_000)));
    }

    [Fact]
    public void BlockedDomainUrl_IsTrash()
    {
        var config = new Config.RuleConfig
        {
            BlockedDomains = ["ads.example.test"],
        };
        var engine = new CleanerEngine(config);
        var content = EntryContent.FromBytes("""
            [InternetShortcut]
            URL=https://ads.example.test/landing
            """u8.ToArray());

        var verdict = engine.Classify(Entries.File("link.url", 90), content);
        Assert.Equal(Classification.Trash, verdict.Classification);
        Assert.Equal("domain.blocked", verdict.MatchedRule);
    }

    [Fact]
    public void PromoOnlyTinyText_IsSuspiciousNotTrash()
    {
        var content = EntryContent.FromBytes("请收藏 最新网址\r\nhttps://example.invalid/page\r\n"u8.ToArray());
        var verdict = _engine.Classify(Entries.File("使用说明.txt", 80), content);
        Assert.NotEqual(Classification.Trash, verdict.Classification);
    }
}
