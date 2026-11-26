using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DemoMVC.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace DemoMVC.Services;

public sealed class EmailVerificationOtpService
{
    private const string LoginProvider = "DemoMVC.EmailVerification";
    private const string OtpTokenName = "Otp";
    private const string ExpirationTokenName = "OtpExpiresAt";
    private const string AttemptsTokenName = "OtpAttempts";
    private const string RequestStatusTokenName = "RequestStatus";
    private const string CooldownTokenName = "RequestCooldownUntil";
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RejectionCooldown = TimeSpan.FromMinutes(1);
    private const int MaximumAttempts = 5;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDataProtector _protector;

    public EmailVerificationOtpService(
        UserManager<ApplicationUser> userManager,
        IDataProtectionProvider dataProtectionProvider)
    {
        _userManager = userManager;
        _protector = dataProtectionProvider.CreateProtector(
            "DemoMVC.EmailVerificationOtp.v1");
    }

    public async Task<string> CreateAsync(ApplicationUser user)
    {
        var state = await GetRequestStateAsync(user);
        if (state.CooldownUntil is { } cooldownUntil &&
            cooldownUntil > DateTimeOffset.UtcNow)
        {
            throw new EmailVerificationCooldownException(cooldownUntil);
        }

        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
        var expiration = DateTimeOffset.UtcNow.Add(OtpLifetime)
            .ToString("O", CultureInfo.InvariantCulture);

        EnsureSucceeded(
            await _userManager.SetAuthenticationTokenAsync(
                user, LoginProvider, OtpTokenName, _protector.Protect(otp)),
            "storing the email verification OTP");
        EnsureSucceeded(
            await _userManager.SetAuthenticationTokenAsync(
                user, LoginProvider, ExpirationTokenName, expiration),
            "storing the email verification OTP expiration");
        EnsureSucceeded(
            await _userManager.SetAuthenticationTokenAsync(
                user, LoginProvider, AttemptsTokenName, "0"),
            "resetting the email verification OTP attempts");

        return otp;
    }

