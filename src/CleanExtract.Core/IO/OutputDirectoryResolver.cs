using CleanExtract.Core.Archive;

namespace CleanExtract.Core.IO;

public static class OutputDirectoryResolver
{
    private static readonly string[] CompoundExtensions =
    [
        ".tar.gz",
        ".tar.bz2",
        ".tar.xz",
        ".tar.zst",
        ".tgz",
        ".tbz",
        ".tbz2",
    ];

    public static string Resolve(string archivePath, string? requestedDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedDirectory))
            return UniqueDirectory(requestedDirectory);

        var parent = Path.GetDirectoryName(archivePath);
        if (string.IsNullOrWhiteSpace(parent))
            parent = Environment.CurrentDirectory;

        var stem = GetArchiveStem(archivePath);
        var candidate = Path.Combine(parent, stem);
        return UniqueDirectory(candidate);
    }

    public static string GetArchiveStem(string archivePath)
    {
        var name = Path.GetFileName(archivePath);
        foreach (var ext in CompoundExtensions)
        {
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return name[..^ext.Length];
        }

        return Path.GetFileNameWithoutExtension(name);
    }

    public static string UniqueDirectory(string desiredPath)
    {
        if (!PathExists(desiredPath))
            return desiredPath;

        if (Directory.Exists(desiredPath) && IsEmptyDirectory(desiredPath))
            return desiredPath;

        var parent = Path.GetDirectoryName(desiredPath) ?? Environment.CurrentDirectory;
        var stem = Path.GetFileName(desiredPath);
        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(parent, $"{stem} ({i})");
            if (!PathExists(candidate) || (Directory.Exists(candidate) && IsEmptyDirectory(candidate)))
                return candidate;
        }

        return Path.Combine(parent, $"{stem} ({Guid.NewGuid():N})");
    }

    public static void EnsureWritable(string directory)
    {
        Directory.CreateDirectory(directory);
        var probe = Path.Combine(directory, $".cleanextract-write-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "ok");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ArchiveException("The output folder is not writable.", ex);
        }
        catch (IOException ex)
        {
            throw new ArchiveException("The output folder could not be created. The disk may be full or the path is invalid.", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(probe))
                    File.Delete(probe);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static bool PathExists(string path) => Directory.Exists(path) || File.Exists(path);

    private static bool IsEmptyDirectory(string path)
    {
        return Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any();
    }
}
