using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models;

public sealed class VerifyEmailOtpViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP phải gồm 6 chữ số.")]
    [Display(Name = "Mã OTP")]
    public string Code { get; set; } = string.Empty;

    public string RequestStatus { get; set; } = string.Empty;
    public DateTimeOffset? CooldownUntil { get; set; }
    public DateTimeOffset? OtpExpiresAt { get; set; }
}

public sealed class AdminEmailVerificationItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Otp { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
