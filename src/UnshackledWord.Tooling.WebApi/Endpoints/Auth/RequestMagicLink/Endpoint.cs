using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.RequestMagicLink;

public sealed class Endpoint : Endpoint<EmailRequest>
{
    // Shorter than a password reset: a magic link proves email ownership and signs the holder straight in.
    private static readonly TimeSpan MagicLinkLifetime = TimeSpan.FromMinutes(15);

    private readonly IAuthRepository _repository;
    private readonly AuthEmailBuilder _emailBuilder;
    private readonly IEmailSender _emailSender;

    public Endpoint(IAuthRepository repository, AuthEmailBuilder emailBuilder, IEmailSender emailSender)
    {
        _repository = repository;
        _emailBuilder = emailBuilder;
        _emailSender = emailSender;
    }

    public override void Configure()
    {
        Post("request-magic-link");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Validator<RequestMagicLinkValidator>(); // the request type is shared with another endpoint
        Throttle(hitLimit: 5, durationSeconds: 60, headerName: null);
        Description(b => b.Produces(202));
        Summary(s => s.Summary = "Emails a sign-in link if the address is registered. Always 202, whether or not it exists.");
    }

    public override async Task HandleAsync(EmailRequest req, CancellationToken ct)
    {
        var user = await _repository.FindByEmailAsync(EmailNormalizer.Normalize(req.Email), ct);

        if (user is not null)
        {
            var token = await _repository.IssueTokenAsync(user.Id, AuthTokenPurpose.MagicLink, MagicLinkLifetime, ct);
            await _emailSender.SendAsync(
                _emailBuilder.BuildMagicLink(user.Email, user.DisplayName, token, AuthEmailBuilder.ResolveLocale(HttpContext)), ct);
        }

        await Send.ResultAsync(TypedResults.Accepted((string?)null));
    }
}

public sealed class RequestMagicLinkValidator : Validator<EmailRequest>
{
    public RequestMagicLinkValidator()
    {
        Include(new EmailRequestValidator());
    }
}
