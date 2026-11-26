namespace DemoMVC.Models;

public sealed class RoleActionPermission
{
    public string RoleId { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Controller { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
}
