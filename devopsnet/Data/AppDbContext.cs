using devopsnet.Models;
using Microsoft.EntityFrameworkCore;

namespace devopsnet.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<GitHubAccount> GitHubAccounts => Set<GitHubAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<GitHubAccount>(entity =>
        {
            entity.HasIndex(g => g.UserId).IsUnique();
            entity.HasIndex(g => g.GitHubId).IsUnique();

            entity.HasOne(g => g.User)
                  .WithOne(u => u.GitHubAccount)
                  .HasForeignKey<GitHubAccount>(g => g.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}