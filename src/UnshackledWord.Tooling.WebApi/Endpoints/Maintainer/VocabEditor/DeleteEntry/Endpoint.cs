using FastEndpoints;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.DeleteEntry;

/// <summary>Deletes an entry without Strong's keys (409 while it has keys). Its meanings go with it; a delete row stays in the change log.</summary>
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
        Delete("vocab/entries/{id}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Deletes an entry without Strong's keys (409 while it has keys). Its meanings go with it; a delete row stays in the change log.");
    }

    public override async Task HandleAsync(VocabEntryIdRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.DeleteAsync(req.Id, UserId, ct), ct);
    }
}
