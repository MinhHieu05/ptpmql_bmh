using System.Text;
using System.Text.Encodings.Web;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace DemoMVC.Controllers
{
    [Route("[controller]/[action]")]
    public sealed class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAppEmailSender _emailSender;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAppEmailSender emailSender,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel input,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var email = input.Email.Trim();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                LockoutEnabled = true
            };

            var result = await _userManager.CreateAsync(user, input.Password);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return View(input);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!roleResult.Succeeded)
            {
                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    _logger.LogError(
                        "Could not remove newly registered account {UserId} after role assignment failed.",
                        user.Id);
                }

                AddIdentityErrors(roleResult);
                return View(input);
            }

            var emailSent = await TrySendConfirmationEmailAsync(user);
            TempData["ConfirmationEmail"] = email;
            TempData["ConfirmationEmailSent"] = emailSent;
            return RedirectToAction(nameof(RegisterConfirmation));
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult RegisterConfirmation()
        {
            return View(new RegisterConfirmationViewModel
            {
                Email = TempData["ConfirmationEmail"] as string,
                EmailSent = TempData["ConfirmationEmailSent"] is bool sent && sent,
                IsResendResult = TempData["ConfirmationIsResend"] is bool isResend && isResend
            });
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ResendConfirmation()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendConfirmation(ForgotPasswordViewModel input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ConfirmationEmail"] = input.Email;
                TempData["ConfirmationIsResend"] = true;
                return RedirectToAction(nameof(RegisterConfirmation));
            }

            var user = await _userManager.FindByEmailAsync(input.Email.Trim());
            if (user != null && !await _userManager.IsEmailConfirmedAsync(user))
            {
                await TrySendConfirmationEmailAsync(user);
            }

            TempData["ConfirmationEmail"] = input.Email.Trim();
            TempData["ConfirmationIsResend"] = true;
            return RedirectToAction(nameof(RegisterConfirmation));
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            {
                return View(new AccountMessageViewModel
                {
                    Title = "Liên kết không hợp lệ",
                    Message = "Liên kết xác thực email không đầy đủ hoặc không hợp lệ."
                });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View(InvalidConfirmationMessage());
            }

            try
            {
                var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
                return View(result.Succeeded
                    ? new AccountMessageViewModel
                    {
                        Title = "Xác thực email thành công",
                        Message = "Bạn có thể đăng nhập bằng tài khoản vừa xác thực.",
                        Succeeded = true
                    }
                    : InvalidConfirmationMessage());
            }
            catch (FormatException)
            {
                return View(InvalidConfirmationMessage());
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel input,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var user = await _userManager.FindByEmailAsync(input.Email.Trim());
            var result = user == null
                ? Microsoft.AspNetCore.Identity.SignInResult.Failed
                : await _signInManager.PasswordSignInAsync(
                    user.UserName!,
                    input.Password,
                    input.RememberMe,
                    lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return Redirect("/Admin");
                }

                var permissions = await _userManager.GetClaimsAsync(user!);
                if (permissions.Any(claim =>
                        claim.Type == StudentProfilePermissions.ClaimType &&
                        claim.Value == StudentProfilePermissions.View))
                {
                    return RedirectToAction("Profiles", "Student");
                }

                if (permissions.Any(claim =>
                        claim.Type == StudentProfilePermissions.ClaimType &&
                        claim.Value == StudentProfilePermissions.Create))
                {
                    return RedirectToAction("Register", "Student");
                }

                return RedirectToAction(nameof(AccessDenied));
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tài khoản đang tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau.");
            }
            else if (result.IsNotAllowed && user != null &&
                     !await _userManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Email chưa được xác thực. Vui lòng kiểm tra hộp thư hoặc gửi lại email xác thực.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            }

            return View(input);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var user = await _userManager.FindByEmailAsync(input.Email.Trim());
            if (user != null && await _userManager.IsEmailConfirmedAsync(user))
            {
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetUrl = Url.ActionLink(
                    nameof(ResetPassword),
                    "Account",
                    new
                    {
                        email = user.Email,
                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code))
                    });

                if (resetUrl == null)
                {
                    throw new InvalidOperationException("Could not generate the password reset URL.");
                }

                try
                {
                    await _emailSender.SendEmailAsync(
                        user.Email!,
                        "Đặt lại mật khẩu DemoMVC",
                        $"<p>Để đặt lại mật khẩu, hãy <a href=\"{HtmlEncoder.Default.Encode(resetUrl)}\">mở liên kết này</a>.</p>");
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Could not send a password reset email to user {UserId}.", user.Id);
                }
            }

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ResetPassword(string? email, string? code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                return View("ResetPasswordConfirmation", new AccountMessageViewModel
                {
                    Title = "Liên kết không hợp lệ",
                    Message = "Liên kết đặt lại mật khẩu không đầy đủ hoặc không hợp lệ."
                });
            }

            return View(new ResetPasswordViewModel { Email = email, Code = code });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var user = await _userManager.FindByEmailAsync(input.Email.Trim());
            if (user == null)
            {
                return View("ResetPasswordConfirmation", ResetPasswordFailedMessage());
            }

            try
            {
                var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Code));
                var result = await _userManager.ResetPasswordAsync(user, decodedCode, input.Password);
                if (result.Succeeded)
                {
                    return View("ResetPasswordConfirmation", new AccountMessageViewModel
                    {
                        Title = "Đặt lại mật khẩu thành công",
                        Message = "Bạn có thể đăng nhập bằng mật khẩu mới.",
                        Succeeded = true
                    });
                }

                AddIdentityErrors(result);
                return View(input);
            }
            catch (FormatException)
            {
                return View("ResetPasswordConfirmation", ResetPasswordFailedMessage());
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            return View(new ManageAccountViewModel
            {
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber
            });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(ManageAccountViewModel input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var phoneResult = await _userManager.SetPhoneNumberAsync(user, input.PhoneNumber);
            if (!phoneResult.Succeeded)
            {
                AddIdentityErrors(phoneResult);
                return View(input);
            }

            var requestedEmail = input.Email.Trim();
            if (!string.Equals(user.Email, requestedEmail, StringComparison.OrdinalIgnoreCase))
            {
                var code = await _userManager.GenerateChangeEmailTokenAsync(user, requestedEmail);
                var confirmUrl = Url.ActionLink(
                    nameof(ConfirmEmailChange),
                    "Account",
                    new
                    {
                        userId = user.Id,
                        email = requestedEmail,
                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code))
                    });

                if (confirmUrl == null)
                {
                    throw new InvalidOperationException("Could not generate the email change URL.");
                }

                try
                {
                    await _emailSender.SendEmailAsync(
                        requestedEmail,
                        "Xác nhận thay đổi email DemoMVC",
                        $"<p>Để xác nhận email mới, hãy <a href=\"{HtmlEncoder.Default.Encode(confirmUrl)}\">mở liên kết này</a>.</p>");
                    TempData["AccountMessage"] =
                        "Thông tin đã được lưu. Hãy mở email mới và xác nhận để hoàn tất thay đổi địa chỉ email.";
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Could not send an email change confirmation to user {UserId}.", user.Id);
                    ModelState.AddModelError(
                        string.Empty,
                        "Không gửi được email xác nhận. Số điện thoại đã được lưu; email chưa thay đổi. Vui lòng kiểm tra cấu hình SMTP hoặc liên hệ quản trị viên.");
                    return View(input);
                }
            }
            else
            {
                TempData["AccountMessage"] = "Thông tin tài khoản đã được cập nhật.";
            }

            await _signInManager.RefreshSignInAsync(user);
            return RedirectToAction(nameof(Manage));
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ConfirmEmailChange(
            string? userId,
            string? email,
            string? code)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(code))
            {
                return View("ConfirmEmail", InvalidConfirmationMessage());
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View("ConfirmEmail", InvalidConfirmationMessage());
            }

            try
            {
                var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                var result = await _userManager.ChangeEmailAsync(user, email, decodedCode);
                if (!result.Succeeded)
                {
                    return View("ConfirmEmail", InvalidConfirmationMessage());
                }

                var usernameResult = await _userManager.SetUserNameAsync(user, email);
                if (!usernameResult.Succeeded)
                {
                    _logger.LogError(
                        "Email changed for user {UserId}, but the username could not be updated.",
                        user.Id);
                    return View("ConfirmEmail", new AccountMessageViewModel
                    {
                        Title = "Email đã xác thực",
                        Message = "Email mới đã được xác nhận. Vui lòng đăng nhập bằng địa chỉ email mới."
                    });
                }

                return View("ConfirmEmail", new AccountMessageViewModel
                {
                    Title = "Thay đổi email thành công",
                    Message = "Email mới đã được xác thực. Hãy đăng nhập lại bằng địa chỉ email mới.",
                    Succeeded = true
                });
            }
            catch (FormatException)
            {
                return View("ConfirmEmail", InvalidConfirmationMessage());
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var result = await _userManager.ChangePasswordAsync(
                user,
                input.CurrentPassword,
                input.NewPassword);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return View(input);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["AccountMessage"] = "Mật khẩu đã được thay đổi thành công.";
            return RedirectToAction(nameof(Manage));
        }

        [Authorize]
        [HttpPost("~/Account/Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            Response.Headers.CacheControl = "no-store, no-cache";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task<bool> TrySendConfirmationEmailAsync(ApplicationUser user)
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationUrl = Url.ActionLink(
                nameof(ConfirmEmail),
                "Account",
                new
                {
                    userId = user.Id,
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code))
                });

            if (confirmationUrl == null)
            {
                throw new InvalidOperationException("Could not generate the email confirmation URL.");
            }

            try
            {
                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Xác thực tài khoản DemoMVC",
                    $"<p>Để xác thực tài khoản, hãy <a href=\"{HtmlEncoder.Default.Encode(confirmationUrl)}\">mở liên kết này</a>.</p>");
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not send an email confirmation to user {UserId}.", user.Id);
                return false;
            }
        }

        private static AccountMessageViewModel InvalidConfirmationMessage()
        {
            return new AccountMessageViewModel
            {
                Title = "Liên kết xác thực không hợp lệ",
                Message = "Liên kết có thể đã hết hạn hoặc đã được sử dụng. Hãy yêu cầu gửi lại email xác thực."
            };
        }

        private static AccountMessageViewModel ResetPasswordFailedMessage()
        {
            return new AccountMessageViewModel
            {
                Title = "Không thể đặt lại mật khẩu",
                Message = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Hãy yêu cầu một liên kết mới."
            };
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
