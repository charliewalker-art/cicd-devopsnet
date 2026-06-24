namespace devopsnet.Models;

public class GitHubAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long GitHubId { get; set; }
    public string GitHubUsername { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }

    public User User { get; set; } = null!;
}