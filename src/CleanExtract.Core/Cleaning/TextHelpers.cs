using System.Text;
using System.Text.RegularExpressions;

namespace CleanExtract.Core.Cleaning;

public static class NameNormalizer
{
    public static string NormalizeStem(string fileName)
    {
        var stem = fileName;
        var slash = Math.Max(stem.LastIndexOf('/'), stem.LastIndexOf('\\'));
        if (slash >= 0)
            stem = stem[(slash + 1)..];

        var dot = stem.LastIndexOf('.');
        if (dot > 0)
            stem = stem[..dot];

        stem = stem.Normalize(NormalizationForm.FormKC);
        var buffer = new StringBuilder(stem.Length);
        foreach (var ch in stem)
        {
            if (char.IsWhiteSpace(ch) || IsDecorative(ch))
                continue;
            if (char.IsLetterOrDigit(ch) || IsCjk(ch) || ch is '_' or '-')
                buffer.Append(char.ToLowerInvariant(ch));
        }

        return buffer.ToString();
    }

    public static bool ContainsPhrase(string normalized, string phrase)
    {
        var needle = NormalizeStem(phrase);
        return needle.Length > 0 && normalized.Contains(needle, StringComparison.Ordinal);
    }

    public static bool EqualsPhrase(string normalized, string phrase)
    {
        var needle = NormalizeStem(phrase);
        return needle.Length > 0 && string.Equals(normalized, needle, StringComparison.Ordinal);
    }

    public static bool IsCjk(char ch)
    {
        return ch is >= '\u3040' and <= '\u30FF'
            or >= '\u3400' and <= '\u9FFF'
            or >= '\uF900' and <= '\uFAFF'
            or >= '\uFF66' and <= '\uFF9D';
    }

    private static bool IsDecorative(char ch)
    {
        if (char.IsPunctuation(ch) || char.IsSymbol(ch))
            return true;

        return ch is '★' or '☆' or '●' or '○' or '■' or '□' or '▲' or '△' or '◆' or '◇'
            or '※' or '→' or '←' or '·' or '•' or '【' or '】' or '「' or '」' or '『' or '』'
            or '《' or '》' or '（' or '）' or '〔' or '〕';
    }
}

public static class UrlExtractor
{
    private static readonly Regex HttpUrl = new(
        @"https?://[^\s<>""'\[\]{}|\\^`]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<string> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var urls = new List<string>();
        foreach (Match match in HttpUrl.Matches(text))
        {
            var url = match.Value.TrimEnd('.', ',', ';', ')', ']', '}', '>', '"', '\'');
            if (url.Length > 0)
                urls.Add(url);
        }

        return urls;
    }

    public static string? TryGetHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;
        return string.IsNullOrWhiteSpace(uri.Host) ? null : uri.Host.Trim('.').ToLowerInvariant();
    }
}

public static class InternetShortcutParser
{
    public static string? TryGetUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[4..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        var urls = UrlExtractor.Extract(text);
        return urls.Count == 1 ? urls[0] : null;
    }
}
