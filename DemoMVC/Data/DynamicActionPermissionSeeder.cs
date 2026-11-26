using DemoMVC.Services;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Data;

public static class DynamicActionPermissionSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var identityContext = scope.ServiceProvider
            .GetRequiredService<ApplicationIdentityDbContext>();
        var permissionTableExists = await identityContext.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'RoleActionPermissions'")
            .SingleAsync() > 0;

        if (!permissionTableExists)
        {
            var identitySchemaReady = await identityContext.Database
                .SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'AspNetRoles'")
                .SingleAsync() > 0;
            if (identitySchemaReady)
            {
                throw new InvalidOperationException(
                    "Dynamic action permissions are not installed. Apply the ApplicationIdentityDbContext migrations before starting the application.");
            }

            logger.LogWarning("Identity tables are not installed; dynamic action permissions were not seeded.");
            return;
        }

        var permissionService = scope.ServiceProvider
            .GetRequiredService<DynamicActionPermissionService>();
        await permissionService.EnsureAllRolePermissionsAsync();
    }
}
