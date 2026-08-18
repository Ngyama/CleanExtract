using CleanExtract.Core.Cleaning;

namespace CleanExtract.Core.Tests;

public sealed class NameNormalizerTests
{
    [Theory]
    [InlineData("★本站最新网址.url", "本站最新网址")]
    [InlineData("【最新网址】.url", "最新网址")]
    [InlineData("[备用网址].txt", "备用网址")]
    [InlineData("  永 久 地 址  ", "永久地址")]
    public void StripsDecorations(string fileName, string expected)
    {
        Assert.Equal(expected, NameNormalizer.NormalizeStem(fileName));
    }

    [Fact]
    public void ParsesInternetShortcut()
    {
        var url = InternetShortcutParser.TryGetUrl("""
            [InternetShortcut]
            URL=https://github.com/example/project
            """);
        Assert.Equal("https://github.com/example/project", url);
    }
}
