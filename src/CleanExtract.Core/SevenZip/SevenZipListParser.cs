using System.Globalization;
using CleanExtract.Core.Archive;

namespace CleanExtract.Core.SevenZip;

public static class SevenZipListParser
{
    public static IReadOnlyList<ArchiveEntry> Parse(string sltOutput)
    {
        if (string.IsNullOrWhiteSpace(sltOutput))
            return [];

        var blocks = SplitEntryBlocks(sltOutput);
        var entries = new List<ArchiveEntry>(blocks.Count);
        foreach (var block in blocks)
        {
            var fields = ParseFields(block);
            if (!fields.TryGetValue("Path", out var path) || string.IsNullOrWhiteSpace(path))
                continue;

            var isDirectory = IsDirectory(fields, path);
            _ = long.TryParse(Get(fields, "Size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size);
            _ = long.TryParse(Get(fields, "Packed Size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var packed);
            var encrypted = string.Equals(Get(fields, "Encrypted"), "+", StringComparison.Ordinal);

            DateTimeOffset? modified = null;
            if (TryParseTime(Get(fields, "Modified"), out var parsed))
                modified = parsed;

            entries.Add(new ArchiveEntry
            {
                Path = path.Trim(),
                Size = isDirectory ? 0 : size,
                PackedSize = packed,
                IsDirectory = isDirectory,
                IsEncrypted = encrypted,
                Modified = modified,
            });
        }

        return entries;
    }

    private static List<string> SplitEntryBlocks(string sltOutput)
    {
        var lines = sltOutput.Replace("\r\n", "\n").Split('\n');
        var start = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("----------", StringComparison.Ordinal))
            {
                start = i + 1;
                break;
            }
        }

        var blocks = new List<string>();
        var current = new List<string>();
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                if (current.Count > 0)
                {
                    blocks.Add(string.Join('\n', current));
                    current.Clear();
                }

                continue;
            }

            current.Add(line);
        }

        if (current.Count > 0)
            blocks.Add(string.Join('\n', current));

        return blocks;
    }

    private static Dictionary<string, string> ParseFields(string block)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in block.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var separator = line.IndexOf(" = ", StringComparison.Ordinal);
            if (separator <= 0)
                continue;
            var key = line[..separator];
            var value = line[(separator + 3)..];
            fields[key] = value;
        }

        return fields;
    }

    private static string? Get(Dictionary<string, string> fields, string key)
    {
        return fields.TryGetValue(key, out var value) ? value : null;
    }

    private static bool IsDirectory(Dictionary<string, string> fields, string path)
    {
        if (string.Equals(Get(fields, "Folder"), "+", StringComparison.Ordinal))
            return true;
        if (string.Equals(Get(fields, "Folder"), "-", StringComparison.Ordinal))
            return false;

        var attributes = Get(fields, "Attributes") ?? string.Empty;
        if (attributes.Contains('D', StringComparison.OrdinalIgnoreCase))
            return true;

        return path.EndsWith('/') || path.EndsWith('\\');
    }

    private static bool TryParseTime(string? value, out DateTimeOffset time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
        };

        return DateTimeOffset.TryParseExact(
                   value,
                   formats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeLocal,
                   out time)
               || DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out time);
    }
}
