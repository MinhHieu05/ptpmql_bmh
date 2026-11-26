using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/Roles")]
public sealed class AdminRolesController : Controller
{
    private static readonly HashSet<string> BuiltInRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Admin", "User" };

    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationIdentityDbContext _identityContext;
    private readonly ActionPermissionCatalog _actionCatalog;
    private readonly DynamicActionPermissionService _permissionService;

    public AdminRolesController(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationIdentityDbContext identityContext,
        ActionPermissionCatalog actionCatalog,
        DynamicActionPermissionService permissionService)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _identityContext = identityContext;
        _actionCatalog = actionCatalog;
        _permissionService = permissionService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var roles = await _roleManager.Roles
            .OrderBy(role => role.Name)
            .ToListAsync();
        var items = new List<RoleListItemViewModel>(roles.Count);

        foreach (var role in roles)
        {
            items.Add(new RoleListItemViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                UserCount = (await _userManager.GetUsersInRoleAsync(role.Name!)).Count,
                IsBuiltIn = BuiltInRoles.Contains(role.Name ?? string.Empty)
            });
        }

        return View("~/Areas/Admin/Views/AdminRoles/Index.cshtml", items);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string roleName)
    {
        var name = roleName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
        {
            TempData["AdminRolesError"] = "Tên Role phải có từ 1 đến 256 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        if (await _roleManager.RoleExistsAsync(name))
        {
            TempData["AdminRolesError"] = "Role này đã tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        var role = new IdentityRole(name);
        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            TempData["AdminRolesError"] = "Không thể tạo Role: " +
                string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        await _permissionService.EnsureRolePermissionsAsync(role);
        TempData["AdminRolesMessage"] =
            $"Đã tạo Role {name}. Mặc định Role chưa có quyền; hãy cấu hình quyền theo Action.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(string id, string roleName)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null)
        {
            return NotFound();
        }

        if (BuiltInRoles.Contains(role.Name ?? string.Empty))
        {
            TempData["AdminRolesError"] = "Không thể đổi tên Role mặc định Admin hoặc User.";
            return RedirectToAction(nameof(Index));
        }

        var name = roleName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
        {
            TempData["AdminRolesError"] = "Tên Role phải có từ 1 đến 256 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            TempData["AdminRolesMessage"] = "Tên Role không thay đổi.";
            return RedirectToAction(nameof(Index));
        }

        if (await _roleManager.RoleExistsAsync(name))
        {
            TempData["AdminRolesError"] = "Role này đã tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        var assignedUsers = await _userManager.GetUsersInRoleAsync(role.Name!);
        var oldName = role.Name;
        role.Name = name;
        await using var transaction = await _identityContext.Database.BeginTransactionAsync();
        var updateResult = await _roleManager.UpdateAsync(role);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["AdminRolesError"] = "Không thể đổi tên Role: " +
                string.Join(" ", updateResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        foreach (var user in assignedUsers)
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                await transaction.RollbackAsync();
                TempData["AdminRolesError"] =
                    $"Không thể cập nhật phiên đăng nhập của {user.Email}; Role chưa được đổi tên.";
                return RedirectToAction(nameof(Index));
            }
        }

        await transaction.CommitAsync();
        TempData["AdminRolesMessage"] = $"Đã đổi tên Role {oldName} thành {name}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> Permissions(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null)
        {
            return NotFound();
        }

        await _permissionService.EnsureRolePermissionsAsync(role);
        var savedPermissions = await _identityContext.RoleActionPermissions
            .Where(permission => permission.RoleId == role.Id)
            .ToDictionaryAsync(
                permission => $"{permission.Area}|{permission.Controller}|{permission.Action}",
                permission => permission.IsAllowed,
                StringComparer.Ordinal);

        var model = new RolePermissionsViewModel
        {
            RoleId = role.Id,
            RoleName = role.Name ?? string.Empty,
            Actions = _actionCatalog.GetActions()
                .Select(action => new RoleActionPermissionViewModel
                {
                    Key = action.Key,
                    Area = action.Area,
                    Controller = action.Controller,
                    Action = action.Action,
                    IsAnonymous = action.IsAnonymous,
                    IsAllowed = savedPermissions.TryGetValue(action.Key, out var allowed) && allowed
                })
                .ToArray()
        };

        return View("~/Areas/Admin/Views/AdminRoles/Permissions.cshtml", model);
    }

    [HttpPost("{id}/permissions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePermissions(string id, string[]? allowedActionKeys)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null)
        {
            return NotFound();
        }

        await _permissionService.EnsureRolePermissionsAsync(role);
        var catalogActions = _actionCatalog.GetActions();
        var allowedKeys = (allowedActionKeys ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var permissions = await _identityContext.RoleActionPermissions
            .Where(permission => permission.RoleId == role.Id)
            .ToDictionaryAsync(
                permission => $"{permission.Area}|{permission.Controller}|{permission.Action}",
                StringComparer.Ordinal);

        foreach (var action in catalogActions)
        {
            var permission = permissions[action.Key];
            permission.IsAllowed = !action.IsAnonymous && allowedKeys.Contains(action.Key);
        }

        await _identityContext.SaveChangesAsync();
        TempData["AdminRolesMessage"] = $"Đã cập nhật quyền cho Role {role.Name}.";
        return RedirectToAction(nameof(Permissions), new { id });
    }

    [HttpPost("{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null)
        {
            return NotFound();
        }

        if (BuiltInRoles.Contains(role.Name ?? string.Empty))
        {
            TempData["AdminRolesError"] = "Không thể xóa Role mặc định Admin hoặc User.";
            return RedirectToAction(nameof(Index));
        }

        var users = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (users.Count > 0)
        {
            TempData["AdminRolesError"] =
                "Hãy thu hồi Role này khỏi tất cả tài khoản trước khi xóa.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _roleManager.DeleteAsync(role);
        TempData[result.Succeeded ? "AdminRolesMessage" : "AdminRolesError"] =
            result.Succeeded
                ? $"Đã xóa Role {role.Name}."
                : "Không thể xóa Role: " +
                  string.Join(" ", result.Errors.Select(error => error.Description));
        return RedirectToAction(nameof(Index));
    }
}
