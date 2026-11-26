using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
[Route("Admin/EmailVerification")]
public sealed class AdminEmailVerificationController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmailVerificationOtpService _otpService;

    public AdminEmailVerificationController(
        UserManager<ApplicationUser> userManager,
        EmailVerificationOtpService otpService)
    {
        _userManager = userManager;
        _otpService = otpService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.GetUsersInRoleAsync("User");
        var items = new List<AdminEmailVerificationItemViewModel>();

        foreach (var user in users.Where(user => !user.EmailConfirmed))
        {
            var state = await _otpService.GetRequestStateAsync(user);
            if (state.Status != EmailVerificationRequestStatus.Pending)
            {
                continue;
            }

            var otp = await _otpService.GetActiveAsync(user);
            items.Add(new AdminEmailVerificationItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Otp = otp?.Code,
                ExpiresAt = otp?.ExpiresAt
            });
        }

        items = items.OrderBy(item => item.Email).ToList();
        return View("~/Areas/Admin/Views/AdminEmailVerification/Index.cshtml", items);
    }

    [HttpPost("{id}/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var state = await _otpService.GetRequestStateAsync(user);
        if (user.EmailConfirmed ||
            !await _userManager.IsInRoleAsync(user, "User") ||
            state.Status != EmailVerificationRequestStatus.Pending)
        {
            TempData["EmailVerificationError"] =
                "Chỉ có thể duyệt yêu cầu xác thực email đang chờ của tài khoản User.";
            return RedirectToAction(nameof(Index));
        }

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await _userManager.ConfirmEmailAsync(user, code);
        if (!result.Succeeded)
        {
            TempData["EmailVerificationError"] =
                "Không thể xác thực email: " +
                string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        await _otpService.CompleteVerificationAsync(user);
        TempData["EmailVerificationMessage"] = $"Đã xác thực email {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (user.EmailConfirmed ||
            !await _userManager.IsInRoleAsync(user, "User"))
        {
            TempData["EmailVerificationError"] =
                "Chỉ có thể từ chối yêu cầu của tài khoản User chưa xác thực.";
            return RedirectToAction(nameof(Index));
        }

        var state = await _otpService.GetRequestStateAsync(user);
        if (state.Status != EmailVerificationRequestStatus.Pending)
        {
            TempData["EmailVerificationError"] =
                "Yêu cầu xác thực không còn ở trạng thái chờ duyệt.";
            return RedirectToAction(nameof(Index));
        }

        await _otpService.RejectRequestAsync(user);
        TempData["EmailVerificationMessage"] =
            $"Đã từ chối yêu cầu của {user.Email}. User có thể yêu cầu lại sau 1 phút.";
        return RedirectToAction(nameof(Index));
    }
}
