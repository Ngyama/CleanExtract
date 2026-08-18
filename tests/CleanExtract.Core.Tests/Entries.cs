using CleanExtract.Core.Archive;

namespace CleanExtract.Core.Tests;

internal static class Entries
{
    public static ArchiveEntry File(string path, long size = 128, bool encrypted = false)
    {
        var isDirectory = path.EndsWith('/') || path.EndsWith('\\');
        return new ArchiveEntry
        {
            Path = path,
            Size = isDirectory ? 0 : size,
            IsDirectory = isDirectory,
            IsEncrypted = encrypted,
        };
    }
}
