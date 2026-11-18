using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace UnshackledWord.Tooling.WebApi.Auth;

/// <summary>A fully-formed email, independent of the SMTP library.</summary>
public sealed record EmailMessage(string ToAddress, string Subject, string PlainTextBody)
{
    public string? ToName { get; init; }
    public string? HtmlBody { get; init; }
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>
/// The only class touching MailKit/MimeKit. Points at Mailpit in dev and a real provider elsewhere,
/// purely via configuration (<see cref="EmailOptions.Security"/> / <see cref="EmailOptions.Username"/>).
/// </summary>
public sealed class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<EmailOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var mimeMessage = ToMimeMessage(message);

        try
        {
            using var client = new SmtpClient
            {
                Timeout = (int)TimeSpan.FromSeconds(_options.TimeoutSeconds).TotalMilliseconds,
            };

            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, ResolveSecurity(), ct);

            if (!string.IsNullOrEmpty(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            }

            var sendRequest = new SmtpSendRequest(mimeMessage);
            await client.SendAsync(sendRequest, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            // Never log the body: it carries single-use tokens.
            _logger.LogError(ex, "Failed to send email to {Recipient}", message.ToAddress);
            throw;
        }
    }

    // An unrecognised value throws instead of falling back to None: a typo must not downgrade to plaintext.
    private SecureSocketOptions ResolveSecurity() =>
        Enum.TryParse<SecureSocketOptions>(_options.Security, ignoreCase: true, out var security)
            ? security
            : throw new InvalidOperationException(
                $"Email:Security has an unrecognised value '{_options.Security}'. "
                + $"Expected one of: {string.Join(", ", Enum.GetNames<SecureSocketOptions>())}.");

    private MimeMessage ToMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mimeMessage.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.ToAddress));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new BodyBuilder
        {
            TextBody = message.PlainTextBody,
            HtmlBody = message.HtmlBody,
        }.ToMessageBody();

        return mimeMessage;
    }
}

/// <summary>Builds the three transactional emails (German and English).</summary>
public sealed class AuthEmailBuilder
{
    private readonly AppOptions _options;

