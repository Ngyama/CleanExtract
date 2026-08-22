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

    public static string Resolve(string archivePath, string? requestedDirectory = null, bool uniquify = true)
    {
        string destination;
        if (!string.IsNullOrWhiteSpace(requestedDirectory))
        {
            destination = Path.GetFullPath(requestedDirectory);
        }
        else
        {
            destination = DefaultSiblingDirectory(archivePath);
        }

        return uniquify ? UniqueDirectory(destination) : destination;
    }

    public static string DefaultSiblingDirectory(string archivePath)
    {
        var parent = ArchiveParent(archivePath);
        var stem = GetArchiveStem(archivePath);
        return Path.Combine(parent, stem);
    }

    public static string ArchiveParent(string archivePath)
    {
        var full = Path.GetFullPath(archivePath);
        var parent = Path.GetDirectoryName(full);
        return string.IsNullOrWhiteSpace(parent) ? Environment.CurrentDirectory : parent;
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
