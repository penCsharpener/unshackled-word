using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.StartSession;

/// <summary>Starts the study session of a course, or resumes the open one (roadmap 0022.8). Login required.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabSessionEndpoint<StartVocabSessionRequest, StartVocabSessionResponse>
{
    private readonly VocabSessionService _service;

    public Endpoint(VocabSessionService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("sessions");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Starts a study session of a course (up to 20 words: due reviews first, then 4–10 new words by frequency rank) or resumes the open one. 404 for an unknown course; when there is nothing to study: 200 with finished true, no session and an empty summary.");
    }

    public override async Task HandleAsync(StartVocabSessionRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _service.StartAsync(UserId, req.Course, ct), ct);
    }
}

public sealed class Validator : Validator<StartVocabSessionRequest>
{
    public Validator()
    {
        RuleFor(x => x.Course).NotEmpty().MaximumLength(30);
    }
}
