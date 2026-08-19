using System.IO;

namespace CleanExtract;

internal static class StartupArgs
{
    public static string[] Collect(string[] eventArgs)
    {
        if (eventArgs is { Length: > 0 })
            return eventArgs;

        return Environment.GetCommandLineArgs().Skip(1).ToArray();
    }

    public static string? ResolveArchive(IEnumerable<string> args)
    {
        foreach (var raw in args)
        {
            var arg = raw.Trim().Trim('"');
            if (arg.Length == 0 || arg.StartsWith('-'))
                continue;
            if (File.Exists(arg))
                return Path.GetFullPath(arg);
        }

        return null;
    }

    public static string Redact(string argument)
    {
        if (argument.StartsWith("-p", StringComparison.OrdinalIgnoreCase) && argument.Length > 2)
            return "-p***";
        return argument;
    }
}
