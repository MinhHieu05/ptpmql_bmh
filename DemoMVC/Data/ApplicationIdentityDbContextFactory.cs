using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DemoMVC.Data
{
    public sealed class ApplicationIdentityDbContextFactory
        : IDesignTimeDbContextFactory<ApplicationIdentityDbContext>
    {
        public ApplicationIdentityDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DemoMvcDb");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                var projectDatabasePath = Path.Combine(currentDirectory, "DemoMVC", "App.db");
                var currentDirectoryDatabasePath = Path.Combine(currentDirectory, "App.db");

                var databasePath = File.Exists(Path.Combine(currentDirectory, "DemoMVC.csproj"))
                    ? currentDirectoryDatabasePath
                    : projectDatabasePath;

                connectionString = $"Data Source={databasePath}";
            }

            var options = new DbContextOptionsBuilder<ApplicationIdentityDbContext>()
                .UseSqlite(
                    connectionString,
                    sqlite => sqlite.MigrationsHistoryTable("__IdentityMigrationsHistory"))
                .Options;

            return new ApplicationIdentityDbContext(options);
        }
    }
}
