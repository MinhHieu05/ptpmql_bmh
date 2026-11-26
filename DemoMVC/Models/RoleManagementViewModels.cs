namespace DemoMVC.Models;

public sealed class RoleListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int UserCount { get; init; }
    public bool IsBuiltIn { get; init; }
}

public sealed class RolePermissionsViewModel
{
    public string RoleId { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public IReadOnlyList<RoleActionPermissionViewModel> Actions { get; init; } =
        Array.Empty<RoleActionPermissionViewModel>();
}

public sealed class RoleActionPermissionViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Area { get; init; } = string.Empty;
    public string Controller { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public bool IsAllowed { get; init; }
    public bool IsAnonymous { get; init; }
}
