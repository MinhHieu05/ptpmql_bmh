using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
[Route("Admin/Users")]
public sealed class AdminUsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationIdentityDbContext _identityContext;

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationIdentityDbContext identityContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _identityContext = identityContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync();
        var availableRoles = await _roleManager.Roles
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync();
        var items = new List<AdminUserListItemViewModel>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new AdminUserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                IsLockedOut = await _userManager.IsLockedOutAsync(user),
                Roles = roles.OrderBy(role => role).ToArray()
            });
        }

        ViewBag.AvailableRoles = availableRoles;
        return View("~/Areas/Admin/Views/AdminUsers/Index.cshtml", items);
    }

    [HttpPost("{id}/roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRoles(string id, string[]? roles)
    {
        var target = await _userManager.FindByIdAsync(id);
        if (target == null)
        {
            return NotFound();
        }

        var desiredRoles = (roles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var availableRoleNames = await _roleManager.Roles
            .Select(role => role.Name!)
            .ToListAsync();
        if (desiredRoles.Any(role => !availableRoleNames.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            TempData["AdminUsersError"] = "Một hoặc nhiều vai trò được chọn không còn tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(target.Id, _userManager.GetUserId(User), StringComparison.Ordinal))
        {
            TempData["AdminUsersError"] = "Không thể tự thay đổi vai trò của tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(Index));
        }

        var currentRoles = await _userManager.GetRolesAsync(target);
        var losesAdmin = currentRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase) &&
            !desiredRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        if (losesAdmin)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count <= 1)
            {
                TempData["AdminUsersError"] = "Không thể hạ quyền Admin cuối cùng của hệ thống.";
                return RedirectToAction(nameof(Index));
            }
        }

        var rolesToRemove = currentRoles
            .Where(role => !desiredRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var rolesToAdd = desiredRoles
            .Where(role => !currentRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        await using var transaction = await _identityContext.Database.BeginTransactionAsync();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(target, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync();
                TempData["AdminUsersError"] = "Không thể cập nhật vai trò: " +
                    string.Join(" ", removeResult.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Index));
            }
        }

        IdentityResult addResult = IdentityResult.Success;
        if (rolesToAdd.Length > 0)
        {
            addResult = await _userManager.AddToRolesAsync(target, rolesToAdd);
        }

        if (!addResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["AdminUsersError"] = "Không thể cập nhật vai trò: " +
                string.Join(" ", addResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(target);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["AdminUsersError"] =
                "Không thể cập nhật phiên đăng nhập sau khi đổi vai trò.";
            return RedirectToAction(nameof(Index));
        }

        await transaction.CommitAsync();
        TempData["AdminUsersMessage"] = "Vai trò tài khoản đã được cập nhật.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id}/lockout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLockout(string id, bool locked)
    {
        var target = await _userManager.FindByIdAsync(id);
        if (target == null)
        {
            return NotFound();
        }

        if (string.Equals(target.Id, _userManager.GetUserId(User), StringComparison.Ordinal))
        {
            TempData["AdminUsersError"] = "Không thể khóa tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(Index));
        }

        if (locked && await _userManager.IsInRoleAsync(target, "Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var otherAvailableAdmins = 0;
            foreach (var admin in admins.Where(admin => admin.Id != target.Id))
            {
                if (!await _userManager.IsLockedOutAsync(admin))
                {
                    otherAvailableAdmins++;
                }
            }

            if (otherAvailableAdmins == 0)
            {
                TempData["AdminUsersError"] = "Không thể khóa Admin cuối cùng có thể truy cập hệ thống.";
                return RedirectToAction(nameof(Index));
            }
        }

        var enableLockoutResult = await _userManager.SetLockoutEnabledAsync(target, true);
        if (!enableLockoutResult.Succeeded)
        {
            TempData["AdminUsersError"] = "Không thể cập nhật trạng thái khóa tài khoản.";
            return RedirectToAction(nameof(Index));
        }

        var lockoutResult = await _userManager.SetLockoutEndDateAsync(
            target,
            locked ? DateTimeOffset.UtcNow.AddYears(100) : null);
        if (lockoutResult.Succeeded)
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(target);
            if (!stampResult.Succeeded)
            {
                TempData["AdminUsersError"] =
                    "Trạng thái khóa đã đổi nhưng không thể thu hồi phiên đăng nhập hiện tại của tài khoản.";
                return RedirectToAction(nameof(Index));
            }
        }

        TempData[lockoutResult.Succeeded ? "AdminUsersMessage" : "AdminUsersError"] =
            lockoutResult.Succeeded
                ? locked ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản."
                : "Không thể cập nhật trạng thái khóa tài khoản.";

        return RedirectToAction(nameof(Index));
    }
}
