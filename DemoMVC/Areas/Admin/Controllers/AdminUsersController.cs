using System.Security.Claims;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("Admin/Users")]
public sealed class AdminUsersController : Controller
{
    private static readonly string[] ManagedRoles = ["Admin", "User"];
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationIdentityDbContext _identityContext;

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        ApplicationIdentityDbContext identityContext)
    {
        _userManager = userManager;
        _identityContext = identityContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync();
        var items = new List<AdminUserListItemViewModel>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            bool HasPermission(string value) => claims.Any(claim =>
                claim.Type == StudentProfilePermissions.ClaimType &&
                claim.Value == value);

            items.Add(new AdminUserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                IsLockedOut = await _userManager.IsLockedOutAsync(user),
                Role = roles.FirstOrDefault(role => ManagedRoles.Contains(role)) ?? "User",
                CanViewStudentProfiles = HasPermission(StudentProfilePermissions.View),
                CanCreateStudentProfiles = HasPermission(StudentProfilePermissions.Create),
                CanEditStudentProfiles = HasPermission(StudentProfilePermissions.Edit),
                CanDeleteStudentProfiles = HasPermission(StudentProfilePermissions.Delete)
            });
        }

        return View("~/Areas/Admin/Views/AdminUsers/Index.cshtml", items);
    }

    [HttpPost("{id}/role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, string role)
    {
        if (!ManagedRoles.Contains(role, StringComparer.Ordinal))
        {
            return BadRequest();
        }

        var target = await _userManager.FindByIdAsync(id);
        if (target == null)
        {
            return NotFound();
        }

        if (string.Equals(target.Id, _userManager.GetUserId(User), StringComparison.Ordinal))
        {
            TempData["AdminUsersError"] = "Không thể tự thay đổi vai trò của tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(Index));
        }

        var currentRoles = await _userManager.GetRolesAsync(target);
        if (currentRoles.Contains("Admin", StringComparer.Ordinal) &&
            !string.Equals(role, "Admin", StringComparison.Ordinal))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count <= 1)
            {
                TempData["AdminUsersError"] = "Không thể hạ quyền Admin cuối cùng của hệ thống.";
                return RedirectToAction(nameof(Index));
            }
        }

        var rolesToRemove = currentRoles.Where(ManagedRoles.Contains).ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(target, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                TempData["AdminUsersError"] = "Không thể cập nhật vai trò: " +
                    string.Join(" ", removeResult.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Index));
            }
        }

        var addResult = await _userManager.AddToRoleAsync(target, role);
        if (!addResult.Succeeded && rolesToRemove.Length > 0)
        {
            var rollbackResult = await _userManager.AddToRolesAsync(target, rolesToRemove);
            if (!rollbackResult.Succeeded)
            {
                TempData["AdminUsersError"] =
                    "Không cập nhật được vai trò và không khôi phục được vai trò cũ. Hãy kiểm tra tài khoản ngay.";
                return RedirectToAction(nameof(Index));
            }
        }

        TempData[addResult.Succeeded ? "AdminUsersMessage" : "AdminUsersError"] =
            addResult.Succeeded
                ? "Vai trò tài khoản đã được cập nhật."
                : "Không thể cập nhật vai trò: " +
                  string.Join(" ", addResult.Errors.Select(error => error.Description));

        if (addResult.Succeeded)
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(target);
            if (!stampResult.Succeeded)
            {
                TempData["AdminUsersError"] =
                    "Vai trò đã đổi nhưng không thể thu hồi phiên đăng nhập hiện tại của tài khoản.";
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id}/student-permissions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveStudentPermissions(
        string id,
        AdminStudentPermissionsViewModel input)
    {
        var target = await _userManager.FindByIdAsync(id);
        if (target == null)
        {
            return NotFound();
        }

        if (!await _userManager.IsInRoleAsync(target, "User"))
        {
            TempData["AdminUsersError"] =
                "Chỉ có thể cấu hình quyền hồ sơ sinh viên cho tài khoản có vai trò User.";
            return RedirectToAction(nameof(Index));
        }

        var desiredValues = new List<string>();
        if (input.CanView)
        {
            desiredValues.Add(StudentProfilePermissions.View);
        }
        if (input.CanCreate)
        {
            desiredValues.Add(StudentProfilePermissions.Create);
        }
        if (input.CanEdit)
        {
            desiredValues.Add(StudentProfilePermissions.Edit);
        }
        if (input.CanDelete)
        {
            desiredValues.Add(StudentProfilePermissions.Delete);
        }

        await using var transaction = await _identityContext.Database.BeginTransactionAsync();
        var existingClaims = await _userManager.GetClaimsAsync(target);
        var permissionValues = new[]
        {
            StudentProfilePermissions.View,
            StudentProfilePermissions.Create,
            StudentProfilePermissions.Edit,
            StudentProfilePermissions.Delete
        };
        var managedClaims = existingClaims
            .Where(claim =>
                claim.Type == StudentProfilePermissions.ClaimType &&
                permissionValues.Contains(claim.Value, StringComparer.Ordinal))
            .ToArray();
        var claimsToRemove = managedClaims
            .Where(claim => !desiredValues.Contains(claim.Value, StringComparer.Ordinal))
            .ToArray();
        var currentValues = managedClaims
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
        var claimsToAdd = desiredValues
            .Where(value => !currentValues.Contains(value))
            .Select(value => new Claim(StudentProfilePermissions.ClaimType, value))
            .ToArray();

        if (claimsToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveClaimsAsync(target, claimsToRemove);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync();
                TempData["AdminUsersError"] = "Không thể thu hồi quyền: " +
                    string.Join(" ", removeResult.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Index));
            }
        }

        if (claimsToAdd.Length > 0)
        {
            var addResult = await _userManager.AddClaimsAsync(target, claimsToAdd);
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync();
                TempData["AdminUsersError"] = "Không thể cấp quyền: " +
                    string.Join(" ", addResult.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Index));
            }
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(target);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync();
            TempData["AdminUsersError"] = "Không thể cập nhật phiên đăng nhập sau khi đổi quyền.";
            return RedirectToAction(nameof(Index));
        }

        await transaction.CommitAsync();
        TempData["AdminUsersMessage"] =
            "Đã lưu quyền hồ sơ sinh viên. Phiên đăng nhập cũ của User sẽ bị thu hồi.";
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
