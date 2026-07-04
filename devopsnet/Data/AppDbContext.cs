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
    public DbSet<Pipeline> Pipelines => Set<Pipeline>(); // <-- AJOUT UNIQUE POUR CORRIGER TON ERREUR

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

        // AJOUT DE LA RELATION POUR L'ENTITÉ PIPELINE
        modelBuilder.Entity<Pipeline>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
            entity.Property(p => p.CloneUrl).IsRequired();
            entity.Property(p => p.Branch).IsRequired().HasMaxLength(50);

            // Un utilisateur a plusieurs pipelines (Relation 1-N)
            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}