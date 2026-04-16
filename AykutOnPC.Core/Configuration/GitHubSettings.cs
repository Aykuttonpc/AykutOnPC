namespace AykutOnPC.Core.Configuration;

public class GitHubSettings
{
    public const string SectionName = "GitHubSettings";

    public string ApiUrl { get; set; } = "https://api.github.com";
    public string UserAgent { get; set; } = "AykutOnPC-App/1.0";
    public string Token { get; set; } = string.Empty;
    public int CacheDurationMinutes { get; set; } = 10;
    public int ErrorCacheDurationHours { get; set; } = 1;
}
