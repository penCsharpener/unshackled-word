using System.Net;
using Microsoft.Extensions.Options;

namespace UnshackledWord.Tooling.WebApi.Auth;

/// <summary>Builds the "you've been invited" email (German/English). No name to greet: the account does not exist yet.</summary>
public sealed class InvitationEmailBuilder
{
    private readonly AppOptions _options;

    public InvitationEmailBuilder(IOptions<AppOptions> options)
    {
        _options = options.Value;
    }

    public EmailMessage Build(string toAddress, string inviteCode, string locale)
    {
        var de = locale == "de";
        var link = $"{_options.FrontendBaseUrl.TrimEnd('/')}/register?invite={Uri.EscapeDataString(inviteCode)}";
        var subject = de ? "Du wurdest zu Unshackled Word eingeladen" : "You have been invited to Unshackled Word";
        var greeting = de ? "Hallo," : "Hello,";
        var intro = de ? "Du wurdest eingeladen, ein Konto anzulegen. Nutze diesen Link:" : "You have been invited to create an account. Use this link:";
        var cta = de ? "Konto anlegen" : "Create account";
        var note = de ? "Falls du diese Einladung nicht erwartet hast, kannst du diese E-Mail ignorieren." : "If you were not expecting this invitation, you can ignore this email.";

        var plain = $"""
            {greeting}

            {intro}
            {link}

            {note}
            """;

        var html = $"""
            <p>{greeting}</p>
            <p>{WebUtility.HtmlEncode(intro)}</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">{cta}</a></p>
            <p>{WebUtility.HtmlEncode(note)}</p>
            """;

        return new EmailMessage(toAddress, subject, plain) { HtmlBody = html };
    }
}
