namespace CleanExtract.Core.Config;

public static class ListText
{
    public static string Join(IEnumerable<string> items)
        => string.Join(Environment.NewLine, items.Where(static s => !string.IsNullOrWhiteSpace(s)));

    public static List<string> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
