using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace DemoMVC.Services;

public sealed record ActionPermissionDescriptor(
    string Area,
    string Controller,
    string Action,
    bool IsAnonymous)
{
    public string Key => $"{Area}|{Controller}|{Action}";

    public static ActionPermissionDescriptor Create(
        string? area,
        string controller,
        string action,
        bool isAnonymous = false)
    {
        return new ActionPermissionDescriptor(
            Normalize(area),
            Normalize(controller),
            Normalize(action),
            isAnonymous);
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}

public sealed class ActionPermissionCatalog
{
    private readonly IActionDescriptorCollectionProvider _actionDescriptors;

    public ActionPermissionCatalog(IActionDescriptorCollectionProvider actionDescriptors)
    {
        _actionDescriptors = actionDescriptors;
    }

    public IReadOnlyList<ActionPermissionDescriptor> GetActions()
    {
        return _actionDescriptors.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(descriptor =>
            {
                descriptor.RouteValues.TryGetValue("area", out var area);
                var isAnonymous = descriptor.EndpointMetadata
                    .OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>()
                    .Any();
                return ActionPermissionDescriptor.Create(
                    area,
                    descriptor.ControllerName,
                    descriptor.ActionName,
                    isAnonymous);
            })
            .DistinctBy(action => action.Key, StringComparer.Ordinal)
            .OrderBy(action => action.Area, StringComparer.Ordinal)
            .ThenBy(action => action.Controller, StringComparer.Ordinal)
            .ThenBy(action => action.Action, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool IsDeleteAction(string actionName)
    {
        return actionName.StartsWith("DELETE", StringComparison.Ordinal) ||
               actionName.StartsWith("REMOVE", StringComparison.Ordinal) ||
               actionName.StartsWith("DESTROY", StringComparison.Ordinal);
    }

    public static bool IsCreateAction(string actionName)
    {
        return actionName.StartsWith("CREATE", StringComparison.Ordinal) ||
               actionName.StartsWith("ADD", StringComparison.Ordinal) ||
               actionName.StartsWith("REGISTER", StringComparison.Ordinal) ||
               actionName.StartsWith("SUBMIT", StringComparison.Ordinal);
    }

    public static bool IsUpdateAction(string actionName)
    {
        return actionName.StartsWith("EDIT", StringComparison.Ordinal) ||
               actionName.StartsWith("UPDATE", StringComparison.Ordinal) ||
               actionName.StartsWith("CHANGE", StringComparison.Ordinal) ||
               actionName.StartsWith("SAVE", StringComparison.Ordinal) ||
               actionName.StartsWith("SET", StringComparison.Ordinal);
    }
}
