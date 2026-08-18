using System.Text;
using CleanExtract.Core.Archive;

namespace CleanExtract.Core.Cleaning;

public sealed class EntryContent
{
    public required byte[] Bytes { get; init; }

    private string? _text;
    private bool _textDecoded;

    public string? Text
    {
        get
        {
            if (_textDecoded)
                return _text;
            _text = TextDecoder.TryDecode(Bytes);
            _textDecoded = true;
            return _text;
        }
    }

    public static EntryContent FromBytes(byte[] bytes) => new() { Bytes = bytes };
}

internal static class TextDecoder
{
    static TextDecoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string? TryDecode(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        if (LooksLikeUtf8(bytes))
            return Encoding.UTF8.GetString(bytes);

        try
        {
            var gbk = Encoding.GetEncoding(54936);
            return gbk.GetString(bytes);
        }
        catch
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static bool LooksLikeUtf8(byte[] bytes)
    {
        try
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            encoding.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
