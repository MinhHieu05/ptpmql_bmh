namespace DemoMVC.Models;

public sealed class AdminUserListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool EmailConfirmed { get; init; }
    public bool IsLockedOut { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}
