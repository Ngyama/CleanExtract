namespace CleanExtract.Core.Config;

public sealed class RuleConfig
{
    public int TrashThreshold { get; set; } = 75;

    public int SuspiciousThreshold { get; set; } = 38;

    public int MaxInspectBytes { get; set; } = 48 * 1024;

    public bool FilterMacosMetadata { get; set; } = true;

    public bool FilterThumbsDb { get; set; } = true;

    public bool FilterDesktopIni { get; set; }

    public bool KeepSuspicious { get; set; } = true;

    public bool EnableAdFilenameDetection { get; set; } = true;

    public bool EnableUrlInspection { get; set; } = true;

    public bool EnableTextInspection { get; set; } = true;

    public bool EnableImageAdDetection { get; set; } = true;

    public List<string> AdPhrasesHigh { get; set; } =
    [
        "最新网址",
        "最新地址",
        "永久地址",
        "永久网址",
        "永久发布",
        "备用网址",
        "备用地址",
        "防失联",
        "防止失联",
        "本站地址",
        "本站网址",
        "本站最新",
        "访问本站",
        "收藏本站",
        "更多游戏下载",
        "tg频道",
        "telegram频道",
        "电报频道",
        "来自tg",
    ];

    public List<string> AdPhrasesMedium { get; set; } =
    [
        "发布页",
        "更多游戏",
        "更多资源",
        "资源下载",
        "关注公众号",
        "加入群",
        "qq群",
        "微信群",
    ];

    public List<string> AdPhrasesLow { get; set; } =
    [
        "游戏下载",
        "使用说明",
        "下载说明",
        "下载地址",
    ];

    public List<string> PromoContentPhrases { get; set; } =
    [
        "最新网址",
        "永久地址",
        "永久网址",
        "请收藏",
        "防止失联",
        "防失联",
        "更多资源",
        "访问本站",
        "收藏本站",
        "备用网址",
        "发布页",
        "tg频道",
        "t.me",
    ];

    public List<string> TrustedUrlNames { get; set; } =
    [
        "official",
        "website",
        "homepage",
        "documentation",
        "docs",
        "support",
        "help",
        "license",
        "github",
        "gitlab",
        "官网",
        "官方网站",
        "官方主页",
        "主页",
        "帮助",
        "文档",
        "手册",
    ];

    public List<string> ReadmeHints { get; set; } =
    [
        "readme",
        "manual",
        "license",
        "changelog",
        "说明",
        "手册",
        "必读",
        "帮助",
        "文档",
    ];

    public List<string> BlockedDomains { get; set; } = [];

    public List<string> TrustedDomains { get; set; } = DomainCatalog.DefaultTrusted.ToList();

    public List<string> SuspiciousDomains { get; set; } = DomainCatalog.DefaultSuspicious.ToList();

    public List<string> AlwaysKeepNames { get; set; } = [];

    public List<string> AlwaysFilterNames { get; set; } = [];

    public List<string> InspectExtensions { get; set; } =
    [
        ".url",
        ".txt",
        ".html",
        ".htm",
        ".md",
    ];

    public List<string> ImageExtensions { get; set; } =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp",
        ".bmp",
        ".tif",
        ".tiff",
        ".jfif",
        ".avif",
    ];

    public List<string> ImageAdPhrasesHigh { get; set; } =
    [
        "二维码",
        "扫码关注",
        "扫码加群",
        "微信二维码",
        "公众号二维码",
        "客服微信",
        "加微信",
        "微信群",
        "qq群",
        "加群",
        "公众号",
        "扫码",
        "防失联",
        "最新网址",
        "备用网址",
        "电报频道",
        "tg频道",
        "telegram频道",
    ];

    public List<string> ImageAdPhrasesMedium { get; set; } =
    [
        "微信",
        "福利",
        "推广",
        "广告",
        "联系方式",
        "加我",
    ];

    public List<string> ImageProtectedNames { get; set; } =
    [
        "cover",
        "preview",
        "screenshot",
        "screenshots",
        "logo",
        "icon",
        "artwork",
        "poster",
        "wallpaper",
        "thumbnail",
        "封面",
        "截图",
        "预览",
        "海报",
        "壁纸",
        "图标",
        "立绘",
    ];
}

public sealed class AppSettings
{
    public bool FilterMacosMetadata { get; set; } = true;

    public bool FilterThumbsDb { get; set; } = true;

    public bool FilterDesktopIni { get; set; }

    public bool KeepSuspicious { get; set; } = true;

    public bool EnableAdFilenameDetection { get; set; } = true;

    public bool EnableUrlInspection { get; set; } = true;

    public bool EnableTextInspection { get; set; } = true;

    public bool EnableImageAdDetection { get; set; } = true;

    public bool CheckForUpdates { get; set; } = true;

    public string UpdateFeedUrl { get; set; } = UpdateDefaults.GitHubRepoUrl;

    public void ApplyTo(RuleConfig rules)
    {
        rules.FilterMacosMetadata = FilterMacosMetadata;
        rules.FilterThumbsDb = FilterThumbsDb;
        rules.FilterDesktopIni = FilterDesktopIni;
        rules.KeepSuspicious = KeepSuspicious;
        rules.EnableAdFilenameDetection = EnableAdFilenameDetection;
        rules.EnableUrlInspection = EnableUrlInspection;
        rules.EnableTextInspection = EnableTextInspection;
        rules.EnableImageAdDetection = EnableImageAdDetection;
    }
}

public static class UpdateDefaults
{
    public const string GitHubRepoUrl = "https://github.com/Ngyama/CleanExtract";
}

public sealed class DomainLists
{
    public List<string> BlockedDomains { get; set; } = [];

    public List<string> TrustedDomains { get; set; } = [];

    public List<string> SuspiciousDomains { get; set; } = [];
}

public sealed class UserOverrides
{
    public List<string> AlwaysKeepNames { get; set; } = [];

    public List<string> AlwaysFilterNames { get; set; } = [];
}
