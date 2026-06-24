namespace devopsnet.Dto;

public class CloneRequestDto
{
    public string RepoFullName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
}