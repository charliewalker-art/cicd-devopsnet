using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace devopsnet.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // On utilise une chaîne factice pour le build, 
            // car le but est juste de générer le bundle de migration, pas de se connecter.
            var connectionString = "Host=localhost;Port=5432;Database=design_time;Username=dummy;Password=dummy";

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}