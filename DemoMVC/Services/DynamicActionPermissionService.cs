using System.Security.Claims;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Services;

public sealed class DynamicActionPermissionService
{
    private readonly ApplicationIdentityDbContext _identityContext;
    private readonly ActionPermissionCatalog _catalog;

    public DynamicActionPermissionService(
        ApplicationIdentityDbContext identityContext,
        ActionPermissionCatalog catalog)
    {
        _identityContext = identityContext;
        _catalog = catalog;
    }

    public async Task EnsureAllRolePermissionsAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _identityContext.Roles.ToListAsync(cancellationToken);
        foreach (var role in roles)
        {
            await EnsureRolePermissionsAsync(role, cancellationToken);
        }
    }

    public async Task EnsureRolePermissionsAsync(
        IdentityRole role,
        CancellationToken cancellationToken = default)
    {
        var actions = _catalog.GetActions();
        var existing = await _identityContext.RoleActionPermissions
            .Where(permission => permission.RoleId == role.Id)
            .Select(permission => new
            {
                permission.Area,
                permission.Controller,
                permission.Action
            })
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(permission => $"{permission.Area}|{permission.Controller}|{permission.Action}")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var action in actions.Where(action => !existingKeys.Contains(action.Key)))
        {
            _identityContext.RoleActionPermissions.Add(new RoleActionPermission
            {
                RoleId = role.Id,
                Area = action.Area,
                Controller = action.Controller,
                Action = action.Action,
                IsAllowed = GetDefaultPermission(role.Name, action)
            });
        }

        await _identityContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsAllowedAsync(
        ClaimsPrincipal principal,
        string? area,
        string? controller,
        string? action,
        CancellationToken cancellationToken = default)
    {
        var roleNames = principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return await IsAllowedForRolesAsync(roleNames, area, controller, action, cancellationToken);
    }

    public async Task<bool> IsAllowedForRolesAsync(
        IEnumerable<string> roleNames,
        string? area,
        string? controller,
        string? action,
        CancellationToken cancellationToken = default)
    {
        var roles = roleNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (roles.Length == 0)
        {
            return false;
        }

        var roleIds = await _identityContext.Roles
            .Where(role => role.Name != null && roles.Contains(role.Name))
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);
        if (roleIds.Count == 0)
        {
            return false;
        }

        var key = ActionPermissionDescriptor.Create(area, controller ?? string.Empty, action ?? string.Empty);
        return await _identityContext.RoleActionPermissions.AnyAsync(
            permission =>
                roleIds.Contains(permission.RoleId) &&
                permission.Area == key.Area &&
                permission.Controller == key.Controller &&
                permission.Action == key.Action &&
                permission.IsAllowed,
            cancellationToken);
    }

    public static bool GetDefaultPermission(string? roleName, ActionPermissionDescriptor action)
    {
        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(roleName, "User", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action.Area, "ADMIN", StringComparison.Ordinal) ||
            (string.Equals(action.Controller, "ACCOUNT", StringComparison.Ordinal) &&
             string.Equals(action.Action, "CONFIRMEMAIL", StringComparison.Ordinal)))
        {
            return false;
        }

        return !ActionPermissionCatalog.IsDeleteAction(action.Action);
    }
}
