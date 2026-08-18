using CleanExtract.Core.IO;

namespace CleanExtract.Core.Tests;

public sealed class OutputDirectoryResolverTests
{
    [Fact]
    public void UsesArchiveStemNextToArchive()
    {
        var archive = @"D:\Downloads\Game.rar";
        var output = OutputDirectoryResolver.Resolve(archive);
        Assert.Equal(@"D:\Downloads\Game", output);
    }

    [Fact]
    public void StripsCompoundTarGz()
    {
        Assert.Equal("backup", OutputDirectoryResolver.GetArchiveStem(@"C:\a\backup.tar.gz"));
    }

    [Fact]
    public void UniqueDirectory_AddsNumericSuffix()
    {
        var root = Path.Combine(Path.GetTempPath(), "cleanextract-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var first = Path.Combine(root, "Game");
            Directory.CreateDirectory(first);
            File.WriteAllText(Path.Combine(first, "keep.txt"), "x");
            var resolved = OutputDirectoryResolver.UniqueDirectory(first);
            Assert.Equal(Path.Combine(root, "Game (1)"), resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UniqueDirectory_ReusesEmptyFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "cleanextract-tests-" + Guid.NewGuid().ToString("N"));
        var empty = Path.Combine(root, "Game");
        Directory.CreateDirectory(empty);
        try
        {
            Assert.Equal(empty, OutputDirectoryResolver.UniqueDirectory(empty));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
