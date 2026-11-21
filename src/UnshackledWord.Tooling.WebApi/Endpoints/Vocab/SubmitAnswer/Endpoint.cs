using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.SubmitAnswer;

/// <summary>Judges the answer to the open card on the server and schedules the card with FSRS (roadmap 0022.8). Login required.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabSessionEndpoint<SubmitVocabAnswerRequest, VocabAnswerResponse>
{
    private readonly VocabSessionService _service;

    public Endpoint(VocabSessionService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("sessions/{id}/answers");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Answers the open card: `seq` of the card plus `optionId` for multiple choice (omitted for an introduction). Returns the verdict, the correct answers, the strength before and after and, at the end, the summary. 409 for a stale seq, 400 for an unknown option.");
    }

    public override async Task HandleAsync(SubmitVocabAnswerRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _service.AnswerAsync(UserId, req.Id, req.Seq, req.OptionId, req.ResponseMs, ct), ct);
    }
}

public sealed class Validator : Validator<SubmitVocabAnswerRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Seq).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OptionId).GreaterThanOrEqualTo(0).When(x => x.OptionId is not null);
        RuleFor(x => x.ResponseMs).InclusiveBetween(0, 3_600_000).When(x => x.ResponseMs is not null);
    }
}
