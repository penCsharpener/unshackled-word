using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentMapping.GetPackage;

[RequiresLogin]
public sealed class Endpoint : Endpoint<GetAgentPackageRequest, GetAgentPackageResponse>
{
    private readonly IAgentPackageBuilder _builder;
    private readonly ILogger<Endpoint> _logger;

    public Endpoint(IAgentPackageBuilder builder, ILogger<Endpoint> logger)
    {
        _builder = builder;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("agent/package");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Debug view of roadmap 0016: builds the agent work packages (local ids, fixed/hint/open per German token, lemma lexicon) for the verses fromVerse..toVerse of one chapter (book = BibleBookId), saves a run and the packages, and returns them with the token shares. Optional mode redo|fill-open|lemma, tokenBudget. 404 when the verses have no German words.");
    }

    public override async Task HandleAsync(GetAgentPackageRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        AgentModes.TryParse(req.Mode, out var mode);
        var range = new AgentRange
        {
            BibleBookId = req.Book,
            FromChapter = req.Chapter,
            FromVerse = req.FromVerse,
            ToChapter = req.Chapter,
            ToVerse = req.ToVerse ?? req.FromVerse,
        };

        var response = await _builder.BuildAsync(range, mode, req.TokenBudget, userId, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var s = response.Stats;
        _logger.LogInformation(
            "Agent packages book {Book} {Chapter}:{From}-{To} ({Mode}): {Packages} package(s), {Tokens} German tokens (fixed {Fixed} = {FixedShare}%, hint {Hint} = {HintShare}%, open {Open} = {OpenShare}%), lemma fixed/hint/open {LemmaFixed}/{LemmaHint}/{LemmaOpen}, {Skipped} verse(s) skipped as fully fixed",
            req.Book, req.Chapter, req.FromVerse, range.ToVerse, response.Mode, response.Packages.Count, s.GermanTokens, s.Fixed, s.FixedShare, s.Hint, s.HintShare, s.Open, s.OpenShare, s.LemmaFixed, s.LemmaHint, s.LemmaOpen, s.SkippedVerses);

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(response, ct);
    }
}

public sealed class Validator : Validator<GetAgentPackageRequest>
{
    private const int MaxVerses = 100;

    public Validator()
    {
        RuleFor(x => x.Book).InclusiveBetween(1, 66);
        RuleFor(x => x.Chapter).GreaterThan(0);
        RuleFor(x => x.FromVerse).GreaterThan(0);
        RuleFor(x => x.ToVerse).GreaterThanOrEqualTo(x => x.FromVerse).When(x => x.ToVerse is not null);
        RuleFor(x => x.ToVerse).Must((r, to) => to is null || to - r.FromVerse < MaxVerses).WithMessage($"At most {MaxVerses} verses per request.");
        RuleFor(x => x.Mode).Must(m => AgentModes.TryParse(m, out _)).WithMessage(AgentModes.ValidModesMessage);
        RuleFor(x => x.TokenBudget).InclusiveBetween(300, 100_000).When(x => x.TokenBudget is not null);
    }
}
