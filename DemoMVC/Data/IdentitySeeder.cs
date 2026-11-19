using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger logger)
        {
            using var scope = services.CreateScope();
            var identityContext = scope.ServiceProvider
                .GetRequiredService<ApplicationIdentityDbContext>();

            var identitySchemaReady = await identityContext.Database
                .SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'AspNetRoles'")
                .SingleAsync() > 0;

            if (!identitySchemaReady)
            {
                logger.LogWarning(
                    "Identity tables are not installed. Review and apply the Identity migration before enabling account registration or login.");
                return;
            }

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = new[] { "Admin", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(role));
                    EnsureSucceeded(result, $"creating role '{role}'");
                }
            }

            var adminEmail = configuration["AdminSeed:Email"]?.Trim();
            var adminPassword = configuration["AdminSeed:Password"];

            if (string.IsNullOrWhiteSpace(adminEmail) &&
                string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning(
                    "No initial Admin account was configured. Set AdminSeed__Email and AdminSeed__Password before startup.");
                return;
            }

            if (string.IsNullOrWhiteSpace(adminEmail) ||
                string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException(
                    "Both AdminSeed:Email and AdminSeed:Password must be configured together.");
            }

            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    LockoutEnabled = true
                };

                var createResult = await userManager.CreateAsync(admin, adminPassword);
                EnsureSucceeded(createResult, "creating the initial Admin account");
            }
            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(admin, "Admin");
                EnsureSucceeded(roleResult, "assigning the Admin role to the configured bootstrap account");
            }
        }

        private static void EnsureSucceeded(IdentityResult result, string operation)
        {
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Identity failed while {operation}: {errors}");
            }
        }
    }
}
