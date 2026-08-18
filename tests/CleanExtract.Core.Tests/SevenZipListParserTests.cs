using CleanExtract.Core.SevenZip;

namespace CleanExtract.Core.Tests;

public sealed class SevenZipListParserTests
{
    [Fact]
    public void ParsesUnicodeAndSpecialNames()
    {
        var slt = """
            7-Zip 26.02 (x64)

            Listing archive: sample.zip

            --
            Path = sample.zip
            Type = zip

            ----------
            Path = Game/game.exe
            Folder = -
            Size = 1024
            Packed Size = 512
            Encrypted = -

            Path = ★本站最新网址.url
            Folder = -
            Size = 86
            Packed Size = 80
            Encrypted = -

            Path = 日本語/説明.txt
            Folder = -
            Size = 20
            Packed Size = 20
            Encrypted = -

            Path = emoji 😀/hello world.pak
            Folder = -
            Size = 8
            Packed Size = 8
            Encrypted = -

            Path = __MACOSX/
            Folder = +
            Size = 0
            Packed Size = 0
            Encrypted = -

            Path = nested/deep/path/data.bin
            Folder = -
            Size = 4096
            Packed Size = 100
            Encrypted = +
            """;

        var entries = SevenZipListParser.Parse(slt);
        Assert.Equal(6, entries.Count);
        Assert.Contains(entries, e => e.Path == "Game/game.exe" && !e.IsDirectory && e.Size == 1024);
        Assert.Contains(entries, e => e.FileName == "★本站最新网址.url");
        Assert.Contains(entries, e => e.Path == "日本語/説明.txt");
        Assert.Contains(entries, e => e.Path.Contains("hello world.pak", StringComparison.Ordinal));
        Assert.Contains(entries, e => e.Path == "__MACOSX/" && e.IsDirectory);
        Assert.Contains(entries, e => e.Path == "nested/deep/path/data.bin" && e.IsEncrypted);
    }

    [Fact]
    public void EmptyOutput_ReturnsEmpty()
    {
        Assert.Empty(SevenZipListParser.Parse(""));
    }
}
