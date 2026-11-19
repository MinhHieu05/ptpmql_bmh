namespace DemoMVC.Models;

public sealed class AdminUserListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool EmailConfirmed { get; init; }
    public bool IsLockedOut { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool CanViewStudentProfiles { get; init; }
    public bool CanCreateStudentProfiles { get; init; }
    public bool CanEditStudentProfiles { get; init; }
    public bool CanDeleteStudentProfiles { get; init; }
}

public sealed class AdminStudentPermissionsViewModel
{
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
