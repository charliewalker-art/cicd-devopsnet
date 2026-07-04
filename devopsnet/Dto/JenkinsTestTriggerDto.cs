namespace devopsnet.Dto;

public class JenkinsTestTriggerDto
{
    public string RepoUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string ImageName { get; set; } = string.Empty;
}