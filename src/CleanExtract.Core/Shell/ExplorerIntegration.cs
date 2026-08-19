using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CleanExtract.Core.Shell;

[SupportedOSPlatform("windows")]
public static class ExplorerIntegration
{
    public const string VerbName = "CleanExtract";
    public const string DisplayName = "干净解压";

    public static IReadOnlyList<string> Extensions { get; } =
    [
        ".zip", ".rar", ".7z", ".xz", ".gz", ".tar", ".tgz", ".tbz", ".tbz2",
        ".001", ".iso", ".cab", ".lzh", ".lha", ".zst", ".zipx",
    ];

    public static string CommandLine(string executablePath)
        => $"\"{executablePath}\" --extract \"%1\"";

    public static string AppliesToFilter =>
        string.Join(" OR ", Extensions.Select(static ext => $"System.FileExtension:\"{ext}\""));

    public static string? InstalledExecutablePath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(CommandKey(Extensions[0]));
        var value = key?.GetValue(null) as string;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end > 1)
                return text[1..end];
        }

        var space = text.IndexOf(" \"%1\"", StringComparison.OrdinalIgnoreCase);
        return space > 0 ? text[..space].Trim('"') : text.Trim('"');
    }

    public static bool IsInstalled()
        => !string.IsNullOrWhiteSpace(InstalledExecutablePath());

    public static bool IsInstalledFor(string executablePath)
    {
        var installed = InstalledExecutablePath();
        return installed is not null
               && string.Equals(installed, executablePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void Install(string executablePath)
    {
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("Clean Extract executable was not found.", executablePath);

        var command = CommandLine(executablePath);
        foreach (var extension in Extensions)
            WriteVerb(AssociationKey(extension), command, executablePath, appliesTo: null);

        WriteVerb(@"Software\Classes\CompressedFolder\shell\" + VerbName, command, executablePath, appliesTo: null);
        WriteVerb(@"Software\Classes\*\shell\" + VerbName, command, executablePath, AppliesToFilter);
        NotifyExplorer();
    }

    public static void Uninstall()
    {
        foreach (var extension in Extensions)
            DeleteKey(AssociationKey(extension));

        DeleteKey(@"Software\Classes\CompressedFolder\shell\" + VerbName);
        DeleteKey(@"Software\Classes\*\shell\" + VerbName);
        NotifyExplorer();
    }

    private static string AssociationKey(string extension)
        => $@"Software\Classes\SystemFileAssociations\{extension}\shell\{VerbName}";

    private static string CommandKey(string extension)
        => AssociationKey(extension) + @"\command";

    private static void WriteVerb(string shellKey, string command, string executablePath, string? appliesTo)
    {
        using var key = Registry.CurrentUser.CreateSubKey(shellKey);
        key.SetValue(null, DisplayName);
        key.SetValue("MUIVerb", DisplayName);
        key.SetValue("Icon", executablePath);
        if (!string.IsNullOrWhiteSpace(appliesTo))
            key.SetValue("AppliesTo", appliesTo);
        using var commandKey = Registry.CurrentUser.CreateSubKey(shellKey + @"\command");
        commandKey.SetValue(null, command);
    }

    private static void DeleteKey(string shellKey)
    {
        Registry.CurrentUser.DeleteSubKeyTree(shellKey, throwOnMissingSubKey: false);
    }

    private static void NotifyExplorer()
    {
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // Explorer refresh is best-effort.
        }
    }

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
