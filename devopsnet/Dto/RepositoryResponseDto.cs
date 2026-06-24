namespace devopsnet.Dto;

public class RepositoryResponseDto
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public string DefaultBranch { get; set; } = string.Empty;
}