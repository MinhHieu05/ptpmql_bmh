namespace DemoMVC.Services;

public interface IAppEmailSender
{
    Task SendEmailAsync(
        string recipient,
        string subject,
        string htmlMessage,
        CancellationToken cancellationToken = default);
}