    public AuthEmailBuilder(IOptions<AppOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>"de" when the Accept-Language header prefers German, else "en".</summary>
    public static string ResolveLocale(HttpContext? context)
    {
        var header = context?.Request.Headers.AcceptLanguage.ToString();
        return header is not null && header.TrimStart().StartsWith("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
    }

    public EmailMessage BuildVerification(string toAddress, string toName, string rawToken, string locale) =>
        Build(toAddress, toName, "verify-email", rawToken, locale,
            en: ("Confirm your email address", "Please confirm your email address to finish creating your account:", "Confirm email", "If you did not create an account, you can ignore this email."),
            de: ("Bestätige deine E-Mail-Adresse", "Bitte bestätige deine E-Mail-Adresse, um dein Konto fertig einzurichten:", "E-Mail bestätigen", "Falls du kein Konto angelegt hast, kannst du diese E-Mail ignorieren."));

    public EmailMessage BuildPasswordReset(string toAddress, string toName, string rawToken, string locale) =>
        Build(toAddress, toName, "reset-password", rawToken, locale,
            en: ("Reset your password", "Use this link to choose a new password (valid for 1 hour; only the newest link is valid):", "Reset password", "If you did not request this, you can ignore this email."),
            de: ("Passwort zurücksetzen", "Mit diesem Link legst du ein neues Passwort fest (1 Stunde gültig, nur der neueste Link ist gültig):", "Passwort zurücksetzen", "Falls du das nicht angefordert hast, kannst du diese E-Mail ignorieren."));

    public EmailMessage BuildMagicLink(string toAddress, string toName, string rawToken, string locale) =>
        Build(toAddress, toName, "magic-link-login", rawToken, locale,
            en: ("Your sign-in link", "Use this link to sign in (valid for 15 minutes):", "Sign in", "If you did not request this, you can ignore this email."),
            de: ("Dein Anmeldelink", "Mit diesem Link meldest du dich an (15 Minuten gültig):", "Anmelden", "Falls du das nicht angefordert hast, kannst du diese E-Mail ignorieren."));

    /// <summary>
    /// Welcome mail of an account an admin created (no password yet); the link opens the reset page in welcome mode.
    /// The token is a <c>password_reset</c> token with the longer <see cref="WelcomeLifetime"/>.
    /// </summary>
    public EmailMessage BuildWelcome(string toAddress, string toName, string rawToken, string locale) =>
        Build(toAddress, toName, "reset-password", rawToken, locale,
            en: ("Welcome to Unshackled Word \u2013 set your password", "An account has been created for you. Use this link to set your password (valid for 72 hours; only the newest link is valid):", "Set password", "If you did not expect this, you can ignore this email."),
            de: ("Willkommen bei Unshackled Word \u2013 Passwort festlegen", "F\u00fcr dich wurde ein Konto angelegt. Mit diesem Link legst du dein Passwort fest (72 Stunden g\u00fcltig, nur der neueste Link ist g\u00fcltig):", "Passwort festlegen", "Falls du das nicht erwartet hast, kannst du diese E-Mail ignorieren."),
            extraQuery: "&welcome=1");

    /// <summary>Notice that an admin changed the account's email; sent to the old address (<paramref name="toNewAddress"/> false) and the new one.</summary>
    public EmailMessage BuildEmailChangedNotice(string toAddress, string toName, string oldEmail, string newEmail, bool toNewAddress, string locale)
    {
        var de = locale == "de";
        var subject = de ? "Deine E-Mail-Adresse wurde ge\u00e4ndert" : "Your email address was changed";
        var greeting = de ? $"Hallo {toName}," : $"Hello {toName},";
        var text = toNewAddress
            ? (de ? $"Dein Konto verwendet jetzt diese Adresse ({newEmail}). Bisher war es {oldEmail}." : $"Your account now uses this address ({newEmail}). It used to be {oldEmail}.")
            : (de ? $"Deine E-Mail-Adresse wurde von einem Administrator ge\u00e4ndert auf {newEmail}." : $"An administrator changed your email address to {newEmail}.");
        var note = de ? "Melde dich ab jetzt mit der neuen Adresse an. Bei Fragen wende dich an den Administrator." : "Sign in with the new address from now on. If you have questions, contact the administrator.";

        var plainTextBody = $"""
            {greeting}

            {text}

            {note}
            """;

        var htmlBody = $"""
            <p>{System.Net.WebUtility.HtmlEncode(greeting)}</p>
            <p>{System.Net.WebUtility.HtmlEncode(text)}</p>
            <p>{note}</p>
            """;

        return new EmailMessage(toAddress, subject, plainTextBody) { ToName = toName, HtmlBody = htmlBody };
    }

    /// <summary>Lifetime of the welcome link (normal reset links live 1 hour).</summary>
    public static readonly TimeSpan WelcomeLifetime = TimeSpan.FromHours(72);

    private EmailMessage Build(
        string toAddress,
        string toName,
        string path,
        string rawToken,
        string locale,
        (string Subject, string Intro, string Cta, string Note) en,
        (string Subject, string Intro, string Cta, string Note) de,
        string extraQuery = "")
    {
        var text = locale == "de" ? de : en;
        var greeting = locale == "de" ? $"Hallo {toName}," : $"Hello {toName},";
        var link = $"{_options.FrontendBaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(rawToken)}{extraQuery}";
        var encodedName = System.Net.WebUtility.HtmlEncode(toName);
        var greetingHtml = locale == "de" ? $"Hallo {encodedName}," : $"Hello {encodedName},";

        var plainTextBody = $"""
            {greeting}

            {text.Intro}
            {link}

            {text.Note}
            """;

        var htmlBody = $"""
            <p>{greetingHtml}</p>
            <p>{text.Intro}</p>
            <p><a href="{link}">{text.Cta}</a></p>
            <p>{text.Note}</p>
            """;

        return new EmailMessage(toAddress, text.Subject, plainTextBody)
        {
            ToName = toName,
            HtmlBody = htmlBody,
        };
    }
}
