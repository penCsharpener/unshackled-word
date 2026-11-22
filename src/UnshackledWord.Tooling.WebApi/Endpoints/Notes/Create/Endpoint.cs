using FastEndpoints;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes.Create;

/// <summary>Creates a private note of the signed-in user on a chapter or verse (roadmap 0023.1).</summary>
[RequiresLogin]
public sealed class Endpoint : NoteEndpoint<CreateNoteRequest, NoteResponse>
{
    private readonly NoteService _service;

    public Endpoint(NoteService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<NoteResponse>(201).ProducesProblem(400));
        Summary(s => s.Summary = "Creates a private note. Anchor kinds `chapter`, `verses` (same chapter, start < end), `verse` and `words` (Elb1871 positions inside one verse, the server fills `quotedText`); chapter/verses/words must exist in Elb1871. Body 1..20000 characters of Markdown. The author is the signed-in user.");
    }

    public override async Task HandleAsync(CreateNoteRequest req, CancellationToken ct) =>
        await SendOutcomeAsync(await _service.CreateAsync(UserId, req, ct), StatusCodes.Status201Created, ct);
}

public sealed class Validator : Validator<CreateNoteRequest>
{
    public Validator()
    {
        Include(new CreateNoteRequestValidator());
    }
}
