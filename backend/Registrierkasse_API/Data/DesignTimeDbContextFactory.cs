using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Registrierkasse_API.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            // Buraya kendi connection string'inizi yazın
            optionsBuilder.UseNpgsql("Host=localhost;Database=kassedb;Username=postgres;Password=Juke1034#;Port=5432");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
} 
