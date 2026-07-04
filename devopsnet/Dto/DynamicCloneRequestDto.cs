namespace devopsnet.Dto;

public class DynamicCloneRequestDto
{
    public string RepoUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
}