using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using UnshackledWord.Tooling.BibleTagger.Features.Configuration;

namespace UnshackledWord.Tooling.BibleTagger.Features.Email;

public sealed class EmailService : IEmailService
{
    private readonly MailKitOptions _options;

    public EmailService(IOptions<AppSettings> options)
    {
        _options = options.Value.MailKitOptions;
    }

    public async Task SendAsync(string username, string userEmail, string subject, string body, bool isHtml = false, CancellationToken token = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.SenderName, _options.SenderEmail));
        message.To.Add(new MailboxAddress(username, userEmail));
        message.Subject = subject;

        message.Body = new TextPart(isHtml ? TextFormat.Html : TextFormat.Plain)
        {
            Text = body
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Server, _options.Port, _options.Security, token);
        // Note: only needed if the SMTP server requires authentication
        if (_options.Security)
        {
            await client.AuthenticateAsync(_options.Account, _options.Password, token);
        }
        await client.SendAsync(message, token);
        await client.DisconnectAsync(true, token);
    }
}

public interface IEmailService
{
    Task SendAsync(string username, string userEmail, string subject, string body, bool isHtml = false,
        CancellationToken token = default);
}
