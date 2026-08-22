using CleanExtract.Core.Config;
using CleanExtract.Core.Logging;
using Velopack;
using Velopack.Sources;

namespace CleanExtract;

internal static class AppUpdate
{
    public static async Task<UpdateCheckResult> CheckAsync(
        AppSettings settings,
        IAppLog log,
        bool downloadAndRestart)
    {
        try
        {
            var manager = CreateManager(settings);
            if (!manager.IsInstalled)
            {
                return new UpdateCheckResult
                {
                    Message = "当前是便携版。自动更新只在使用安装包安装后可用。",
                };
            }

            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(true);
            if (update is null)
            {
                return new UpdateCheckResult
                {
                    Message = "已是最新版本。",
                    CurrentVersion = manager.CurrentVersion?.ToString(),
                };
            }

            var version = update.TargetFullRelease.Version.ToString();
            if (!downloadAndRestart)
            {
                return new UpdateCheckResult
                {
                    Message = $"发现新版本 {version}。",
                    UpdateAvailable = true,
                    Version = version,
                };
            }

            await manager.DownloadUpdatesAsync(update).ConfigureAwait(true);
            manager.ApplyUpdatesAndRestart(update);
            return new UpdateCheckResult
            {
                Message = "正在重启以完成更新…",
                UpdateAvailable = true,
                Version = version,
            };
        }
        catch (Exception ex)
        {
            log.Warn($"Update check failed: {ex.Message}");
            return new UpdateCheckResult
            {
                Message = "检查更新失败：" + ex.Message,
            };
        }
    }

    private static UpdateManager CreateManager(AppSettings settings)
    {
        var url = string.IsNullOrWhiteSpace(settings.UpdateFeedUrl)
            ? UpdateDefaults.GitHubRepoUrl
            : settings.UpdateFeedUrl.Trim();

        if (url.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return new UpdateManager(new GithubSource(url, accessToken: null, prerelease: false));

        return new UpdateManager(url);
    }
}

internal sealed class UpdateCheckResult
{
    public string Message { get; init; } = string.Empty;

    public bool UpdateAvailable { get; init; }

    public string? Version { get; init; }

    public string? CurrentVersion { get; init; }
}
