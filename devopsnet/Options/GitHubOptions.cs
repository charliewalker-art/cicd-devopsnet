namespace devopsnet.Options;

public class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
}