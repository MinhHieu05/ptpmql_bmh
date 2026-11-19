using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DemoMVC.Services;

public sealed class SmtpEmailSender : IAppEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendEmailAsync(
        string recipient,
        string subject,
        string htmlMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException(
                "SMTP is not configured. Set Email:Host and Email:FromAddress.");
        }

        if ((_options.UserName.Length == 0) != (_options.Password.Length == 0))
        {
            throw new InvalidOperationException(
                "Configure both Email:UserName and Email:Password, or leave both empty.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlMessage }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.Security,
            cancellationToken);

        if (_options.UserName.Length > 0)
        {
            await client.AuthenticateAsync(
                _options.UserName,
                _options.Password,
                cancellationToken);
        }

        await client.SendAsync(new SmtpSendRequest(message), cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
