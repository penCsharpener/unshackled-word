using FastEndpoints;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.Retire;

/// <summary>Retires the entry: out of the courses, no rank, ranks recomputed, ContentVersion bumped, one retire row. Cards stay and show entfernt.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<VocabEntryIdRequest>
{
    private readonly VocabEntryEditor _editor;

    public Endpoint(VocabEntryEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Post("vocab/entries/{id}/retire");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Retires the entry: out of the courses, no rank, ranks recomputed, ContentVersion bumped, one retire row. Cards stay and show entfernt.");
    }

    public override async Task HandleAsync(VocabEntryIdRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.RetireAsync(req.Id, UserId, ct), ct);
    }
}
