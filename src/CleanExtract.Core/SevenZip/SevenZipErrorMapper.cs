using CleanExtract.Core.Archive;

namespace CleanExtract.Core.SevenZip;

internal static class SevenZipErrorMapper
{
    public static void ThrowIfFailed(SevenZipProcessResult result, bool passwordProvided)
    {
        var combined = $"{result.StdoutText}\n{result.StderrText}";

        if (IsWrongPassword(combined))
            throw new WrongPasswordException();

        if (IsPasswordRequired(combined, result.ExitCode, passwordProvided))
            throw new PasswordRequiredException();

        if (IsMissingVolume(combined))
            throw new MissingVolumeException(FriendlyMissingVolume(combined));

        if (IsUnsupported(combined))
            throw new UnsupportedArchiveException("This archive format is not supported, or the file is not a valid archive.");

        if (IsCorrupt(combined))
            throw new CorruptedArchiveException("The archive is damaged or incomplete and cannot be opened.");

        if (IsDiskFull(combined))
            throw new ArchiveException("There is not enough disk space to extract this archive.");

        if (IsAccessDenied(combined))
            throw new ArchiveException("Permission denied. The output folder may require administrator rights, or another program is using the files.");

        if (result.ExitCode is 0 or 1)
            return;

        if (result.ExitCode == 255)
        {
            if (!passwordProvided)
                throw new PasswordRequiredException();
            throw new OperationCancelledArchiveException();
        }

        if (result.ExitCode == 8)
            throw new ArchiveException("7-Zip ran out of memory while processing this archive.");

        if (result.ExitCode == 7)
            throw new ArchiveException("7-Zip rejected the command. This is likely a Clean Extract bug; check the log for details.");

        var detail = FirstUsefulLine(combined);
        throw new ArchiveException(string.IsNullOrWhiteSpace(detail)
            ? $"7-Zip failed with exit code {result.ExitCode}."
            : $"7-Zip failed (exit code {result.ExitCode}): {detail}");
    }

    private static bool IsWrongPassword(string text)
    {
        return Contains(text, "wrong password")
               || Contains(text, "incorrect password")
               || Contains(text, "Data Error in encrypted file");
    }

    private static bool IsPasswordRequired(string text, int exitCode, bool passwordProvided)
    {
        if (passwordProvided)
            return false;

        return Contains(text, "enter password")
               || Contains(text, "encrypted archive")
               || Contains(text, "Can't open encrypted")
               || Contains(text, "Cannot open encrypted")
               || (exitCode != 0 && Contains(text, "encrypted") && Contains(text, "password"));
    }

    private static bool IsMissingVolume(string text)
    {
        return Contains(text, "missing volume")
               || Contains(text, "cannot find volume")
               || Contains(text, "can not find volume")
               || Contains(text, "Unavailable volume")
               || Contains(text, "split file") && Contains(text, "cannot");
    }

    private static bool IsUnsupported(string text)
    {
        return Contains(text, "Cannot open the file as archive")
               || Contains(text, "Can not open the file as archive")
               || Contains(text, "is not supported archive")
               || Contains(text, "Unsupported Method");
    }

    private static bool IsCorrupt(string text)
    {
        return Contains(text, "Data Error")
               || Contains(text, "CRC Failed")
               || Contains(text, "CRC error")
               || Contains(text, "Unexpected end of archive")
               || Contains(text, "Headers Error")
               || Contains(text, "Is not archive");
    }

    private static bool IsDiskFull(string text)
    {
        return Contains(text, "No space")
               || Contains(text, "not enough space")
               || Contains(text, "disk full")
               || Contains(text, "There is not enough space");
    }

    private static bool IsAccessDenied(string text)
    {
        return Contains(text, "Access is denied")
               || Contains(text, "Access denied")
               || Contains(text, "Permission denied")
               || Contains(text, "Cannot create output directory");
    }

    private static bool Contains(string text, string value)
        => text.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string FriendlyMissingVolume(string text)
    {
        var line = FirstUsefulLine(text);
        return string.IsNullOrWhiteSpace(line)
            ? "This is a split archive and at least one volume is missing."
            : $"This is a split archive and a volume is missing. {line}";
    }

    private static string FirstUsefulLine(string text)
    {
        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("7-Zip", StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.StartsWith("Scanning", StringComparison.OrdinalIgnoreCase))
                continue;
            return line.Length > 240 ? line[..240] : line;
        }

        return string.Empty;
    }
}
