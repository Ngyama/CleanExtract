namespace CleanExtract.Core;

public static class AppPaths
{
    public static string UserDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanExtract");

    public static string LogsDirectory => Path.Combine(UserDataDirectory, "logs");

    public static string TodayLogFile => Path.Combine(LogsDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
}
