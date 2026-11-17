using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.ApiTokens;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ApiTokens.Create;

[RequiresLogin]
public sealed class Endpoint : Endpoint<CreateApiTokenRequest, CreateApiTokenResponse>
{
    private readonly IApiTokenService _service;
    private readonly IAgentRunRepository _runs;
    private readonly TimeProvider _time;

    public Endpoint(IApiTokenService service, IAgentRunRepository runs, TimeProvider time)
    {
        _service = service;
        _runs = runs;
        _time = time;
    }

    public override void Configure()
    {
        Post("api-tokens");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Creates an MCP token (scope propose) for a user with role ai, maintainer or admin, given by userId or by model (the user ai:<model> is created on first use). The token string is in the response once; only its hash is stored (roadmap 0016.8).");
    }

    public override async Task HandleAsync(CreateApiTokenRequest req, CancellationToken ct)
    {
        var creator = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userId = string.IsNullOrWhiteSpace(req.Model) ? req.UserId : await _runs.GetOrCreateAiUserAsync(req.Model.Trim(), ct);
        var result = await _service.CreateAsync(userId, req.Name, req.ExpiresInDays ?? ApiTokenService.DefaultDays, creator, ct);

        switch (result.Outcome)
        {
            case ApiTokenCreateOutcome.UserNotFound:
                AddError(r => r.UserId, "No user with this id.");
                await Send.ErrorsAsync(StatusCodes.Status404NotFound, ct);
                return;
            case ApiTokenCreateOutcome.UserNotAllowed:
                AddError(r => r.UserId, "Tokens can only be issued for users with role ai, maintainer or admin.");
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(new CreateApiTokenResponse { Token = ApiTokenMapping.ToDto(result.Created!.Record, _time.GetUtcNow().UtcDateTime), Secret = result.Created.Secret }, ct);
    }
}

public sealed class Validator : Validator<CreateApiTokenRequest>
{
    public Validator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).When(x => string.IsNullOrWhiteSpace(x.Model));
        RuleFor(x => x.Model).MaximumLength(200);
        RuleFor(x => x.UserId).Equal(0).When(x => !string.IsNullOrWhiteSpace(x.Model)).WithMessage("Send either userId or model, not both.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, ApiTokenService.MaxDays).When(x => x.ExpiresInDays is not null);
    }
}
