namespace CleanExtract.Core.SevenZip;

public static class SevenZipLocator
{
    public static string Find(string? baseDirectory = null)
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseDirectory))
            roots.Add(baseDirectory);
        roots.Add(AppContext.BaseDirectory);

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var candidate in Candidates(root))
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new Archive.ArchiveBackendNotFoundException(
            Path.Combine(AppContext.BaseDirectory, "resources", "7zz.exe"));
    }

    private static IEnumerable<string> Candidates(string root)
    {
        yield return Path.Combine(root, "resources", "7zz.exe");
        yield return Path.Combine(root, "resources", "7z.exe");
        yield return Path.Combine(root, "resources", "7zip", "7z.exe");
        yield return Path.Combine(root, "7zz.exe");
        yield return Path.Combine(root, "7z.exe");
    }
}