    public async Task<EmailVerificationOtp?> GetActiveAsync(ApplicationUser user)
    {
        var protectedOtp = await _userManager.GetAuthenticationTokenAsync(
            user, LoginProvider, OtpTokenName);
        var expirationValue = await _userManager.GetAuthenticationTokenAsync(
            user, LoginProvider, ExpirationTokenName);

        if (protectedOtp == null ||
            !DateTimeOffset.TryParse(
                expirationValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var expiration) ||
            expiration <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return new EmailVerificationOtp(_protector.Unprotect(protectedOtp), expiration);
    }

    public async Task<bool> IsValidAsync(ApplicationUser user, string otp)
    {
        var state = await GetRequestStateAsync(user);
        if (state.CooldownUntil is { } cooldownUntil &&
            cooldownUntil > DateTimeOffset.UtcNow)
        {
            return false;
        }

        var activeOtp = await GetActiveAsync(user);
        if (activeOtp == null)
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(activeOtp.Code);
        var actual = Encoding.UTF8.GetBytes(otp);
        if (expected.Length == actual.Length &&
            CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return true;
        }

        var attemptsValue = await _userManager.GetAuthenticationTokenAsync(
            user, LoginProvider, AttemptsTokenName);
        if (attemptsValue != null &&
            !int.TryParse(attemptsValue, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            throw new InvalidOperationException("The stored email verification OTP attempt count is invalid.");
        }

        var attempts = attemptsValue == null
            ? 1
            : int.Parse(attemptsValue, NumberStyles.None, CultureInfo.InvariantCulture) + 1;
        if (attempts >= MaximumAttempts)
        {
            await ClearAsync(user);
        }
        else
        {
            EnsureSucceeded(
                await _userManager.SetAuthenticationTokenAsync(
                    user,
                    LoginProvider,
                    AttemptsTokenName,
                    attempts.ToString(CultureInfo.InvariantCulture)),
                "updating the email verification OTP attempt count");
        }

        return false;
    }

    public async Task SubmitRequestAsync(ApplicationUser user)
    {
        var state = await GetRequestStateAsync(user);
        if (state.Status == EmailVerificationRequestStatus.Pending)
        {
            throw new EmailVerificationRequestPendingException();
        }

        if (state.CooldownUntil is { } cooldownUntil &&
            cooldownUntil > DateTimeOffset.UtcNow)
        {
            throw new EmailVerificationCooldownException(cooldownUntil);
        }

        EnsureSucceeded(
            await _userManager.SetAuthenticationTokenAsync(
                user, LoginProvider, RequestStatusTokenName, "Pending"),
            "submitting the email verification request");
    }

    public async Task CompleteVerificationAsync(ApplicationUser user)
    {
        await ClearAsync(user);
        foreach (var tokenName in new[] { RequestStatusTokenName, CooldownTokenName })
        {
            EnsureSucceeded(
                await _userManager.RemoveAuthenticationTokenAsync(
                    user, LoginProvider, tokenName),
                "clearing the email verification request state");
        }
    }

    public async Task RejectRequestAsync(ApplicationUser user)
    {
        var state = await GetRequestStateAsync(user);
        if (state.Status != EmailVerificationRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending email verification request can be rejected.");
        }

        var cooldownUntil = DateTimeOffset.UtcNow.Add(RejectionCooldown)
            .ToString("O", CultureInfo.InvariantCulture);
        EnsureSucceeded(
            await _userManager.SetAuthenticationTokenAsync(
                user, LoginProvider, RequestStatusTokenName, "Rejected"),
            "rejecting the email verification request");
        EnsureSucceeded(
            await _userManager.SetAuthenticationTokenAsync(
                user, LoginProvider, CooldownTokenName, cooldownUntil),
            "storing the email verification request cooldown");
        await ClearAsync(user);
    }

    public async Task<EmailVerificationRequestState> GetRequestStateAsync(
        ApplicationUser user)
    {
        var status = await _userManager.GetAuthenticationTokenAsync(
            user, LoginProvider, RequestStatusTokenName);
        var cooldownValue = await _userManager.GetAuthenticationTokenAsync(
            user, LoginProvider, CooldownTokenName);

        if (status is not (null or "Pending" or "Rejected") ||
            (cooldownValue != null &&
             !DateTimeOffset.TryParse(
                 cooldownValue,
                 CultureInfo.InvariantCulture,
                 DateTimeStyles.None,
                 out _)))
        {
            throw new InvalidOperationException(
                "The stored email verification request state is invalid.");
        }

        DateTimeOffset? cooldownUntil = null;
        if (cooldownValue != null)
        {
            cooldownUntil = DateTimeOffset.Parse(
                cooldownValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);
            if (cooldownUntil <= DateTimeOffset.UtcNow)
            {
                cooldownUntil = null;
            }
        }

        return new EmailVerificationRequestState(
            status switch
            {
                "Pending" => EmailVerificationRequestStatus.Pending,
                "Rejected" => EmailVerificationRequestStatus.Rejected,
                _ => EmailVerificationRequestStatus.None
            },
            cooldownUntil);
    }

    public async Task ClearAsync(ApplicationUser user)
    {
        EnsureSucceeded(
            await _userManager.RemoveAuthenticationTokenAsync(
                user, LoginProvider, OtpTokenName),
            "removing the email verification OTP");
        EnsureSucceeded(
            await _userManager.RemoveAuthenticationTokenAsync(
                user, LoginProvider, ExpirationTokenName),
            "removing the email verification OTP expiration");
        EnsureSucceeded(
            await _userManager.RemoveAuthenticationTokenAsync(
                user, LoginProvider, AttemptsTokenName),
            "removing the email verification OTP attempt count");
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Identity failed while {operation}: {errors}");
        }
    }
}

public sealed record EmailVerificationOtp(string Code, DateTimeOffset ExpiresAt);

public enum EmailVerificationRequestStatus
{
    None,
    Pending,
    Rejected
}

public sealed record EmailVerificationRequestState(
    EmailVerificationRequestStatus Status,
    DateTimeOffset? CooldownUntil);

public sealed class EmailVerificationCooldownException : Exception
{
    public EmailVerificationCooldownException(DateTimeOffset cooldownUntil)
        : base("The email verification request is still in its rejection cooldown.")
    {
        CooldownUntil = cooldownUntil;
    }

    public DateTimeOffset CooldownUntil { get; }
}

public sealed class EmailVerificationRequestPendingException : Exception
{
    public EmailVerificationRequestPendingException()
        : base("An email verification request is already awaiting Admin review.")
    {
    }
}

public sealed class EmailVerificationOtpUnavailableException : Exception
{
    public EmailVerificationOtpUnavailableException()
        : base("An active email verification OTP is required to submit a request.")
    {
    }
}
