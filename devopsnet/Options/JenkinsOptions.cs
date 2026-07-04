namespace devopsnet.Options;

public class JenkinsOptions
{
    public const string SectionName = "Jenkins";

    public string BaseUrl { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
}