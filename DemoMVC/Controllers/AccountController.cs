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
        private readonly EmailVerificationOtpService _otpService;
        private readonly DynamicActionPermissionService _permissionService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAppEmailSender emailSender,
            EmailVerificationOtpService otpService,
            DynamicActionPermissionService permissionService,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
            _otpService = otpService;
            _permissionService = permissionService;
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

            var emailSent = await TrySendVerificationCodeAsync(user);
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
                try
                {
                    var sent = await TrySendVerificationCodeAsync(user);
                    TempData["VerificationNotice"] = sent
                        ? "Mã OTP đã được gửi đến email. Nhập mã để gửi yêu cầu xác thực cho Admin."
                        : "Chưa gửi được email OTP. Hãy kiểm tra cấu hình SMTP hoặc liên hệ Admin.";
                }
                catch (EmailVerificationCooldownException)
                {
                    TempData["VerificationNotice"] =
                        "Yêu cầu xác thực vừa bị từ chối. Vui lòng đợi hết thời gian đếm ngược trước khi gửi yêu cầu mới.";
                }
                catch (EmailVerificationRequestPendingException)
                {
                    TempData["VerificationNotice"] =
                        "Yêu cầu xác thực của tài khoản đang chờ Admin xử lý.";
                }
            }

            return RedirectToAction(nameof(VerifyEmailOtp), new { email = input.Email.Trim() });
        }

        [Authorize]
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

            var requestState = await _otpService.GetRequestStateAsync(user);
            if (requestState.Status != EmailVerificationRequestStatus.Pending)
            {
                return View(new AccountMessageViewModel
                {
                    Title = "Yêu cầu chưa được duyệt",
                    Message = "Email chỉ được xác thực sau khi Admin đồng ý yêu cầu."
                });
            }

            try
            {
                var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
                if (result.Succeeded)
                {
                    await _otpService.ClearAsync(user);
                }

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
        public async Task<IActionResult> VerifyEmailOtp(string? email)
        {
            return View(await CreateVerificationViewModelAsync(email));
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmailOtp(VerifyEmailOtpViewModel input)
        {
            if (!ModelState.IsValid)
            {
                await PopulateVerificationStateAsync(input);
                return View(input);
            }

            var user = await _userManager.FindByEmailAsync(input.Email.Trim());
            if (user == null || user.EmailConfirmed ||
                !await _otpService.IsValidAsync(user, input.Code))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Email hoặc mã OTP không hợp lệ, hoặc mã đã hết hạn. Hãy yêu cầu gửi mã mới.");
                await PopulateVerificationStateAsync(input);
                return View(input);
            }

            var confirmationToken =
                await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var result = await _userManager.ConfirmEmailAsync(user, confirmationToken);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                await PopulateVerificationStateAsync(input);
                return View(input);
            }

            await _otpService.CompleteVerificationAsync(user);
            return View("ConfirmEmail", new AccountMessageViewModel
            {
                Title = "Xác thực email thành công",
                Message = "Bạn có thể đăng nhập bằng tài khoản vừa xác thực.",
                Succeeded = true
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestAdminEmailVerification(
            ForgotPasswordViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View("VerifyEmailOtp", await CreateVerificationViewModelAsync(input.Email));
            }

            var user = await _userManager.FindByEmailAsync(input.Email.Trim());
            if (user == null || user.EmailConfirmed ||
                !await _userManager.IsInRoleAsync(user, "User"))
            {
                TempData["VerificationNotice"] =
                    "Không tìm thấy tài khoản User chưa xác thực. Hãy kiểm tra email hoặc đăng ký tài khoản.";
                return RedirectToAction(nameof(VerifyEmailOtp), new { email = input.Email.Trim() });
            }

            try
            {
                await _otpService.SubmitRequestAsync(user);
            }
            catch (EmailVerificationCooldownException)
            {
                TempData["VerificationNotice"] =
                    "Yêu cầu trước đã bị từ chối. Hãy đợi hết thời gian đếm ngược để gửi lại.";
            }
            catch (EmailVerificationRequestPendingException)
            {
                TempData["VerificationNotice"] =
                    "Yêu cầu xác thực của bạn đang chờ Admin xử lý.";
            }

            return RedirectToAction(nameof(VerifyEmailOtp), new { email = user.Email });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> VerificationStatus(string? email)
        {
            Response.Headers.CacheControl = "no-store";
            var user = string.IsNullOrWhiteSpace(email)
                ? null
                : await _userManager.FindByEmailAsync(email.Trim());
            if (user == null)
            {
                return Json(new { status = "available", cooldownUntil = (DateTimeOffset?)null });
            }
            if (user.EmailConfirmed)
            {
                return Json(new { status = "confirmed", cooldownUntil = (DateTimeOffset?)null });
            }

            var state = await _otpService.GetRequestStateAsync(user);
            var status = state.Status switch
            {
                EmailVerificationRequestStatus.Pending => "pending",
                EmailVerificationRequestStatus.Rejected when state.CooldownUntil.HasValue =>
                    "cooldown",
                EmailVerificationRequestStatus.Rejected => "available",
                _ => "available"
            };
            return Json(new { status, cooldownUntil = state.CooldownUntil });
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

                var roles = await _userManager.GetRolesAsync(user!);
                if (await _permissionService.IsAllowedForRolesAsync(
                        roles, null, "Student", "Profiles", HttpContext.RequestAborted))
                {
                    return RedirectToAction("Profiles", "Student");
                }

                if (await _permissionService.IsAllowedForRolesAsync(
                        roles, null, "Student", "Register", HttpContext.RequestAborted))
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
                    "Email chưa được Admin xác thực. Hãy gửi yêu cầu xác thực và chờ Admin duyệt.");
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

        private async Task<bool> TrySendVerificationCodeAsync(ApplicationUser user)
        {
            var otp = await _otpService.CreateAsync(user);

            try
            {
                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Mã OTP xác thực tài khoản DemoMVC",
                    $"<p>Mã OTP xác thực tài khoản của bạn là: <strong>{otp}</strong>.</p>" +
                    "<p>Mã có hiệu lực trong 10 phút và chỉ sử dụng được một lần.</p>");
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not send an email confirmation to user {UserId}.", user.Id);
                return false;
            }
        }

        private async Task<VerifyEmailOtpViewModel> CreateVerificationViewModelAsync(string? email)
        {
            var input = new VerifyEmailOtpViewModel { Email = email ?? string.Empty };
            await PopulateVerificationStateAsync(input);
            return input;
        }

        private async Task PopulateVerificationStateAsync(VerifyEmailOtpViewModel input)
        {
            input.RequestStatus = string.Empty;
            input.CooldownUntil = null;
            if (string.IsNullOrWhiteSpace(input.Email))
            {
                return;
            }

            var user = await _userManager.FindByEmailAsync(input.Email.Trim());
            if (user == null)
            {
                return;
            }
            if (user.EmailConfirmed)
            {
                input.RequestStatus = "confirmed";
                return;
            }

            var state = await _otpService.GetRequestStateAsync(user);
            input.RequestStatus = state.Status switch
            {
                EmailVerificationRequestStatus.Pending => "pending",
                EmailVerificationRequestStatus.Rejected when state.CooldownUntil.HasValue =>
                    "cooldown",
                EmailVerificationRequestStatus.Rejected => "available",
                _ => "available"
            };
            input.CooldownUntil = state.CooldownUntil;
            input.OtpExpiresAt = (await _otpService.GetActiveAsync(user))?.ExpiresAt;
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
