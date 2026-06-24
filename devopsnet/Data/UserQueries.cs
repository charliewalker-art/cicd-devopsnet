using devopsnet.Models;
using Microsoft.EntityFrameworkCore;

namespace devopsnet.Data;

public static class UserQueries
{
    public static IQueryable<User> ByUsername(this IQueryable<User> query, string username)
    {
        return query.Where(u => u.Username == username);
    }

    public static IQueryable<User> ByEmail(this IQueryable<User> query, string email)
    {
        return query.Where(u => u.Email == email);
    }

    public static IQueryable<User> ById(this IQueryable<User> query, Guid id)
    {
        return query.Where(u => u.Id == id);
    }

    public static IQueryable<User> WithGitHubAccount(this IQueryable<User> query)
    {
        return query.Include(u => u.GitHubAccount);
    }
}