using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.ViewComponents;

public sealed class EmailVerificationNotificationViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmailVerificationOtpService _otpService;

    public EmailVerificationNotificationViewComponent(
        UserManager<ApplicationUser> userManager,
        EmailVerificationOtpService otpService)
    {
        _userManager = userManager;
        _otpService = otpService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var users = await _userManager.GetUsersInRoleAsync("User");
        var pendingCount = 0;
        foreach (var user in users.Where(user => !user.EmailConfirmed))
        {
            var state = await _otpService.GetRequestStateAsync(user);
            if (state.Status == EmailVerificationRequestStatus.Pending)
            {
                pendingCount++;
            }
        }

        return View(pendingCount);
    }
}
