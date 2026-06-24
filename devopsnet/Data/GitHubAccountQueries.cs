using devopsnet.Models;

namespace devopsnet.Data;

public static class GitHubAccountQueries
{
    public static IQueryable<GitHubAccount> ByUserId(this IQueryable<GitHubAccount> query, Guid userId)
    {
        return query.Where(g => g.UserId == userId);
    }

    public static IQueryable<GitHubAccount> ByGitHubId(this IQueryable<GitHubAccount> query, long gitHubId)
    {
        return query.Where(g => g.GitHubId == gitHubId);
    }
}