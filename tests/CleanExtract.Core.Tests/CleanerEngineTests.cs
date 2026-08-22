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

    [Theory]
    [InlineData("zombie/PC/zombie1/资源来自TG频道@xxx.txt")]
    [InlineData("zombie/PC/zombie1/资源来自TG频道@xxx.url")]
    [InlineData("zombie/PC/zombie1/资源来自TG频道@xxx.png")]
    [InlineData("zombie1/关注TG频道.jpg")]
    public void NestedTelegramPromo_IsTrash(string path)
    {
        var verdict = _engine.Classify(Entries.File(path, 80));
        Assert.Equal(Classification.Trash, verdict.Classification);
        Assert.Equal("ad.telegram.filename", verdict.MatchedRule);
        Assert.False(verdict.ShouldExtract(keepSuspicious: true));
    }

    [Fact]
    public void NestedClassicAdUrl_IsStillTrash()
    {
        var verdict = _engine.Classify(Entries.File("zombie/PC/zombie1/★本站最新网址.url", 80));
        Assert.Equal(Classification.Trash, verdict.Classification);
    }

    [Fact]
    public void NestedGameExe_IsNotAffectedByTelegramRule()
    {
        var verdict = _engine.Classify(Entries.File("zombie/PC/zombie1/zombie1.exe", 8_000_000));
        Assert.Equal(Classification.Clean, verdict.Classification);
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

    [Theory]
    [InlineData("二维码.png")]
    [InlineData("微信二维码.jpg")]
    [InlineData("公众号.webp")]
    [InlineData("扫码加群.gif")]
    [InlineData("客服微信.png")]
    public void PromoImages_AreTrash(string path)
    {
        var verdict = _engine.Classify(Entries.File(path, 48_000));
        Assert.Equal(Classification.Trash, verdict.Classification);
        Assert.Equal("ad.image.high", verdict.MatchedRule);
    }

    [Fact]
    public void WeChatNamedImage_IsSuspiciousNotTrash()
    {
        var verdict = _engine.Classify(Entries.File("微信.jpg", 36_000));
        Assert.Equal(Classification.Suspicious, verdict.Classification);
        Assert.True(verdict.ShouldExtract(keepSuspicious: true));
    }

    [Theory]
    [InlineData("cover.png")]
    [InlineData("screenshot.jpg")]
    [InlineData("封面.png")]
    [InlineData("icon.png")]
    [InlineData("screenshot2.webp")]
    public void ProtectedImages_AreClean(string path)
    {
        var verdict = _engine.Classify(Entries.File(path, 120_000));
        Assert.Equal(Classification.Clean, verdict.Classification);
    }

    [Fact]
    public void OrdinaryGameTexture_IsClean()
    {
        var verdict = _engine.Classify(Entries.File("Game/textures/wall01.png", 250_000));
        Assert.Equal(Classification.Clean, verdict.Classification);
    }

    [Fact]
    public void NumberedRootJpeg_IsNotTrash()
    {
        var verdict = _engine.Classify(Entries.File("1.jpg", 80_000));
        Assert.NotEqual(Classification.Trash, verdict.Classification);
    }
}
